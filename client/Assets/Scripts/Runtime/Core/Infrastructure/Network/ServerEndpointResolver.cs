using System;

namespace Panoptes.Core.Infrastructure.Network
{
    public static class ServerEndpointResolver
    {
        public const string DefaultWebSocketUrl = "ws://47.116.32.157:8080/ws";

        public static string ResolveCurrentWebSocketUrl()
        {
            if (NetworkManager.Instance != null && !string.IsNullOrWhiteSpace(NetworkManager.Instance.ServerUrl))
            {
                return NetworkManager.Instance.ServerUrl;
            }

            return DefaultWebSocketUrl;
        }

        public static string ResolveHttpBaseUrl(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                endpoint = DefaultWebSocketUrl;
            }

            var normalized = endpoint.Trim().TrimEnd('/');
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            {
                return normalized;
            }

            if (!string.Equals(uri.Scheme, "ws", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            var scheme = string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase) ? "https" : "http";
            var path = uri.AbsolutePath.TrimEnd('/');
            if (path.EndsWith("/ws", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(0, path.Length - 3);
            }

            return string.IsNullOrWhiteSpace(path)
                ? $"{scheme}://{uri.Authority}"
                : $"{scheme}://{uri.Authority}{path}";
        }
    }
}
