using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace Pulsar.Shared.Network
{
    public static class SimpleHttpClient
    {
        // REST API request timeout in milliseconds
        private const int TimeoutMs = 3000;

        public static TV Get<TV>(string url)
            where TV : class, new()
        {
            try
            {
                using var response = (HttpWebResponse)
                    CreateRequest(HttpMethod.Get, url).GetResponse();

                using var responseStream = response.GetResponseStream();
                if (responseStream == null)
                    return null;

                using var streamReader = new StreamReader(responseStream, Encoding.UTF8);
                return JsonConvert.DeserializeObject<TV>(streamReader.ReadToEnd());
            }
            catch (WebException e)
            {
                LogFile.Error($"REST API request failed: GET {url} [{e.Message}]");
                return null;
            }
        }

        public static TV Get<TV>(string url, Dictionary<string, string> parameters)
            where TV : class, new()
        {
            var uriBuilder = new StringBuilder(url);
            AppendQueryParameters(uriBuilder, parameters);
            var uri = uriBuilder.ToString();

            try
            {
                using var response = (HttpWebResponse)
                    CreateRequest(HttpMethod.Get, uri).GetResponse();

                using var responseStream = response.GetResponseStream();
                if (responseStream == null)
                    return null;

                using var streamReader = new StreamReader(responseStream, Encoding.UTF8);
                return JsonConvert.DeserializeObject<TV>(streamReader.ReadToEnd());
            }
            catch (WebException e)
            {
                LogFile.Error($"REST API request failed: GET {uri} [{e.Message}]");
                return null;
            }
        }

        public static TV Post<TV>(string url)
            where TV : class, new()
        {
            try
            {
                var request = CreateRequest(HttpMethod.Post, url);
                request.ContentLength = 0L;
                return PostRequest<TV>(request);
            }
            catch (WebException e)
            {
                LogFile.Error($"REST API request failed: POST {url} [{e.Message}]");
                return null;
            }
        }

        public static TV Post<TV>(string url, Dictionary<string, string> parameters)
            where TV : class, new()
        {
            var uriBuilder = new StringBuilder(url);
            AppendQueryParameters(uriBuilder, parameters);
            var uri = uriBuilder.ToString();

            try
            {
                var request = CreateRequest(HttpMethod.Post, uri);
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = 0;
                return PostRequest<TV>(request);
            }
            catch (WebException e)
            {
                LogFile.Error($"REST API request failed: POST {uri} [{e.Message}]");
                return null;
            }
        }

        public static TV Post<TV, TR>(string url, TR body)
            where TR : class, new()
            where TV : class, new()
        {
            try
            {
                var request = CreateRequest(HttpMethod.Post, url);
                var requestJson = JsonConvert.SerializeObject(body);
                var requestBytes = Encoding.UTF8.GetBytes(requestJson);
                request.ContentType = "application/json";
                request.ContentLength = requestBytes.Length;
                return PostRequest<TV>(request, requestBytes);
            }
            catch (WebException e)
            {
                LogFile.Error($"REST API request failed: POST {url} [{e.Message}]");
                return null;
            }
        }

        public static bool Post<TR>(string url, TR body)
            where TR : class, new()
        {
            try
            {
                var request = CreateRequest(HttpMethod.Post, url);
                var requestJson = JsonConvert.SerializeObject(body);
                var requestBytes = Encoding.UTF8.GetBytes(requestJson);
                request.ContentType = "application/json";
                request.ContentLength = requestBytes.Length;
                return PostRequest(request, requestBytes);
            }
            catch (WebException e)
            {
                LogFile.Error($"REST API request failed: POST {url} [{e.Message}]");
                return false;
            }
        }

        private static TV PostRequest<TV>(HttpWebRequest request, byte[] body = null)
            where TV : class, new()
        {
            if (body != null)
            {
                using var requestStream = request.GetRequestStream();
                requestStream.Write(body, 0, body.Length);
                requestStream.Close();
            }

            using var response = (HttpWebResponse)request.GetResponse();
            using var responseStream = response.GetResponseStream();
            if (responseStream == null)
                return null;

            using var streamReader = new StreamReader(responseStream, Encoding.UTF8);
            var data = JsonConvert.DeserializeObject<TV>(streamReader.ReadToEnd());
            return data;
        }

        private static bool PostRequest(HttpWebRequest request, byte[] body = null)
        {
            if (body != null)
            {
                using var requestStream = request.GetRequestStream();
                requestStream.Write(body, 0, body.Length);
                requestStream.Close();
            }

            using var response = (HttpWebResponse)request.GetResponse();

            return response.StatusCode == HttpStatusCode.OK;
        }

        private static HttpWebRequest CreateRequest(HttpMethod method, string url)
        {
            var http = WebRequest.CreateHttp(url);
            http.Method = method.ToString().ToUpper();
            http.Timeout = TimeoutMs;
            return http;
        }

        private static void AppendQueryParameters(
            StringBuilder stringBuilder,
            Dictionary<string, string> parameters
        )
        {
            if (parameters == null || parameters.Count == 0)
                return;

            var first = true;
            foreach (var p in parameters)
            {
                stringBuilder.Append(first ? '?' : '&');
                first = false;
                stringBuilder.Append(Uri.EscapeDataString(p.Key));
                stringBuilder.Append('=');
                stringBuilder.Append(Uri.EscapeDataString(p.Value));
            }
        }
    }
}
