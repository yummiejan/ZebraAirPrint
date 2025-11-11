using System.Text;
using ZebraAirPrintService.Models;

namespace ZebraAirPrintService.Utils;

/// <summary>
/// Parser for IPP (Internet Printing Protocol) requests and responses
/// </summary>
public class IppParser
{
    // IPP delimiter tags
    private const byte TAG_OPERATION_ATTRIBUTES = 0x01;
    private const byte TAG_JOB_ATTRIBUTES = 0x02;
    private const byte TAG_END_OF_ATTRIBUTES = 0x03;
    private const byte TAG_PRINTER_ATTRIBUTES = 0x04;
    private const byte TAG_UNSUPPORTED_ATTRIBUTES = 0x05;

    // IPP value tags
    private const byte TAG_INTEGER = 0x21;
    private const byte TAG_BOOLEAN = 0x22;
    private const byte TAG_ENUM = 0x23;
    private const byte TAG_STRING = 0x30;
    private const byte TAG_TEXT = 0x41;
    private const byte TAG_NAME = 0x42;
    private const byte TAG_KEYWORD = 0x44;
    private const byte TAG_URI = 0x45;
    private const byte TAG_CHARSET = 0x47;
    private const byte TAG_LANGUAGE = 0x48;
    private const byte TAG_MIMETYPE = 0x49;

    /// <summary>
    /// Parses an IPP request from a stream
    /// </summary>
    public IppRequest ParseRequest(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var request = new IppRequest();

        // Read IPP header (8 bytes)
        request.Version = (reader.ReadByte(), reader.ReadByte());
        request.Operation = (IppOperation)ReadInt16(reader);
        request.RequestId = ReadInt32(reader);

        // Read attributes
        byte currentGroup = 0;
        Dictionary<string, object>? currentAttributes = null;

        while (stream.Position < stream.Length)
        {
            byte tag = reader.ReadByte();

            // Check for delimiter tags
            if (tag == TAG_END_OF_ATTRIBUTES)
            {
                // Rest of the stream is document data
                if (stream.Position < stream.Length)
                {
                    var dataLength = stream.Length - stream.Position;
                    request.DocumentData = reader.ReadBytes((int)dataLength);
                }
                break;
            }

            if (tag == TAG_OPERATION_ATTRIBUTES || tag == TAG_JOB_ATTRIBUTES ||
                tag == TAG_PRINTER_ATTRIBUTES || tag == TAG_UNSUPPORTED_ATTRIBUTES)
            {
                currentGroup = tag;
                string groupName = tag switch
                {
                    TAG_OPERATION_ATTRIBUTES => "operation",
                    TAG_JOB_ATTRIBUTES => "job",
                    TAG_PRINTER_ATTRIBUTES => "printer",
                    TAG_UNSUPPORTED_ATTRIBUTES => "unsupported",
                    _ => "unknown"
                };
                currentAttributes = new Dictionary<string, object>();
                request.Attributes[groupName] = currentAttributes;
                continue;
            }

            // Read attribute
            if (currentAttributes != null)
            {
                var (name, value) = ReadAttribute(reader, tag);
                if (!string.IsNullOrEmpty(name))
                {
                    currentAttributes[name] = value;

                    // Extract commonly used attributes
                    if (name == "document-format")
                        request.ContentType = value.ToString();
                    else if (name == "job-name")
                        request.DocumentName = value.ToString();
                }
            }
        }

        return request;
    }

    /// <summary>
    /// Reads a single IPP attribute
    /// </summary>
    private (string Name, object Value) ReadAttribute(BinaryReader reader, byte valueTag)
    {
        // Read name length and name
        short nameLength = ReadInt16(reader);
        string name = nameLength > 0 ? Encoding.UTF8.GetString(reader.ReadBytes(nameLength)) : string.Empty;

        // Read value length and value
        short valueLength = ReadInt16(reader);
        object value;

        switch (valueTag)
        {
            case TAG_INTEGER:
            case TAG_ENUM:
                value = ReadInt32(reader);
                break;

            case TAG_BOOLEAN:
                value = reader.ReadByte() != 0;
                break;

            case TAG_STRING:
            case TAG_TEXT:
            case TAG_NAME:
            case TAG_KEYWORD:
            case TAG_URI:
            case TAG_CHARSET:
            case TAG_LANGUAGE:
            case TAG_MIMETYPE:
                value = Encoding.UTF8.GetString(reader.ReadBytes(valueLength));
                break;

            default:
                // Unknown tag, read as byte array
                value = reader.ReadBytes(valueLength);
                break;
        }

        return (name, value);
    }

    /// <summary>
    /// Creates a successful IPP response
    /// </summary>
    public byte[] CreateSuccessResponse(int requestId, int jobId, Dictionary<string, object>? attributes = null)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Write IPP header
        writer.Write((byte)1); // Major version
        writer.Write((byte)1); // Minor version
        WriteInt16(writer, (short)IppStatusCode.SuccessfulOk);
        WriteInt32(writer, requestId);

        // Write operation attributes
        writer.Write(TAG_OPERATION_ATTRIBUTES);

