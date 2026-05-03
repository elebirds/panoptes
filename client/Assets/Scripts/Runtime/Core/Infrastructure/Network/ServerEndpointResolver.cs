using System;
using UnityEngine;

namespace Panoptes.Core.Infrastructure.Network
{
    public static class ServerEndpointResolver
    {
        public const string EditorWebSocketUrl = "ws://localhost:8080/ws";
        public const string RemoteWebSocketUrl = "ws://47.116.32.157:8080/ws";

        public static string DefaultWebSocketUrl => ResolveDefaultWebSocketUrl(UnityEngine.Application.isEditor);

        public static string ResolveDefaultWebSocketUrl(bool isEditor)
        {
            return isEditor ? EditorWebSocketUrl : RemoteWebSocketUrl;
        }

        public static string ResolveCurrentWebSocketUrl(string configuredEndpoint)
        {
            if (!string.IsNullOrWhiteSpace(configuredEndpoint))
            {
                return configuredEndpoint;
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
