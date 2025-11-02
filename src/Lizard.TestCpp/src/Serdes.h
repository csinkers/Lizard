#pragma once
#include <algorithm>
#include <vector>
#include <span>
#include <stdexcept>
#include <functional>

class ISerdes
{
public:
	ISerdes() = default;
	ISerdes(const ISerdes&) = delete;
	ISerdes(ISerdes&&) = delete;
	bool operator=(const ISerdes&) const = delete;
	bool operator=(ISerdes&&) const = delete;

	// Begin virtual methods
	virtual ~ISerdes() = default;

	virtual size_t Offset() = 0;
	virtual size_t BytesRemaining() = 0;

	virtual void Begin(std::string_view name) = 0;
	virtual void Begin(int name) = 0;
	virtual void End() = 0;
	virtual void NewLine() = 0;
	virtual void Comment(std::string_view comment) = 0;

	virtual void  UInt8(std::string_view name,  uint8_t& value) = 0;
	virtual void UInt16(std::string_view name, uint16_t& value) = 0;
	virtual void UInt32(std::string_view name, uint32_t& value) = 0;
	virtual void UInt64(std::string_view name, uint64_t& value) = 0;

	virtual void  Int8(std::string_view name,  int8_t& value) = 0;
	virtual void Int16(std::string_view name, int16_t& value) = 0;
	virtual void Int32(std::string_view name, int32_t& value) = 0;
	virtual void Int64(std::string_view name, int64_t& value) = 0;

	virtual void String(std::string_view name, std::string& value) = 0;

	virtual void  UInt8(int name,  uint8_t& value) = 0;
	virtual void UInt16(int name, uint16_t& value) = 0;
	virtual void UInt32(int name, uint32_t& value) = 0;
	virtual void UInt64(int name, uint64_t& value) = 0;

	virtual void  Int8(int name,  int8_t& value) = 0;
	virtual void Int16(int name, int16_t& value) = 0;
	virtual void Int32(int name, int32_t& value) = 0;
	virtual void Int64(int name, int64_t& value) = 0;

	virtual void String(int name, std::string& value) = 0;

	virtual void Bytes(std::string_view name, std::vector<uint8_t>& buffer) = 0;
	// End virtual methods

	template<class Tname> // Tname = std::string_view or int
	void Bool(Tname name, bool& value)
	{
		uint8_t val = value ? 1 : 0;
		UInt8(name, val);
		value = (val != 0);
	}

	template<class T, class Tname> // Tname = std::string_view or int
	void Int8Enum(Tname name, T& value)
	{
		int8_t val = static_cast<int8_t>(value);
		Int8(name, val);
		value = static_cast<T>(val);
	}

	template<class T, class Tname> // Tname = std::string_view or int
	void Int16Enum(Tname name, T& value)
	{
		int16_t val = static_cast<int16_t>(value);
		Int16(name, val);
		value = static_cast<T>(val);
	}

	template<class T, class Tname> // Tname = std::string_view or int
	void Int32Enum(Tname name, T& value)
	{
		int32_t val = static_cast<int32_t>(value);
		Int32(name, val);
		value = static_cast<T>(val);
	}

	template<class T, class Tname> // Tname = std::string_view or int
	void UInt8Enum(Tname name, T& value)
	{
		uint8_t val = static_cast<uint8_t>(value);
		UInt8(name, val);
		value = static_cast<T>(val);
	}

	template<class T, class Tname> // Tname = std::string_view or int
	void UInt16Enum(Tname name, T& value)
	{
		uint16_t val = static_cast<uint16_t>(value);
		UInt16(name, val);
		value = static_cast<T>(val);
	}

	template<class T, class Tname> // Tname = std::string_view or int
	void UInt32Enum(Tname name, T& value)
	{
		uint32_t val = static_cast<uint32_t>(value);
		UInt32(name, val);
		value = static_cast<T>(val);
	}

	template<typename T, typename Tname> // Tname = std::string_view or int
	void Array(Tname name, std::vector<T>& value)
	{
		Begin(name);

		uint32_t count = static_cast<uint32_t>(value.size());
		UInt32("count", count);
		value.resize(count);

		for (int i = 0; i < static_cast<int>(value.size()); ++i)
		{
			T& item = value[i];
			item.Serdes(i, *this);
		}

		End();
	}

	template<typename T, typename Tname> // Tname = std::string_view or int
	void Array(Tname name, std::vector<T>& value, std::function<void(int, T&, ISerdes&)> serdes)
	{
		Begin(name);

		uint32_t count = static_cast<uint32_t>(value.size());
		UInt32("count", count);
		value.resize(count);

		for (int i = 0; i < static_cast<int>(value.size()); ++i)
		{
			T& item = value[i];
			serdes(i, item, *this);
		}

		End();
	}
};

class SerdesWrite final : public ISerdes
{
	std::vector<uint8_t>& m_buffer;

public:
	explicit SerdesWrite(std::vector<uint8_t>& buffer): m_buffer(buffer) { }
	SerdesWrite(const SerdesWrite&) = delete;
	SerdesWrite(SerdesWrite&&) = delete;
	SerdesWrite& operator=(const SerdesWrite&) = delete;
	SerdesWrite& operator=(SerdesWrite&&) = delete;
	~SerdesWrite() override = default;

