module LizardGenFs.CppImpl
open LizardGenFs.CppCommon
open LizardGenFs.Types
open LizardGenFs.Util

let private nl = System.Environment.NewLine

let private commonImpl namespaceName = $$"""#include "{{namespaceName}}.g.h"
#include <cstdio>
#include <format>
#include <functional>
#include <future>
#include <span>

namespace {{namespaceName}}
{
    uint8_t ClientHandshake(ISocket& socket, uint8_t maxClientVersion)
    {
        const uint8_t buffer[] = { 'L', 'i', 'z', 'a', 'r', 'd', maxClientVersion };
        socket.Send(buffer);

        uint8_t maxServerVersion;
        socket.Receive(std::span(&maxServerVersion, 1));
        return std::min(maxClientVersion, maxServerVersion);
    }

    uint8_t ServerHandshake(ISocket& socket, uint8_t maxServerVersion)
    {
        static uint8_t HandshakeMagic[] = { 'L', 'i', 'z', 'a', 'r', 'd' /*, clientVersion*/ };
        uint8_t buffer[sizeof(HandshakeMagic) + 1] = {};

        socket.Receive(buffer);
        if (std::memcmp(buffer, HandshakeMagic, sizeof(HandshakeMagic)) != 0)
            throw CommsError("Invalid handshake received");

        uint8_t maxClientVersion = HandshakeMagic[sizeof(HandshakeMagic) - 1];
        uint8_t version = std::min(maxClientVersion, maxServerVersion);

        socket.Send(std::span(&maxServerVersion, 1));
        return version;
    }

    class BaseDeserializer
    {
    protected:
        void Respond(std::vector<uint8_t>& buffer) const
        {
            buffer.clear();
        }

        template<class T>
        void Respond(std::vector<uint8_t>& buffer, T& result)
        {
            buffer.clear();
            SerdesWrite sw(buffer);
            result.Serdes("result", sw);
        }

        template<class T>
        void Respond(std::vector<uint8_t>& buffer, std::vector<T>& results)
        {
            buffer.clear();
            SerdesWrite sw(buffer);
            uint32_t count = static_cast<uint32_t>(results.size());
            sw.UInt32("count", count);

            for (size_t i = 0; i < results.size(); i++)
            {
                T& result = results.at(i);
                result.Serdes(std::to_string(i), sw);
            }
        }

        template<>
        void Respond(std::vector<uint8_t>& buffer, std::vector<uint8_t>& results)
        {
            buffer.clear();
            SerdesWrite sw(buffer);
            sw.Bytes("result", results) ;
        }

        template<>
        void Respond(std::vector<uint8_t>& buffer, uint32_t& result)
        {
            buffer.clear();
            SerdesWrite sw(buffer);
            sw.UInt32("result", result);
        }
    };

    class BaseSerializer
    {
    protected:
        std::function<void(std::vector<uint8_t>&)> sendCallback_;

        explicit BaseSerializer(std::function<void(std::vector<uint8_t>&)> sendCallback)
            : sendCallback_(std::move(sendCallback))
        {
        }

        template<class T>
        std::vector<T> DecodeListResponse(std::span<uint8_t> response)
        {
            SerdesRead s(response);
            std::vector<T> results;
            s.Array("results", results);
            return results;
        }

        template<class T>
        T DecodeResponse(std::span<uint8_t> response)
        {
            SerdesRead s(response);
            T result;
            result.Serdes("result", s);
            return result;
        }

        template<>
        uint32_t DecodeResponse(std::span<uint8_t> response)
        {
            SerdesRead s(response);
            uint32_t result;
            s.UInt32("result", result);
            return result;
        }
    };

    class Duplex final : public IDuplex
    {
        static constexpr int MAX_CONCURRENCY = 16;

        enum class PacketType : uint8_t
        {
            Unknown = 0,
            Request = 1,
            ResponseOk = 2,
            ResponseFail = 3
        };

        const char* DescribePacketType(PacketType type)
        {
            switch (type)
            {
            case PacketType::Unknown: return "Unknown";
            case PacketType::Request: return "Request";
            case PacketType::ResponseOk: return "ResponseOk";
            case PacketType::ResponseFail: return "ResponseFail";
            default: return "Unknown";
            }
        }

        class Packet
        {
            std::vector<uint8_t> ownedData;

        public:
            PacketType type;
            uint8_t id;
            std::vector<uint8_t>& data;
            ManualResetEvent done;

            Packet(PacketType typeParam, uint8_t idParam) :
                type(typeParam),
                id(idParam),
                data(ownedData)
            {
            }

            Packet(PacketType typeParam, uint8_t idParam, std::vector<uint8_t>& dataParam) :
                type(typeParam),
                id(idParam),
                data(dataParam)
            {
            }

            Packet(const Packet&) = delete;
            Packet& operator=(const Packet&) = delete;
            ~Packet() = default;
        };

        class DuplexEvents
        {
            enum EventFlags
            {
                Done = 1 << 0,
                SendQueue = 1 << 1,
                ReceiveQueue = 1 << 2,
            };

            BitfieldEvent e_;

        public:
            void SetDone() { e_.set(Done); }
            void SendQueueAvailable() { e_.set(SendQueue); }
            void SendQueueCleared() { e_.clear(SendQueue); }
            void WaitUntilDone() { e_.wait_any(Done); }
            bool IsDone() const { return e_.get(Done) != 0; }

            int WaitForSendOrDone() // Return 0 if done
            {
                int result = e_.wait_any(Done | SendQueue);
                return result & Done ? 0 : 1;
            }
        };

        std::unique_ptr<ISocket> socket_;
        DuplexEvents events_;
        std::mutex mutex_;
        std::list<uint8_t> freeIds_;
        std::shared_ptr<ILogger> log_;
        std::list<std::shared_ptr<Packet> > sendQueue_;
        std::list<std::shared_ptr<Packet> > pendingRequests_;
        std::list<std::shared_ptr<Packet> > receiveQueue_;
        std::unique_ptr<std::thread>        sendThread_;
        std::unique_ptr<std::thread>        receiveThread_;
        std::function<void(std::vector<uint8_t>&)> receiveFunc_;

        // Must be called with mutex_ held
        bool TryGetId(uint8_t& id)
        {
            if (freeIds_.empty())
                return false;

            id = freeIds_.front();
            freeIds_.pop_front();
            return true;
        }

        // Must be called with mutex_ held
        void ReturnId(uint8_t id) { freeIds_.push_back(id); }

        void SendLoop()
        {
            while (events_.WaitForSendOrDone()) // Returns 0 when done
            {
                std::shared_ptr<Packet> packet = TryPopSendQueue();
                if (packet == nullptr)
                    continue;

                if (!SendPacket(*packet))
                {
                    events_.SetDone();
                    break;
                }

                if (packet->type == PacketType::Request)
                {
                    std::unique_lock lock(mutex_);
                    pendingRequests_.emplace_back(packet);
                }
                else
                {
                    packet->done.set();
                }
            }

            log_->Debug("Exiting Duplex::SendLoop");
        }

        std::shared_ptr<Packet> TryPopSendQueue()
        {
            std::unique_lock lock(mutex_);
            if (sendQueue_.empty())
            {
                events_.SendQueueCleared();
                return nullptr;
            }

            std::shared_ptr<Packet> packet = sendQueue_.front();
            sendQueue_.pop_front();
            return packet;
        }

        void ReceiveLoop()
        {
            while (!events_.IsDone())
            {
                try
                {
                    if (!ReceivePacket())
                        events_.SetDone();
                }
                catch (const CommsError&)
                {
                    events_.SetDone();
                }
            }

            log_->Debug("Exiting Duplex::ReceiveLoop");
        }

        bool SendPacket(Packet& packet)
        {
            std::vector<uint8_t> header;
            header.reserve(6);

            SerdesWrite sw(header);
            uint32_t size = packet.data.size();
            sw.UInt32("size", size);
            sw.UInt8Enum("type", packet.type);
            sw.UInt8("id", packet.id);
            log_->Debug(std::format("SEND {} {} - {} bytes", (int)packet.id, DescribePacketType(packet.type), (int)size));

            socket_->Send(header);
            socket_->Send(packet.data);
            return true;
        }

        bool ReceivePacket()
        {
            uint8_t header[6];
            socket_->Receive(header);

            uint32_t size = 0;
            PacketType type = PacketType::Request;
            uint8_t id = 0;

            SerdesRead sr(std::span(header, sizeof(header)));
            sr.UInt32("size", size);
            sr.UInt8Enum("type", type);
            sr.UInt8("id", id);

            std::shared_ptr<Packet> packet;
            {
                std::unique_lock lock(mutex_);
                if (type == PacketType::ResponseOk || type == PacketType::ResponseFail)
                {
                    auto it = std::ranges::find_if(pendingRequests_,
                        [id](const std::shared_ptr<Packet>& p) { return p->id == id; });

                    if (it == pendingRequests_.end())
                    {
                        log_->Error(std::format("Duplex::ReceivePacket: Received response with unknown id {}", id));
                        return false;
                    }

                    packet = *it;
                    packet->type = type;
                    pendingRequests_.erase(it);
                }
                else if (type == PacketType::Request)
                {
                    packet = std::make_shared<Packet>(type, id);
                }
                else
                {
                    log_->Error(std::format("Duplex::ReceivePacket: Received unexpected packet type {}", static_cast<int>(type)));
                    return false;
                }
            }

            packet->data.resize(size);
            log_->Debug(std::format("RECV {} {} - {} bytes", (int)id, DescribePacketType(type), (int)size));
            socket_->Receive(packet->data);

            if (type == PacketType::ResponseOk || type == PacketType::ResponseFail)
            {
                // Set the event to unblock the synchronous sender so it can decode and handle the response.
                packet->done.set();
            }
            else if (type == PacketType::Request)
            {
                auto future = std::async(
                    std::launch::async,
                    &Duplex::HandleIncomingRequest,
                    this,
                    packet);
            }
            else
            {
                log_->Warn(std::format("Duplex::ReceivePacket: Unexpected packet type \"{}\" received", (int)type));
            }

            return true;
        }

        void HandleIncomingRequest(std::shared_ptr<Packet> packet)
        {
            if (packet == nullptr)
                return;

            try
            {
                receiveFunc_(packet->data);
                packet->type = PacketType::ResponseOk;
            }
            catch (const std::exception& ex)
            {
                std::string msg = ex.what();
                packet->data.clear();
                packet->data.insert(packet->data.end(), msg.begin(), msg.end());
                packet->type = PacketType::ResponseFail;
            }

            {
                std::unique_lock lock(mutex_);
                EnqueueForSend(packet);
            }
        }

        void EnqueueForSend(std::shared_ptr<Packet> packet)
        {
            sendQueue_.emplace_back(packet);
            events_.SendQueueAvailable();
        }

    public:
        Duplex(
            std::unique_ptr<ISocket> socket,
            const std::shared_ptr<ILogger>& log,
            std::function<void(std::vector<uint8_t>&)> receiveAndReplaceBufferWithResponse
        ) :
            socket_(std::move(socket)),
            log_(log),
            receiveFunc_(std::move(receiveAndReplaceBufferWithResponse))
        {
            sendThread_ = std::make_unique<std::thread>(&Duplex::SendLoop, this);
            receiveThread_ = std::make_unique<std::thread>(&Duplex::ReceiveLoop, this);

            for (uint8_t i = 0; i < MAX_CONCURRENCY; i++)
                freeIds_.push_back(i);
        }

        ~Duplex() override
        {
            events_.SetDone();
            sendThread_->join();
            receiveThread_->join();
        }

        void WaitForExit() override { events_.WaitUntilDone(); }
        void Disconnect() override
        {
            socket_->Close();
            events_.SetDone();
        }

        void SendAndReplaceBufferWithResponse(std::vector<uint8_t>& buffer) override
        {
            // Outbound packet lifecycle:
            // 1. Created and added to sendQueue
            // 2. Moved to pendingRequests when sent
            // 3. Removed from pendingRequests when response received, buffer contents replaced and event set
            // 4. Destroyed when going out of scope after Send returns
            std::shared_ptr<Packet> packet;
            {
                std::unique_lock lock(mutex_);
                uint8_t id;
                if (!TryGetId(id))
                    throw CommsError("Maximum concurrency reached");

                packet = std::make_shared<Packet>(PacketType::Request, id, buffer);
                EnqueueForSend(packet);
            }

            packet->done.wait();

            ReturnId(packet->id);

            if (packet->type == PacketType::ResponseFail)
                throw CommsError(std::string(packet->data.begin(), packet->data.end()));
        }
    };

    IDuplex* CreateDuplex(std::unique_ptr<ISocket> socket, const std::shared_ptr<ILogger>& log, std::function<void(std::vector<uint8_t>&)> receive)
    {
        return new Duplex(std::move(socket), log, std::move(receive));
    }

"""

