using System;
using McmLib.Models;
using System.Text.Json;
namespace McmLib.Services
{
    public class UsersService
    {
        public static async Task<JsonDocument> LoginAsync(string? username, string? password, CancellationToken cancellationToken = default)
        {         
            try
			{
                var url = "users/login.php";
                var payload = new
                {
                    username = username,
                    password = password
                };

                var doc = await ApiServices.PostAsync(url, payload, cancellationToken);
                if (doc == null)
                {
                    return JsonDocument.Parse("{\"result\":false,\"message\":\"No response from server\"}");
                }
                else
                {
                    return doc;
                }
            }
			catch (Exception ex)
			{
                return JsonDocument.Parse($"{{\"result\":false,\"message\":\"Login Error: {EscapeJson(ex.Message)}\"}}");
            }
        }

        public static async Task<JsonDocument> Change_PasswordAsync(string? username, string? oldpassword, string? newpassword, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = "users/change_password.php";
                var payload = new
                {
                    username = username,
                    oldpassword = oldpassword,
                    newpassword = newpassword
                };

                var doc = await ApiServices.PostAsync(url, payload, cancellationToken);
                if (doc == null)
                {
                    return JsonDocument.Parse("{\"result\":false,\"message\":\"No response from server\"}");
                }
                else
                {
                    return NormalizeChangePasswordResponse(doc);
                }
            }
            catch (Exception ex)
            {
                return JsonDocument.Parse($"{{\"result\":false,\"message\":\"Check Password Error: {EscapeJson(ex.Message)}\"}}");
            }
        }

        public static async Task<LoginApiResponse> LoginResponseAsync(string? username, string? password, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = "users/login.php";
                var payload = new
                {
                    username,
                    password
                };

                return await ApiServices.PostResponseAsync<object, LoginApiResponse>(
                    url,
                    payload,
                    "No response from server",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                return ApiServices.CreateErrorResponse<LoginApiResponse>($"Login Error: {ex.Message}");
            }
        }

        public static async Task<ChangePasswordApiResponse> ChangePasswordResponseAsync(string? username, string? oldpassword, string? newpassword, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = "users/change_password.php";
                var payload = new
                {
                    username,
                    oldpassword,
                    newpassword
                };

                var response = await ApiServices.PostResponseAsync<object, ChangePasswordApiResponse>(
                    url,
                    payload,
                    "No response from server",
                    cancellationToken);

                return NormalizeChangePasswordResponse(response);
            }
            catch (Exception ex)
            {
                return ApiServices.CreateErrorResponse<ChangePasswordApiResponse>($"Check Password Error: {ex.Message}");
            }
        }
        public static async Task<CompaniesApiResponse> GetCompaniesResponseAsync(int user_id, string? token = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = "users/get_companies.php";
                var payload = new
                {
                    token = token,
                    user_id = user_id
                };

                var response = await ApiServices.PostResponseAsync<object, CompaniesApiResponse>(
                    url,
                    payload,
                    "No response from server",
                    cancellationToken);

                return NormalizeCompaniesResponse(response);
            }
            catch (Exception ex)
            {
                return ApiServices.CreateErrorResponse<CompaniesApiResponse>($"Get Companies Error: {ex.Message}");
            }
        }

        public static async Task<List<Company>> GetCompaniesAsync(int user_id, string? token, CancellationToken cancellationToken = default)
        {
            var response = await GetCompaniesResponseAsync(user_id, token, cancellationToken);
            return response.Companies;
        }

        public static async Task<BranchesApiResponse> GetBranchesResponseAsync(int user_id, int company_id, string? token = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = "users/get_branchs.php";
                var payload = new
                {
                    token = token,
                    user_id,
                    company_id
                };

                var response = await ApiServices.PostResponseAsync<object, BranchesApiResponse>(
                    url,
                    payload,
                    "No response from server",
                    cancellationToken);

                return NormalizeBranchesResponse(response);
            }
            catch (Exception ex)
            {
                return ApiServices.CreateErrorResponse<BranchesApiResponse>($"Get Branches Error: {ex.Message}");
            }
        }

        public static async Task<List<Branch>> GetBranchesAsync(int user_id, int company_id, string? token = null, CancellationToken cancellationToken = default)
        {
            var response = await GetBranchesResponseAsync(user_id, company_id, token, cancellationToken);
            return response.Branches;
        }

        public static async Task<LoginAlertApiResponse> SendLoginAlertResponseAsync(string? username, int failedAttempts, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = "users/send_login_alert.php";
                var payload = new
                {
                    username,
                    failed_attempts = failedAttempts
                };

                return await ApiServices.PostResponseAsync<object, LoginAlertApiResponse>(
                    url,
                    payload,
                    "Unable to send login alert",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                return ApiServices.CreateErrorResponse<LoginAlertApiResponse>($"Send Login Alert Error: {ex.Message}");
            }
        }
        private static JsonDocument NormalizeChangePasswordResponse(JsonDocument doc)
        {
            var root = doc.RootElement;

            if (root.TryGetProperty("result", out _))
            {
                return doc;
            }

            if (!root.TryGetProperty("change_password", out var changePasswordProperty))
            {
                return doc;
            }

            var isPasswordChanged = changePasswordProperty.ValueKind == JsonValueKind.True;
            var isPasswordChecked = root.TryGetProperty("check_password", out var checkPasswordProperty) &&
                                    checkPasswordProperty.ValueKind == JsonValueKind.True;
            var message = root.TryGetProperty("message", out var messageProperty)
                ? messageProperty.GetString()
                : null;

            var normalized = new
            {
                result = isPasswordChanged,
                message,
                check_password = isPasswordChecked,
                change_password = isPasswordChanged
            };

            return JsonDocument.Parse(JsonSerializer.Serialize(normalized));
        }

        private static ChangePasswordApiResponse NormalizeChangePasswordResponse(ChangePasswordApiResponse response)
        {
            if (response.Result)
            {
                response.Change_Password = true;
                return response;
            }

            if (response.Change_Password)
            {
                response.Result = true;
            }

            return response;
        }

        private static BranchesApiResponse NormalizeBranchesResponse(BranchesApiResponse response)
        {
            response.Branches ??= new List<Branch>();

            if (response.Result || response.IsCanceled)
            {
                return response;
            }

            if (response.Branches.Count >= 0 && string.IsNullOrWhiteSpace(response.Message) && response.Status == null)
            {
                response.Result = true;
            }

            return response;
        }

        private static CompaniesApiResponse NormalizeCompaniesResponse(CompaniesApiResponse response)
        {
            response.Companies ??= new List<Company>();

            if (response.Result || response.IsCanceled)
            {
                return response;
            }

            if (response.Companies.Count >= 0 && string.IsNullOrWhiteSpace(response.Message) && response.Status == null)
            {
                response.Result = true;
            }

            return response;
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
    }
}