	size_t Offset() override { return m_buffer.size(); }
	size_t BytesRemaining() override { return std::numeric_limits<size_t>::max(); }

	void Begin([[maybe_unused]] std::string_view name) override {}
	void Begin([[maybe_unused]] int name) override {}
	void End() override {}
	void NewLine() override {}
	void Comment([[maybe_unused]] std::string_view comment) override {}

	void UInt8([[maybe_unused]] std::string_view name, uint8_t& value) override { UInt8(0, value); }
	void UInt8([[maybe_unused]] int name, uint8_t& value) override
	{
		m_buffer.push_back(value);
	}

	void UInt16([[maybe_unused]] std::string_view name, uint16_t& value) override { UInt16(0, value); }
	void UInt16([[maybe_unused]] int name, uint16_t& value) override
	{
		m_buffer.push_back(static_cast<uint8_t>(value      & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 8 & 0xFF));
	}

	void UInt32([[maybe_unused]] std::string_view name, uint32_t& value) override { UInt32(0, value); }
	void UInt32([[maybe_unused]] int name, uint32_t& value) override
	{
		m_buffer.push_back(static_cast<uint8_t>(value       & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >>  8 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 16 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 24 & 0xFF));
	}

	void UInt64([[maybe_unused]] std::string_view name, uint64_t& value) override { UInt64(0, value); }
	void UInt64([[maybe_unused]] int name, uint64_t& value) override
	{
		m_buffer.push_back(static_cast<uint8_t>(value       & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 8  & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 16 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 24 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 32 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 40 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 48 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 56 & 0xFF));
	}

	void Int8([[maybe_unused]] std::string_view name, int8_t& value) override { Int8(0, value); }
	void Int8([[maybe_unused]] int name, int8_t& value) override
	{
		m_buffer.push_back(static_cast<uint8_t>(value));
	}

	void Int16([[maybe_unused]] std::string_view name, int16_t& value) override { Int16(0, value); }
	void Int16([[maybe_unused]] int name, int16_t& value) override
	{
		m_buffer.push_back(static_cast<uint8_t>(value      & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 8 & 0xFF));
	}

	void Int32([[maybe_unused]] std::string_view name, int32_t& value) override { Int32(0, value); }
	void Int32([[maybe_unused]] int name, int32_t& value) override
	{
		m_buffer.push_back(static_cast<uint8_t>(value       & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 8  & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 16 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 24 & 0xFF));
	}

	void Int64([[maybe_unused]] std::string_view name, int64_t& value) override { Int64(0, value); }
	void Int64([[maybe_unused]] int name, int64_t& value) override
	{
		m_buffer.push_back(static_cast<uint8_t>(value       & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >>  8 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 16 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 24 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 32 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 40 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 48 & 0xFF));
		m_buffer.push_back(static_cast<uint8_t>(value >> 56 & 0xFF));
	}

	void String([[maybe_unused]] std::string_view name, std::string& value) override { String(0, value); }
	void String([[maybe_unused]] int name, std::string& value) override
	{
		uint32_t len = static_cast<uint32_t>(value.size());
		UInt32(name, len);
		m_buffer.insert(m_buffer.end(), value.begin(), value.end());
	}

	void Bytes([[maybe_unused]] std::string_view name, std::vector<uint8_t>& buffer) override
	{
		uint32_t len = static_cast<uint32_t>(buffer.size());
		UInt32(std::string(name) + "Length", len);
		m_buffer.insert(m_buffer.end(), buffer.begin(), buffer.end());
	}
};

class SerdesRead final : public ISerdes
{
	std::span<uint8_t> m_buffer;
	std::span<uint8_t> m_current;

public:
	explicit SerdesRead(const std::span<uint8_t>& buffer) : m_buffer(buffer), m_current(buffer) {}
	SerdesRead(const SerdesRead&) = delete;
	SerdesRead(SerdesRead&&) = delete;
	SerdesRead& operator=(const SerdesRead&) = delete;
	SerdesRead& operator=(SerdesRead&&) = delete;
	~SerdesRead() override = default;

	size_t Offset() override { return m_buffer.size() - m_current.size(); }
	size_t BytesRemaining() override { return m_current.size(); }

	// These are only relevant for commenting proxy types.
	void Begin([[maybe_unused]] std::string_view name) override {}
	void Begin([[maybe_unused]] int name) override {}
	void End() override {}
	void NewLine() override {}
	void Comment([[maybe_unused]] std::string_view comment) override {}

	void UInt8([[maybe_unused]] std::string_view name, uint8_t& value) override { UInt8(0, value); }
	void UInt8([[maybe_unused]] int name, uint8_t& value) override
	{
		if (m_current.empty())
			throw std::runtime_error("Insufficient data for UInt8 deserialization");

		value = m_current[0];
		m_current = m_current.subspan(1);
	}

