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
    private const int MaxRedirects = 10;
    private const HttpStatusCode PermanentRedirect = (HttpStatusCode)308; // Missing on net48
    private static readonly HttpClient Client = CreateClient();

    private static CancellationTokenSource ResponseTimeout() =>
        new(TimeSpan.FromMilliseconds(ConfigManager.Instance.Core.NetworkTimeout));

    private static CancellationTokenSource DownloadTimeout() =>
        new(TimeSpan.FromMilliseconds(ConfigManager.Instance.Core.DownloadTimeout));

    public static async Task<string> GetStringAsync(Uri uri)
    {
        return await SendStringRequestAsync(HttpMethod.Get, uri, null).ConfigureAwait(false);
    }

    public static async Task<string> PostStringAsync(Uri uri, HttpContent content)
    {
        return await SendStringRequestAsync(HttpMethod.Post, uri, content).ConfigureAwait(false);
    }

    public static async Task<Stream> GetStreamAsync(Uri uri, string accept = null)
    {
        using CancellationTokenSource timeout = ResponseTimeout();
        using HttpResponseMessage response = await SendAsync(
                HttpMethod.Get,
                uri,
                accept,
                null,
                timeout.Token
            )
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
        using HttpResponseMessage response = await SendAsync(
                HttpMethod.Get,
                uri,
                null,
                null,
                timeout.Token
            )
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

    private static async Task<string> SendStringRequestAsync(
        HttpMethod method,
        Uri uri,
        HttpContent content
    )
    {
        using CancellationTokenSource timeout = ResponseTimeout();
        using HttpResponseMessage response = await SendAsync(
                method,
                uri,
                null,
                content,
                timeout.Token
            )
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        using MemoryStream output = new();
        await CopyDownloadAsync(response, output).ConfigureAwait(false);
        output.Position = 0;

        using StreamReader reader = new(output);
        return reader.ReadToEnd();
    }

    // Redirects are followed manually because HttpClient drops the Authorization
    // header when the host changes. GitHub serves archives via a redirect from
    // api.github.com to codeload.github.com so the token must be re-applied.
    private static async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Uri uri,
        string accept,
        HttpContent content,
        CancellationToken cancellationToken
    )
    {
        for (int hop = 0; ; hop++)
        {
            HttpResponseMessage response;
            using (HttpRequestMessage request = CreateRequest(method, uri, accept))
            {
                request.Content = content;
                try
                {
                    response = await Client
                        .SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
                finally
                {
                    // The content is owned by the caller and may be re-sent
                    request.Content = null;
                }
            }

            HttpStatusCode status = response.StatusCode;
            Uri location = response.Headers.Location;

            if (!IsRedirect(status) || location is null)
                return response;

            response.Dispose();

            if (hop >= MaxRedirects)
                throw new HttpRequestException(
                    $"Too many redirects ({MaxRedirects}) while requesting {uri}"
                );

            if (!location.IsAbsoluteUri)
                location = new Uri(uri, location);

            if (!location.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new HttpRequestException($"Refusing insecure redirect to {location}");

            // Per RFC 9110 only 307/308 preserve the method and body
            if (status != HttpStatusCode.TemporaryRedirect && status != PermanentRedirect)
            {
                method = HttpMethod.Get;
                content = null;
            }

            uri = location;
        }
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status
            is HttpStatusCode.MovedPermanently
                or HttpStatusCode.Found
                or HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect
                or PermanentRedirect;

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string accept)
    {
        HttpRequestMessage request = new(method, uri);
        request.Headers.UserAgent.ParseAdd(ConfigManager.Instance.Core.UserAgent);

        if (accept is not null)
            request.Headers.Accept.ParseAdd(accept);

        if (!string.IsNullOrWhiteSpace(GitHub.Token) && GitHub.IsTokenHost(uri))
            request.Headers.Authorization = new("Bearer", GitHub.Token);

        return request;
    }

    private static HttpClient CreateClient()
    {
        HttpClientHandler handler = new()
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }
}
