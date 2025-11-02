module LizardGenFs.CppHeader
open LizardGenFs.CppCommon
open LizardGenFs.Types
open LizardGenFs.Util

let private nl = System.Environment.NewLine
let private commonHeader namespaceName= $$"""#pragma once

#include <cstdint>
#include <functional>
#include <memory>
#include <mutex>
#include <string>
#include "Serdes.h"

namespace {{namespaceName}}
{
    enum LogLevel { Debug, Info, Warn, Error, Fatal };
    class ILogger
    {
    public:
        virtual ~ILogger() = default;
        virtual void Debug(std::string_view message) = 0;
        virtual void Info(std::string_view message) = 0;
        virtual void Warn(std::string_view message) = 0;
        virtual void Error(std::string_view message) = 0;
        virtual void Fatal(std::string_view message) = 0;
    };

    class ISocket
    {
    public:
		virtual ~ISocket() = default;
        virtual ISocket* Accept() = 0;
		virtual void Send(std::span<const uint8_t> buffer) = 0;
		virtual void Receive(std::span<uint8_t> buffer) = 0;
        virtual void GetPeerAddress(uint32_t &ip, uint16_t& port) const = 0;
        virtual std::string GetPeerAddress() const = 0;
        virtual void Close() = 0;
    };

    class BitfieldEvent
    {
        int state_;
        std::mutex m_;
        std::condition_variable cv_;

    public:
        BitfieldEvent() : state_(false) {}
        void set(int flags)
        {
            std::unique_lock ul(m_);
            state_ |= flags;
            cv_.notify_all();
        }

        void clear(int flags)
        {
            std::unique_lock ul(m_);
            state_ &= ~flags;
        }

        int wait_any(int flags)
        {
            std::unique_lock ul(m_);
            while ((state_ & flags) == 0) {
                cv_.wait(ul);
            }

            return state_;
        }

        void wait_all(int flags)
        {
            std::unique_lock ul(m_);
            while ((state_ & flags) != flags) {
                cv_.wait(ul);
            }
        }

        int get(int flags) const
        {
            std::unique_lock ul(*const_cast<std::mutex*>(&m_));
            return state_ & flags;
        }
    };

    class ManualResetEvent
    {
        BitfieldEvent e_;

    public:
        void set() { e_.set(1); }
        void reset() { e_.clear(1); }
        void wait() { e_.wait_any(1); }
        bool get() const { return e_.get(1) != 0; }
    };

    uint8_t ClientHandshake(ISocket& socket, uint8_t maxClientVersion);
    uint8_t ServerHandshake(ISocket& socket, uint8_t maxServerVersion);

    class IDuplex
    {
    public:
        virtual ~IDuplex() = default;
        virtual void SendAndReplaceBufferWithResponse(std::vector<uint8_t>& buffer) = 0;
        virtual void WaitForExit() = 0;
        virtual void Disconnect() = 0;
    };

    IDuplex* CreateDuplex(
        std::unique_ptr<ISocket> socket,
        const std::shared_ptr<ILogger>& log,
        std::function<void(std::vector<uint8_t>&)> receiveAndReplaceBufferWithResponse);

    class IDeserializer
    {
    public:
        virtual ~IDeserializer() = default;
        virtual void HandleMessage(std::vector<uint8_t>& buffer) = 0;
    };

    class CommsError : public std::exception
    {
    public:
        explicit CommsError(const std::string& message) : std::exception(message.c_str()) {}
    };

"""

let private generateEnum (e : EnumDef) =
    seq {
        yield $$"""    enum class {{e.name}} : {{backingTypeName e.backingType}}
    {
"""

        let lines =
            e.values
            |> List.map (fun (name, v) -> $"        {name} = {v}")

        yield String.concat ("," + nl) lines
        yield """
    };

"""
    } |> String.concat ""

let private generateStruct (s : StructDef) =
    seq {
        yield $$"""    struct {{s.name}}
    {
"""

        for (name, t) in s.members do
            yield $$"""        {{typeName t}} {{name}};
"""

        yield """
        template<typename Tname>
        void Serdes(Tname name, ISerdes& s)
        {
            s.Begin(name);
"""

        for (name, t) in s.members do
            yield indentText 3 (serdesCall "s" name t)
            yield """;
"""

        yield """            s.End();
        }
    };

"""
    } |> String.concat ""

let private methodSignature (m : MethodDef) =
    let returnType = returnTypeName m.returnType
    let paramString =
        m.parameters
        |> List.map (fun (n, t) -> $"{paramTypeName t} {n}")
        |> String.concat ", "

    $"{returnType } {m.name}({paramString})"

let private generateServiceInterface (s : ServiceDef) =
    seq {
        yield $$"""    class I{{s.name}}
    {
    public:
        virtual ~I{{s.name}}() = default;
"""

        for m in s.methods do
            yield "        virtual "
            yield methodSignature m
            yield " = 0;" + nl

        yield "    };" + nl + nl
    } |> String.concat ""

let private generateServiceDeserializerFactory (s : ServiceDef) =
    $"    IDeserializer* Make{s.name}Deserializer(I{s.name}& receiver);" + nl

let private generateServiceSerializerFactory (s : ServiceDef) =
    $"    I{s.name}* Make{s.name}Serializer(std::function<void(std::vector<uint8_t>&)> sendCallback);" + nl + nl

let private generateServiceHeader (s : ServiceDef) =
    seq {
        yield generateServiceInterface s
        yield generateServiceDeserializerFactory s
        yield generateServiceSerializerFactory s
    } |> String.concat ""

let generateHeader namespaceName types =
    let genType =
        function
        | Basic   _ -> "" // predefined, don't need to emit anything
        | Enum    e -> generateEnum e
        | Struct  s -> generateStruct s
        | Array   _ -> failwith "Arrays cannot be top-level types in Lizard-proto"
        | Service s -> generateServiceHeader s

    seq {
    yield commonHeader namespaceName

    for t in types do
        yield (genType t)

    yield "}"
    } |> String.concat ""

