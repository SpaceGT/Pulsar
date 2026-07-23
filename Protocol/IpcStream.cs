using System;
using System.IO;

namespace Pulsar.Protocol;

public class IpcStream(Stream input, Stream output)
{
    private const int MaxMessageSize = 256 * 1024 * 1024;
    private const int HeaderSize = sizeof(int);

    private readonly object writeLock = new();

    public IpcMessage Read()
    {
        if (!TryRead(out IpcMessage message))
            throw new EndOfStreamException("The IPC stream was closed.");

        return message;
    }

    public bool TryRead(out IpcMessage message)
    {
        message = null;

        int firstByte = input.ReadByte();
        if (firstByte < 0)
            return false;

        byte[] header = new byte[HeaderSize];
        header[0] = (byte)firstByte;
        ReadExact(input, header, 1, HeaderSize - 1);

        if (!BitConverter.IsLittleEndian)
            Array.Reverse(header);

        int length = BitConverter.ToInt32(header, 0);
        if (length <= HeaderSize || length > MaxMessageSize)
            throw new InvalidDataException($"Invalid IPC message length: {length}");

        byte[] data = new byte[length];
        ReadExact(input, data, 0, data.Length);

        byte[] type = new byte[HeaderSize];
        Buffer.BlockCopy(data, 0, type, 0, HeaderSize);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(type);

        message = new IpcMessage
        {
            Type = BitConverter.ToInt32(type, 0),
            Data = new byte[length - HeaderSize],
        };
        Buffer.BlockCopy(data, HeaderSize, message.Data, 0, message.Data.Length);

        return true;
    }

    public void Write<T>(int type, T value) => Write(IpcMessage.Create(type, value));

    public void Write(IpcMessage message)
    {
        byte[] data = message.Data ?? [];
        int messageLength = HeaderSize + data.Length;
        if (messageLength > MaxMessageSize)
            throw new InvalidDataException($"Invalid IPC message length: {messageLength}");

        byte[] header = BitConverter.GetBytes(messageLength);
        byte[] type = BitConverter.GetBytes(message.Type);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(header);
            Array.Reverse(type);
        }

        lock (writeLock)
        {
            output.Write(header, 0, header.Length);
            output.Write(type, 0, type.Length);
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
