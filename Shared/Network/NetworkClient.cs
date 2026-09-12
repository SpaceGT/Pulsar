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

    public static async Task<Stream> GetStreamAsync(Uri uri, string accept = null)
    {
        using CancellationTokenSource timeout = ResponseTimeout();
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, uri);

        if (accept is not null)
            request.Headers.Accept.ParseAdd(accept);

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
        return request;
    }

    private static HttpClient CreateClient()
    {
        HttpClientHandler handler = new()
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        HttpMessageHandler redirects = new RedirectHandler(handler);
        return new HttpClient(redirects) { Timeout = Timeout.InfiniteTimeSpan };
    }
}

file sealed class RedirectHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    private const int MaxRedirects = 10;
    private const HttpStatusCode PermanentRedirect = (HttpStatusCode)308; // Missing on net48

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        ApplyAuthorization(request);
        Task<HttpResponseMessage> send = base.SendAsync(request, cancellationToken);
        HttpResponseMessage response = await send.ConfigureAwait(false);

        for (int redirects = 0; redirects < MaxRedirects; redirects++)
        {
            HttpStatusCode status = response.StatusCode;
            Uri location = response.Headers.Location;
            if (!IsRedirect(status) || location is null)
                return response;

            Uri currentUri = request.RequestUri;
            Uri redirectUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);

            bool isHttps = redirectUri.Scheme == Uri.UriSchemeHttps;
            bool unsupported = !isHttps && redirectUri.Scheme != Uri.UriSchemeHttp;
            bool isDowngrade = currentUri.Scheme == Uri.UriSchemeHttps && !isHttps;

            if (unsupported || isDowngrade)
                return response;

            response.Dispose();
            request.RequestUri = redirectUri;
            ApplyAuthorization(request);

            bool keepsPost = status is HttpStatusCode.TemporaryRedirect or PermanentRedirect;
            if (request.Method == HttpMethod.Post && !keepsPost)
            {
                request.Method = HttpMethod.Get;
                request.Content = null;
                request.Headers.TransferEncodingChunked = false;
            }

            send = base.SendAsync(request, cancellationToken);
            response = await send.ConfigureAwait(false);
        }

        return response;
    }

    private static void ApplyAuthorization(HttpRequestMessage request)
    {
        request.Headers.Authorization = null;
        if (!string.IsNullOrWhiteSpace(GitHub.Token) && GitHub.IsTokenHost(request.RequestUri))
            request.Headers.Authorization = new("Bearer", GitHub.Token);
    }

    private static bool IsRedirect(HttpStatusCode status)
    {
        return status
            is >= HttpStatusCode.MultipleChoices
                and <= HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect
                or PermanentRedirect;
    }
}
