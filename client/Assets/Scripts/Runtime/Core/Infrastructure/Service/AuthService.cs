using System;
using System.Text;
using System.Threading.Tasks;
using Panoptes.Core.Infrastructure.Network;
using UnityEngine;
using UnityEngine.Networking;

namespace Panoptes.Core.Infrastructure.Service
{
    public sealed class AuthResult
    {
        public bool Success { get; set; }
        public string Token { get; set; }
        public string PlayerID { get; set; }
        public string Username { get; set; }
        public string ErrorCode { get; set; }
    }

    public sealed class AuthService
    {
        private readonly string _baseUrl;

        [Serializable]
        private sealed class AuthRequestBody
        {
            public string username;
            public string password;
        }

        [Serializable]
        private sealed class AuthSuccessBody
        {
            public string token;
            public string player_id;
            public string username;
        }

        [Serializable]
        private sealed class AuthErrorBody
        {
            public string error;
        }

        public AuthService(string baseUrl = null)
        {
            var resolvedBaseUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? ServerEndpointResolver.ResolveHttpBaseUrl(ServerEndpointResolver.ResolveCurrentWebSocketUrl())
                : ServerEndpointResolver.ResolveHttpBaseUrl(baseUrl);
            _baseUrl = resolvedBaseUrl.TrimEnd('/');
        }

        public Task<AuthResult> RegisterAsync(string username, string password)
        {
            return PostAuthAsync("/api/register", username, password);
        }

        public Task<AuthResult> LoginAsync(string username, string password)
        {
            return PostAuthAsync("/api/login", username, password);
        }

        private async Task<AuthResult> PostAuthAsync(string path, string username, string password)
        {
            var requestBody = new AuthRequestBody
            {
                username = username ?? string.Empty,
                password = password ?? string.Empty
            };

            var bodyJson = JsonUtility.ToJson(requestBody);
            var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

            using var request = new UnityWebRequest($"{_baseUrl}{path}", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(bodyBytes),
                downloadHandler = new DownloadHandlerBuffer()
            };

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            await SendRequestAsync(request);
            var responseText = request.downloadHandler?.text ?? string.Empty;
            
            Debug.Log(responseText);

            var isSuccess =
                request.result == UnityWebRequest.Result.Success &&
                request.responseCode >= 200 &&
                request.responseCode < 300;

            if (isSuccess)
            {
                var ok = ParseSuccessResponse(responseText);
                ok.Success = true;
                return ok;
            }

            var errorCode = ParseErrorCode(responseText);
            if (string.IsNullOrWhiteSpace(errorCode))
            {
                errorCode = request.result == UnityWebRequest.Result.ConnectionError
                    ? "internal_error"
                    : "invalid_request";
            }

            return new AuthResult
            {
                Success = false,
                ErrorCode = errorCode
            };
        }

        private static Task SendRequestAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();
            operation.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }

        private static AuthResult ParseSuccessResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AuthResult();
            }

            try
            {
                var dto = JsonUtility.FromJson<AuthSuccessBody>(json);
                if (dto == null)
                {
                    return new AuthResult();
                }

                return new AuthResult
                {
                    Token = dto.token,
                    PlayerID = dto.player_id,
                    Username = dto.username
                };
            }
            catch (Exception)
            {
                return new AuthResult();
            }
        }

        private static string ParseErrorCode(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            try
            {
                var dto = JsonUtility.FromJson<AuthErrorBody>(json);
                return dto?.error ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
