using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace McmLib.Services
{
    public static class HttpApiClients
    {
        private static readonly Uri _baseUri = new Uri("http://php84.localhost/mcmapi/api/", UriKind.Absolute);
        private static readonly HttpClient _httpClient = CreateHttpClient();
        private static Func<string?>? _tokenProvider;
        private static Func<string?>? _apiKeyProvider;
        private const long SlowRequestThresholdMilliseconds = 1000;
        
                    
        private static readonly HttpStatusCode[] _retryableStatusCodes =
        {
            HttpStatusCode.RequestTimeout,      // 408
            (HttpStatusCode)429,                // Too Many Requests
            HttpStatusCode.InternalServerError, // 500
            HttpStatusCode.BadGateway,          // 502
            HttpStatusCode.ServiceUnavailable,  // 503
            HttpStatusCode.GatewayTimeout       // 504
        };

        private static readonly TimeSpan[] _retryDelays =
        {
            TimeSpan.FromMilliseconds(400),
            TimeSpan.FromMilliseconds(900)
        };

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        private static HttpClient CreateHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                MaxConnectionsPerServer = 20,
                UseCookies = false
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public static async Task<JsonDocument?> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            return await SendJsonAsync(HttpMethod.Get, url, data: null, cancellationToken);
        }

        public static async Task<JsonDocument?> DeleteAsync(string url, CancellationToken cancellationToken = default)
        {
            return await SendJsonAsync(HttpMethod.Delete, url, data: null, cancellationToken);
        }

        public static async Task<JsonDocument?> PostAsync(string url, object data, CancellationToken cancellationToken = default)
        {
            return await SendJsonAsync(HttpMethod.Post, url, data, cancellationToken);
        }

        public static async Task<JsonDocument?> PutAsync(string url, object data, CancellationToken cancellationToken = default)
        {
            return await SendJsonAsync(HttpMethod.Put, url, data, cancellationToken);
        }

        // Generic POST helper for typed response models.
        public static async Task<TResponse?> PostAsync<TRequest, TResponse>(
            string url,
            TRequest data,
            CancellationToken cancellationToken = default)
        {
            var doc = await PostAsync(url, (object?)data ?? new { }, cancellationToken);
            if (doc == null)
            {
                return default;
            }
            // 1) Try full payload first (works for CompaniesApiResponse with [JsonPropertyName("data")])
            try
            {
                var response = JsonSerializer.Deserialize<TResponse>(doc.RootElement.GetRawText(), _jsonOptions);
                if (response is not null)
                {
                    return response;
                }
            }
            catch
            {
                // continue to fallback
            }

            // 2) Fallback: only deserialize nested "data"
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("data", out var dataElement))
            {
                try
                {
                    return JsonSerializer.Deserialize<TResponse>(dataElement.GetRawText(), _jsonOptions);
                }
                catch
                {
                    return default;
                }
            }

            return default;
        }

        public static void ConfigureAuthentication(Func<string?> tokenProvider)
        {
            _tokenProvider = tokenProvider;
        }
        public static void ConfigureAPI_Key(Func<string?> apiKeyProvider)
        {
            _apiKeyProvider = apiKeyProvider;
        }
        private static async Task<JsonDocument?> SendJsonAsync(
            HttpMethod method,
            string url,
            object? data,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage? response = null;

            try
            {
                var requestUri = new Uri(_baseUri, url);

                if (!IsAllowedTransport(requestUri))
                {
                    return CreateErrorResponse("Insecure transport blocked: use HTTPS", endpoint: url);
                }

                for (var attempt = 0; attempt <= _retryDelays.Length; attempt++)
                {
                    using var request = BuildRequest(method, requestUri, data);

                    try
                    {
                        response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    }
                    catch (Exception ex) when (IsRetryableException(ex, cancellationToken) && attempt < _retryDelays.Length)
                    {
                        await Task.Delay(_retryDelays[attempt], cancellationToken);
                        continue;
                    }

                    if (attempt < _retryDelays.Length && IsRetryableStatusCode(response.StatusCode))
                    {
                        response.Dispose();
                        await Task.Delay(_retryDelays[attempt], cancellationToken);
                        continue;
                    }

                    break;
                }

                if (response == null)
                {
                    return CreateErrorResponse("No response from API", endpoint: url, elapsedMilliseconds: stopwatch.ElapsedMilliseconds);
                }

                using (response)
                {
                    var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(jsonString))
                    {
                        jsonString = CreateErrorResponse("Empty response", statusCode: response.StatusCode, endpoint: url, elapsedMilliseconds: stopwatch.ElapsedMilliseconds).RootElement.GetRawText();
                    }

                    LogRequest(method, requestUri, response.StatusCode, stopwatch.ElapsedMilliseconds);

                    if (LooksLikeJson(jsonString))
                    {
                        return JsonDocument.Parse(jsonString);
                    }

                    var snippet = jsonString.Length > 180 ? jsonString.Substring(0, 180) : jsonString;
                    return CreateErrorResponse(
                        "Non-JSON response from API",
                        statusCode: response.StatusCode,
                        endpoint: url,
                        elapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                        contentType: contentType,
                        bodyPrefix: snippet);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                LogRequest(method, new Uri(_baseUri, url), null, stopwatch.ElapsedMilliseconds, cancelled: true);
                return CreateErrorResponse("Request cancelled", endpoint: url, elapsedMilliseconds: stopwatch.ElapsedMilliseconds, isCanceled: true);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse($"Connection Error: {ex.Message}", endpoint: url, elapsedMilliseconds: stopwatch.ElapsedMilliseconds);
            }
        }

        private static HttpRequestMessage BuildRequest(HttpMethod method, Uri requestUri, object? data)
        {
            var request = new HttpRequestMessage(method, requestUri);
            var token = GetCurrentToken();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Remove("X-Token");
                request.Headers.TryAddWithoutValidation("X-Token", token);
            }
            else
            {
                var apiKey = GetCurrentApiKey();
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    request.Headers.Remove("X-API-Key");
                    request.Headers.TryAddWithoutValidation("X-API-Key", apiKey);
                }
            }
            if (data != null && (method == HttpMethod.Post || method == HttpMethod.Put || method.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase)))
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return request;
        }

        private static string? GetCurrentToken()
        {
            return _tokenProvider?.Invoke();            
        }
        private static string? GetCurrentApiKey()
        {
            return _apiKeyProvider?.Invoke();
        }
        private static void LogRequest(HttpMethod method, Uri requestUri, HttpStatusCode? statusCode, long elapsedMilliseconds, bool cancelled = false)
        {
            if (cancelled)
            {
                Trace.WriteLine($"[HTTP CANCELLED] {method.Method} {FormatEndpoint(requestUri)} | Elapsed={elapsedMilliseconds} ms");
                return;
            }

            if (elapsedMilliseconds < SlowRequestThresholdMilliseconds)
            {
                return;
            }

            var statusText = statusCode.HasValue ? ((int)statusCode.Value).ToString() : "N/A";
            Trace.WriteLine(
                $"[HTTP SLOW] {method.Method} {FormatEndpoint(requestUri)} | Status={statusText} | Elapsed={elapsedMilliseconds} ms | Threshold={SlowRequestThresholdMilliseconds} ms");
        }

        private static string FormatEndpoint(Uri requestUri)
        {
            return _baseUri.MakeRelativeUri(requestUri).ToString();
        }

        private static JsonDocument CreateErrorResponse(
            string message,
            HttpStatusCode? statusCode = null,
            string? endpoint = null,
            long? elapsedMilliseconds = null,
            string? contentType = null,
            string? bodyPrefix = null,
            bool isCanceled = false)
        {
            var payload = new Dictionary<string, object?>
            {
                ["result"] = false,
                ["message"] = message,
                ["endpoint"] = endpoint,
                ["status"] = statusCode.HasValue ? (int)statusCode.Value : null,
                ["elapsedMs"] = elapsedMilliseconds,
                ["isCanceled"] = isCanceled
            };

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                payload["contentType"] = contentType;
            }

            if (!string.IsNullOrWhiteSpace(bodyPrefix))
            {
                payload["bodyPrefix"] = bodyPrefix;
            }

            return JsonDocument.Parse(JsonSerializer.Serialize(payload, _jsonOptions));
        }

        private static bool LooksLikeJson(string text)
        {
            var t = text.TrimStart();
            return t.StartsWith("{", StringComparison.Ordinal) || t.StartsWith("[", StringComparison.Ordinal);
        }

        private static bool IsAllowedTransport(Uri uri)
        {
            if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Dev-only allowance for local testing environments.
            if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                var host = uri.Host ?? string.Empty;
                if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string EscapeJson(string value)
        {
            value ??= string.Empty;
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
        }

        private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
        {
            foreach (var code in _retryableStatusCodes)
            {
                if (statusCode == code)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRetryableException(Exception ex, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            return ex is HttpRequestException || ex is TaskCanceledException;
        }
    }
}
