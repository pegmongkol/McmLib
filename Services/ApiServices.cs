using McmLib.Models;
using System.Text.Json;

namespace McmLib.Services
{
    public static class ApiServices
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static Task<JsonDocument?> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            return HttpApiClients.GetAsync(url, cancellationToken);
        }

        public static Task<JsonDocument?> PostAsync(string url, object data, CancellationToken cancellationToken = default)
        {
            return HttpApiClients.PostAsync(url, data, cancellationToken);            
        }

        public static Task<TResponse?> PostAsync<TRequest, TResponse>(
            string url,
            TRequest data,
            CancellationToken cancellationToken = default)
        {
            return HttpApiClients.PostAsync<TRequest, TResponse>(url, data, cancellationToken);
        }

        public static async Task<TResponse> PostResponseAsync<TRequest, TResponse>(
            string url,
            TRequest data,
            string fallbackMessage,
            CancellationToken cancellationToken = default)
            where TResponse : ApiResponse, new()
        {
            var response = await HttpApiClients.PostAsync<TRequest, TResponse>(url, data, cancellationToken);
            return EnsureResponse(response, fallbackMessage);
        }

        public static Task<JsonDocument?> PutAsync(string url, object data, CancellationToken cancellationToken = default)
        {
            return HttpApiClients.PutAsync(url, data, cancellationToken);
        }

        public static Task<JsonDocument?> DeleteAsync(string url, CancellationToken cancellationToken = default)
        {
            return HttpApiClients.DeleteAsync(url, cancellationToken);
        }

        public static bool IsSuccess(JsonDocument? document)
        {
            if (document == null)
            {
                return false;
            }

            return TryGetBoolean(document, "result") == true;
        }

        public static bool IsCanceled(JsonDocument? document)
        {
            if (document == null)
            {
                return false;
            }

            return TryGetBoolean(document, "isCanceled") == true;
        }

        public static string GetMessage(JsonDocument? document, string fallbackMessage = "Operation failed")
        {
            if (document == null)
            {
                return fallbackMessage;
            }

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return fallbackMessage;
            }

            if (root.TryGetProperty("message", out var messageProperty) && messageProperty.ValueKind == JsonValueKind.String)
            {
                var message = messageProperty.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message;
                }
            }

            return fallbackMessage;
        }

        public static string? GetString(JsonDocument? document, string propertyName)
        {
            if (document == null)
            {
                return null;
            }

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }

            return null;
        }

        public static int? GetInt32(JsonDocument? document, string propertyName)
        {
            if (document == null)
            {
                return null;
            }

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value))
            {
                return value;
            }

            return null;
        }

        public static bool? TryGetBoolean(JsonDocument? document, string propertyName)
        {
            if (document == null)
            {
                return null;
            }

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!root.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        public static TResponse EnsureResponse<TResponse>(TResponse? response, string fallbackMessage)
            where TResponse : ApiResponse, new()
        {
            if (response != null)
            {
                if (!response.Result && !response.IsCanceled && string.IsNullOrWhiteSpace(response.Message))
                {
                    response.Message = fallbackMessage;
                }

                return response;
            }

            return CreateErrorResponse<TResponse>(fallbackMessage);
        }

        public static TResponse CreateErrorResponse<TResponse>(string message)
            where TResponse : ApiResponse, new()
        {
            return new TResponse
            {
                Result = false,
                Message = message
            };
        }

        public static TResponse DeserializeResponse<TResponse>(JsonDocument? document, string fallbackMessage)
            where TResponse : ApiResponse, new()
        {
            if (document == null)
            {
                return CreateErrorResponse<TResponse>(fallbackMessage);
            }

            try
            {
                var response = JsonSerializer.Deserialize<TResponse>(document.RootElement.GetRawText(), _jsonOptions);
                return EnsureResponse(response, fallbackMessage);
            }
            catch
            {
                return CreateErrorResponse<TResponse>(fallbackMessage);
            }
        }
    }
}
