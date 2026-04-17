#if UNITY_EDITOR || DEVELOPMENT_BUILD || PANOPTES_DEBUG_PANEL
using System;
using System.Text;
using System.Threading.Tasks;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
using UnityEngine;
using UnityEngine.Networking;

namespace Panoptes.DebugTools
{
    [Serializable]
    public sealed class DebugVisionResult
    {
        public string participant_id;
        public bool full_map;
        public int visible_node_count;
        public int total_node_count;
        public int visible_unit_count;
        public bool refreshed;

        public string ParticipantID => participant_id ?? string.Empty;
        public bool FullMap => full_map;
        public int VisibleNodeCount => visible_node_count;
        public int TotalNodeCount => total_node_count;
        public int VisibleUnitCount => visible_unit_count;
        public bool Refreshed => refreshed;
    }

    public sealed class DebugGameHttpService
    {
        private readonly string _baseUrlOverride;

        [Serializable]
        private sealed class DebugVisionRequestBody
        {
            public string participant_id;
            public bool full_map;
        }

        [Serializable]
        private sealed class DebugErrorBody
        {
            public string error;
        }

        public DebugGameHttpService(string baseUrl = null)
        {
            _baseUrlOverride = baseUrl;
        }

        public async Task<DebugVisionResult> SetFullMapVisibilityAsync(bool fullMap, string participantID = null)
        {
            var token = SessionManager.Instance != null ? SessionManager.Instance.Token : string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("missing_session");
            }

            var body = JsonUtility.ToJson(new DebugVisionRequestBody
            {
                participant_id = participantID ?? string.Empty,
                full_map = fullMap
            });
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var baseUrl = ResolveBaseUrl(string.IsNullOrWhiteSpace(_baseUrlOverride)
                ? NetworkManager.Instance != null ? NetworkManager.Instance.ServerUrl : null
                : _baseUrlOverride);

            using var request = new UnityWebRequest($"{baseUrl}/api/dev/game/vision", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(bodyBytes),
                downloadHandler = new DownloadHandlerBuffer()
            };

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {token}");

            await SendRequestAsync(request);

            var responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            var isSuccess =
                request.result == UnityWebRequest.Result.Success &&
                request.responseCode >= 200 &&
                request.responseCode < 300;
            if (!isSuccess)
            {
                var errorCode = ParseErrorCode(responseText);
                if (string.IsNullOrWhiteSpace(errorCode))
                {
                    errorCode = request.result == UnityWebRequest.Result.ConnectionError
                        ? "connection_error"
                        : $"http_{request.responseCode}";
                }
                throw new InvalidOperationException(errorCode);
            }

            var result = JsonUtility.FromJson<DebugVisionResult>(responseText);
            if (result == null)
            {
                throw new InvalidOperationException("invalid_response");
            }
            return result;
        }

        public static string ResolveBaseUrl(string endpoint)
        {
            return ServerEndpointResolver.ResolveHttpBaseUrl(endpoint);
        }

        private static string ParseErrorCode(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            try
            {
                var dto = JsonUtility.FromJson<DebugErrorBody>(json);
                return dto != null ? (dto.error ?? string.Empty) : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static Task SendRequestAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();
            operation.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }
    }
}
#endif