        // Write charset
        WriteAttribute(writer, TAG_CHARSET, "attributes-charset", "utf-8");
        WriteAttribute(writer, TAG_LANGUAGE, "attributes-natural-language", "en-us");

        // Write job attributes if this is a Print-Job response
        if (jobId > 0)
        {
            writer.Write(TAG_JOB_ATTRIBUTES);
            WriteAttribute(writer, TAG_INTEGER, "job-id", jobId);
            WriteAttribute(writer, TAG_URI, "job-uri", $"ipp://localhost:631/jobs/{jobId}");
            WriteAttribute(writer, TAG_ENUM, "job-state", 3); // pending
        }

        // Write additional attributes if provided
        if (attributes != null && attributes.Count > 0)
        {
            foreach (var attr in attributes)
            {
                WriteAttributeAuto(writer, attr.Key, attr.Value);
            }
        }

        // Write end of attributes
        writer.Write(TAG_END_OF_ATTRIBUTES);

        return ms.ToArray();
    }

    /// <summary>
    /// Creates an error IPP response
    /// </summary>
    public byte[] CreateErrorResponse(int requestId, IppStatusCode statusCode, string errorMessage)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Write IPP header
        writer.Write((byte)1); // Major version
        writer.Write((byte)1); // Minor version
        WriteInt16(writer, (short)statusCode);
        WriteInt32(writer, requestId);

        // Write operation attributes
        writer.Write(TAG_OPERATION_ATTRIBUTES);

        WriteAttribute(writer, TAG_CHARSET, "attributes-charset", "utf-8");
        WriteAttribute(writer, TAG_LANGUAGE, "attributes-natural-language", "en-us");
        WriteAttribute(writer, TAG_TEXT, "status-message", errorMessage);

        // Write end of attributes
        writer.Write(TAG_END_OF_ATTRIBUTES);

        return ms.ToArray();
    }

    /// <summary>
    /// Creates a Get-Printer-Attributes response
    /// </summary>
    public byte[] CreateGetPrinterAttributesResponse(int requestId, Dictionary<string, object> printerAttributes)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Write IPP header
        writer.Write((byte)1); // Major version
        writer.Write((byte)1); // Minor version
        WriteInt16(writer, (short)IppStatusCode.SuccessfulOk);
        WriteInt32(writer, requestId);

        // Write operation attributes
        writer.Write(TAG_OPERATION_ATTRIBUTES);
        WriteAttribute(writer, TAG_CHARSET, "attributes-charset", "utf-8");
        WriteAttribute(writer, TAG_LANGUAGE, "attributes-natural-language", "en-us");

        // Write printer attributes
        writer.Write(TAG_PRINTER_ATTRIBUTES);
        foreach (var attr in printerAttributes)
        {
            WriteAttributeAuto(writer, attr.Key, attr.Value);
        }

        // Write end of attributes
        writer.Write(TAG_END_OF_ATTRIBUTES);

        return ms.ToArray();
    }

    /// <summary>
    /// Writes an attribute with automatic type detection
    /// </summary>
    private void WriteAttributeAuto(BinaryWriter writer, string name, object value)
    {
        switch (value)
        {
            case int intValue:
                WriteAttribute(writer, TAG_INTEGER, name, intValue);
                break;
            case bool boolValue:
                WriteAttribute(writer, TAG_BOOLEAN, name, boolValue);
                break;
            case string strValue:
                WriteAttribute(writer, TAG_TEXT, name, strValue);
                break;
            case string[] strArray:
                foreach (var str in strArray)
                {
                    WriteAttribute(writer, TAG_TEXT, name, str);
                }
                break;
            default:
                WriteAttribute(writer, TAG_TEXT, name, value.ToString() ?? string.Empty);
                break;
        }
    }

    /// <summary>
    /// Writes a single attribute
    /// </summary>
    private void WriteAttribute(BinaryWriter writer, byte valueTag, string name, object value)
    {
        writer.Write(valueTag);

        // Write name
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        WriteInt16(writer, (short)nameBytes.Length);
        writer.Write(nameBytes);

        // Write value
        switch (valueTag)
        {
            case TAG_INTEGER:
            case TAG_ENUM:
                WriteInt16(writer, 4);
                WriteInt32(writer, Convert.ToInt32(value));
                break;

            case TAG_BOOLEAN:
                WriteInt16(writer, 1);
                writer.Write((byte)(Convert.ToBoolean(value) ? 1 : 0));
                break;

            default:
                byte[] valueBytes = Encoding.UTF8.GetBytes(value.ToString() ?? string.Empty);
                WriteInt16(writer, (short)valueBytes.Length);
                writer.Write(valueBytes);
                break;
        }
    }

    // Helper methods for reading/writing network byte order (big-endian)

    private short ReadInt16(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt16(bytes, 0);
    }

    private int ReadInt32(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    private void WriteInt16(BinaryWriter writer, short value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        writer.Write(bytes);
    }

    private void WriteInt32(BinaryWriter writer, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        writer.Write(bytes);
    }
}