let private generateServiceEnum (s : ServiceDef) =
    seq {
        yield $$"""    enum class {{s.name}}Message : uint16_t
    {
"""

        yield """        Unknown = 0,
"""

        let offset = 1

        yield
            s.methods
            |> List.mapi (fun i m -> $"        {m.name} = {i + offset}")
            |> String.concat ("," + nl)

        yield """
    };

"""
    } |> String.concat ""

let private generateDeserializerMethod (s : ServiceDef) (m : MethodDef) =
    seq {

    yield $$"""
                case {{s.name}}Message::{{m.name}}:
                {"""

    for p in m.parameters do
        yield $$"""
                    {{typeName (snd p)}} {{fst p}};"""

    for p in m.parameters do
        yield $$"""
                    {{serdesCall "sr" (fst p) (snd p)}};"""

    if (m.returnType <> (Basic Void)) then
        yield $$"""
                    {{returnTypeName m.returnType}} result = receiver_.{{m.name}}({{String.concat ", " (List.map fst m.parameters)}});
                    Respond(buffer, result);
                    break;
                }"""
    else
        yield $$"""
                    receiver_.{{m.name}}({{String.concat ", " (List.map fst m.parameters)}});
                    Respond(buffer);
                    break;
                }"""
    } |> String.concat ""