	void UInt16([[maybe_unused]] std::string_view name, uint16_t& value) override { UInt16(0, value); }
	void UInt16([[maybe_unused]] int name, uint16_t& value) override
	{
		if (m_current.size() < 2)
			throw std::runtime_error("Insufficient data for UInt16 deserialization");

		value = static_cast<uint16_t>(m_current[0]) | static_cast<uint16_t>(m_current[1]) << 8;

		m_current = m_current.subspan(2);
	}

	void UInt32([[maybe_unused]] std::string_view name, uint32_t& value) override { UInt32(0, value); }
	void UInt32([[maybe_unused]] int name, uint32_t& value) override
	{
		if (m_current.size() < 4)
			throw std::runtime_error("Insufficient data for UInt32 deserialization");

		value =
			static_cast<uint32_t>(m_current[0])       |
			static_cast<uint32_t>(m_current[1]) << 8  |
			static_cast<uint32_t>(m_current[2]) << 16 |
			static_cast<uint32_t>(m_current[3]) << 24;

		m_current = m_current.subspan(4);
	}

	void UInt64([[maybe_unused]] std::string_view name, uint64_t& value) override { UInt64(0, value); }
	void UInt64([[maybe_unused]] int name, uint64_t& value) override
	{
		if (m_current.size() < 8)
			throw std::runtime_error("Insufficient data for UInt64 deserialization");

		value =
			static_cast<uint64_t>(m_current[0])       |
			static_cast<uint64_t>(m_current[1]) <<  8 |
			static_cast<uint64_t>(m_current[2]) << 16 |
			static_cast<uint64_t>(m_current[3]) << 24 |
			static_cast<uint64_t>(m_current[4]) << 32 |
			static_cast<uint64_t>(m_current[5]) << 40 |
			static_cast<uint64_t>(m_current[6]) << 48 |
			static_cast<uint64_t>(m_current[7]) << 56;

		m_current = m_current.subspan(8);
	}

	void Int8([[maybe_unused]] std::string_view name, int8_t& value) override { Int8(0, value); }
	void Int8([[maybe_unused]] int name, int8_t& value) override
	{
		if (m_current.empty())
			throw std::runtime_error("Insufficient data for Int8 deserialization");

		value = static_cast<int8_t>(m_current[0]);

		m_current = m_current.subspan(1);
	}

	void Int16([[maybe_unused]] std::string_view name, int16_t& value) override { Int16(0, value); }
	void Int16([[maybe_unused]] int name, int16_t& value) override
	{
		if (m_current.size() < 2)
			throw std::runtime_error("Insufficient data for Int16 deserialization");

		value = static_cast<int16_t>(m_current[0] | (m_current[1] << 8));

		m_current = m_current.subspan(2);
	}

	void Int32([[maybe_unused]] std::string_view name, int32_t& value) override { Int32(0, value); }
	void Int32([[maybe_unused]] int name, int32_t& value) override
	{
		if (m_current.size() < 4)
			throw std::runtime_error("Insufficient data for Int32 deserialization");

		value =
			static_cast<int32_t>(m_current[0])       |
			static_cast<int32_t>(m_current[1]) <<  8 |
			static_cast<int32_t>(m_current[2]) << 16 |
			static_cast<int32_t>(m_current[3]) << 24;

		m_current = m_current.subspan(4);
	}

	void Int64([[maybe_unused]] std::string_view name, int64_t& value) override { Int64(0, value); }
	void Int64([[maybe_unused]] int name, int64_t& value) override
	{
		if (m_current.size() < 8)
			throw std::runtime_error("Insufficient data for Int64 deserialization");

		value =
			static_cast<int64_t>(m_current[0])       |
			static_cast<int64_t>(m_current[1]) <<  8 |
			static_cast<int64_t>(m_current[2]) << 16 |
			static_cast<int64_t>(m_current[3]) << 24 |
			static_cast<int64_t>(m_current[4]) << 32 |
			static_cast<int64_t>(m_current[5]) << 40 |
			static_cast<int64_t>(m_current[6]) << 48 |
			static_cast<int64_t>(m_current[7]) << 56;

		m_current = m_current.subspan(8);
	}

	void String([[maybe_unused]] std::string_view name, std::string& value) override { String(0, value); }
	void String([[maybe_unused]] int name, std::string& value) override
	{
		if (m_current.size() < 4)
			throw std::runtime_error("Insufficient data for String deserialization");

		uint32_t len;
		UInt32(name, len);
		if (len > m_current.size())
			throw std::runtime_error("String length exceeds remaining data");

		value.assign(reinterpret_cast<const char*>(m_current.data()), len);
		m_current = m_current.subspan(len);
	}

	void Bytes([[maybe_unused]] std::string_view name, std::vector<uint8_t>& buffer) override
	{
		uint32_t length = 0;
		UInt32(0, length);

		buffer.resize(length);

		auto toCopy = m_current.subspan(0, length);
		buffer.assign(toCopy.begin(), toCopy.end());
		m_current = m_current.subspan(length);
	}
};
