using System.Text;
using Newtonsoft.Json;

namespace Pulsar.Protocol;

public class IpcMessage
{
    public int Type { get; set; }

    public byte[] Data { get; set; }

    public static IpcMessage Create<T>(int type, T value) =>
        new()
        {
            Type = type,
            Data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value)),
        };

    public T GetData<T>() =>
        JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(Data ?? []));
}
