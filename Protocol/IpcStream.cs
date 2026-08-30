using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Pulsar.Protocol;

public class IpcStream(Stream input, Stream output)
{
    private const int MaxMessageSize = 256 * 1024 * 1024;
    private const int HeaderSize = sizeof(int);

    private readonly object writeLock = new();

    public T Read<T>()
    {
        if (!TryRead(out T value))
            throw new EndOfStreamException("The IPC stream was closed.");

        return value;
    }

    public bool TryRead<T>(out T value)
    {
        value = default;

        int firstByte = input.ReadByte();
        if (firstByte < 0)
            return false;

        byte[] header = new byte[HeaderSize];
        header[0] = (byte)firstByte;
        ReadExact(input, header, 1, HeaderSize - 1);

        if (!BitConverter.IsLittleEndian)
            Array.Reverse(header);

        int length = BitConverter.ToInt32(header, 0);
        if (length <= 0 || length > MaxMessageSize)
            throw new InvalidDataException($"Invalid IPC message length: {length}");

        byte[] data = new byte[length];
        ReadExact(input, data, 0, data.Length);
        value = JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(data));

        return true;
    }

    public void Write<T>(T value)
    {
        byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value));
        if (data.Length > MaxMessageSize)
            throw new InvalidDataException($"Invalid IPC message length: {data.Length}");

        byte[] header = BitConverter.GetBytes(data.Length);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(header);

        lock (writeLock)
        {
            output.Write(header, 0, header.Length);
            output.Write(data, 0, data.Length);
            output.Flush();
        }
    }

    private static void ReadExact(Stream stream, byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            int read = stream.Read(buffer, offset, count);
            if (read <= 0)
                throw new EndOfStreamException("The IPC message ended early.");

            offset += read;
            count -= read;
        }
    }
}
