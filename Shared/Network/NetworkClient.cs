using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.Shared.Config;

namespace Pulsar.Shared.Network;

internal static class NetworkClient
{
    private const int CopyBufferSize = 80 * 1024;
    private static readonly HttpClient Client = CreateClient();

    private static CancellationTokenSource ResponseTimeout() =>
        new(TimeSpan.FromMilliseconds(ConfigManager.Instance.Core.NetworkTimeout));

    private static CancellationTokenSource DownloadTimeout() =>
        new(TimeSpan.FromMilliseconds(ConfigManager.Instance.Core.DownloadTimeout));

    public static async Task<string> GetStringAsync(Uri uri)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, uri);
        return await SendStringRequestAsync(request).ConfigureAwait(false);
    }

    public static async Task<string> PostStringAsync(Uri uri, HttpContent content)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, uri);
        request.Content = content;
        return await SendStringRequestAsync(request).ConfigureAwait(false);
    }

    public static async Task<Stream> GetStreamAsync(Uri uri)
    {
        using CancellationTokenSource timeout = ResponseTimeout();
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, uri);
        using HttpResponseMessage response = await Client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        MemoryStream output = new();
        await CopyDownloadAsync(response, output).ConfigureAwait(false);
        output.Position = 0;

        return output;
    }

    public static async Task DownloadAsync(Uri uri, string destination)
    {
        using CancellationTokenSource timeout = ResponseTimeout();
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, uri);
        using HttpResponseMessage response = await Client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using FileStream output = File.Create(destination);
        await CopyDownloadAsync(response, output).ConfigureAwait(false);
    }

    private static async Task CopyDownloadAsync(HttpResponseMessage response, Stream output)
    {
        using Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using CancellationTokenSource timeout = DownloadTimeout();
        await input.CopyToAsync(output, CopyBufferSize, timeout.Token).ConfigureAwait(false);
    }

    private static async Task<string> SendStringRequestAsync(HttpRequestMessage request)
    {
        using CancellationTokenSource timeout = ResponseTimeout();
        using HttpResponseMessage response = await Client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        using MemoryStream output = new();
        await CopyDownloadAsync(response, output).ConfigureAwait(false);
        output.Position = 0;

        using StreamReader reader = new(output);
        return reader.ReadToEnd();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
    {
        HttpRequestMessage request = new(method, uri);
        request.Headers.UserAgent.ParseAdd(ConfigManager.Instance.Core.UserAgent);

        // Note HttpClient drops the Authorization header on redirects
        if (!string.IsNullOrWhiteSpace(GitHub.Token) && GitHub.IsTokenHost(uri))
            request.Headers.Authorization = new("Bearer", GitHub.Token);

        return request;
    }

    private static HttpClient CreateClient()
    {
        HttpClientHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }
}