let private generateServiceDeserializer (s : ServiceDef) =
    seq {

    yield $$"""    class {{s.name}}Deserializer final : public IDeserializer, BaseDeserializer
    {
        I{{s.name}}& receiver_;

    public:
        explicit {{s.name}}Deserializer(I{{s.name}}& receiver) : receiver_(receiver) {}
        void HandleMessage(std::vector<uint8_t>& buffer) override
        {
            if (buffer.empty())
                throw CommsError("Insufficient data for {{s.name}} deserialization");

            SerdesRead sr(buffer);
            {{s.name}}Message messageType = {{s.name}}Message::Unknown;
            sr.UInt16Enum("type", messageType);

            switch (messageType)
            {
                case {{s.name}}Message::Unknown:
                    throw CommsError("Unknown message type received in {{s.name}} deserializer");
"""

    yield s.methods
        |> List.map (generateDeserializerMethod s)
        |> String.concat nl

    yield $$"""
            }

#ifdef _DEBUG
            if (sr.BytesRemaining() != 0)
                throw CommsError("Unexpected data remaining after processing {{s.name}} message");
#endif
        }
    };

    IDeserializer* Make{{s.name}}Deserializer(I{{s.name}}& receiver) { return new {{s.name}}Deserializer(receiver); }
"""

    } |> String.concat ""

