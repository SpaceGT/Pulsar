using System;
using System.IO;
using System.Net;

namespace Pulsar.Updater;

internal static class Network
{
    public static Stream GetStream(Uri uri, int timeout = 10000, bool allowIPv6 = true)
    {
        HttpWebRequest request = WebRequest.CreateHttp(uri);
        request.UserAgent = "Pulsar";
        request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        request.Timeout = timeout;

        if (!allowIPv6)
            request.ServicePoint.BindIPEndPointDelegate = BlockIPv6;

        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
        MemoryStream output = new();
        using (Stream responseStream = response.GetResponseStream())
            responseStream.CopyTo(output);
        output.Position = 0;
        return output;
    }

    private static IPEndPoint BlockIPv6(
        ServicePoint servicePoint,
        IPEndPoint remoteEndPoint,
        int retryCount
    )
    {
        if (remoteEndPoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            return new IPEndPoint(IPAddress.Any, 0);

        throw new InvalidOperationException("No IPv4 address");
    }
}