let private generateServiceSerializer (s : ServiceDef) =
    seq {

    yield $$"""
    class {{s.name}}Serializer final : public I{{s.name}}, BaseSerializer
    {
    public:
        explicit {{s.name}}Serializer(std::function<void(std::vector<uint8_t>&)> sendCallback)
            : BaseSerializer(std::move(sendCallback))
        {
        }
"""
    for m in s.methods do
        let paramText =
            m.parameters
            |> List.map (fun (n, t) -> $"{paramTypeName t} {n}")
            |> String.concat ", "

        yield $$"""
        {{returnTypeName m.returnType}} {{m.name}}({{paramText}}) override
        {
            std::vector<uint8_t> buffer;
            SerdesWrite sw(buffer);
            {{s.name}}Message type = {{s.name}}Message::{{m.name}};

            sw.UInt16Enum("type", type);
"""

        for p in m.parameters do
            yield indentText 3 (serdesCall "sw" (fst p) (snd p))
            yield ";" + nl

        yield "            sendCallback_(buffer);" + nl

        match m.returnType with
        | Basic Void -> ()
        | Array (Basic UInt8) -> yield "            return buffer;" + nl
        | Array t -> yield $"            return DecodeListResponse<{typeName t}>(buffer);" + nl
        | _ -> yield $"            return DecodeResponse<{returnTypeName m.returnType}>(buffer);" + nl

        yield "        }" + nl

    yield $$"""    };

    I{{s.name}}* Make{{s.name}}Serializer(std::function<void(std::vector<uint8_t>&)> sendCallback)
    {
        return new {{s.name}}Serializer(std::move(sendCallback));
    }

"""
    } |> String.concat ""

let generateImplementation namespaceName types =
    seq {
    yield commonImpl namespaceName

    for t in types do
        match t with
        | Service s ->
            yield generateServiceEnum s
            yield generateServiceDeserializer s
            yield generateServiceSerializer   s
        | _ -> ()

    yield "}"
    } |> String.concat ""
