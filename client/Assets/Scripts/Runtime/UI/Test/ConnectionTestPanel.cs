using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Panoptes.Protocol.V1;
using Panoptes.Protocol.V1.Auth;
using Panoptes.Runtime.Network;
using Panoptes.Runtime.Service;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Panoptes.Runtime.UI.Test
{
    public sealed class ConnectionTestPanel : MonoBehaviour
    {
        private enum ConnectionState
        {
            Unconnected,
            Connecting,
            Connected,
            Disconnected
        }

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private ScrollRect logScrollRect;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button sendTestButton;

        [Header("Config")]
        [SerializeField] private string wsBaseUrl = "ws://localhost:8080/ws";

        private readonly List<string> _logs = new();
        private ConnectionState _state = ConnectionState.Unconnected;

        private void Awake()
        {
            connectButton?.onClick.AddListener(OnClickConnect);
            disconnectButton?.onClick.AddListener(OnClickDisconnect);
            sendTestButton?.onClick.AddListener(OnClickSendTest);
        }

        private void OnEnable()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnected += HandleConnected;
                NetworkManager.Instance.OnDisconnected += HandleDisconnected;
                NetworkManager.Instance.OnError += HandleError;
                NetworkManager.Instance.OnEnvelopeReceived += HandleEnvelopeReceived;
                NetworkManager.Instance.OnEnvelopeSent += HandleEnvelopeSent;
            }

            _state = NetworkManager.Instance != null && NetworkManager.Instance.IsConnected
                ? ConnectionState.Connected
                : ConnectionState.Unconnected;
            RefreshStatus();
            RefreshButtons();
        }

        private void OnDisable()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnected -= HandleConnected;
                NetworkManager.Instance.OnDisconnected -= HandleDisconnected;
                NetworkManager.Instance.OnError -= HandleError;
                NetworkManager.Instance.OnEnvelopeReceived -= HandleEnvelopeReceived;
                NetworkManager.Instance.OnEnvelopeSent -= HandleEnvelopeSent;
            }
        }

        private void OnDestroy()
        {
            connectButton?.onClick.RemoveListener(OnClickConnect);
            disconnectButton?.onClick.RemoveListener(OnClickDisconnect);
            sendTestButton?.onClick.RemoveListener(OnClickSendTest);
        }

        private async void OnClickConnect()
        {
            if (NetworkManager.Instance == null)
            {
                AppendLog("[ERR] NetworkManager 未初始化");
                return;
            }

            var token = SessionManager.Instance != null ? SessionManager.Instance.Token : string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                AppendLog("[ERR] SessionManager.Token 为空，请先登录");
                return;
            }

            _state = ConnectionState.Connecting;
            RefreshStatus();
            RefreshButtons();

            var wsUrl = $"{wsBaseUrl}?token={UnityWebRequest.EscapeURL(token)}";
            AppendLog($"[SYS] Connecting: {wsUrl}");

            try
            {
                await NetworkManager.Instance.ConnectAsync(wsUrl);
            }
            catch (Exception e)
            {
                AppendLog($"[ERR] Connect failed: {e.Message}");
                _state = ConnectionState.Disconnected;
                RefreshStatus();
                RefreshButtons();
            }
        }

        private void OnClickDisconnect()
        {
            if (NetworkManager.Instance == null)
            {
                AppendLog("[ERR] NetworkManager 未初始化");
                return;
            }

            NetworkManager.Instance.Disconnect();
            AppendLog("[SYS] Disconnect requested");
        }

        private void OnClickSendTest()
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            {
                AppendLog("[ERR] 当前未连接，无法发送");
                return;
            }

            var msg = new MsgLogin
            {
                Username = "connection_test",
                Password = "test_password"
            };

            MessageSender.Send(msg);
        }

        private void HandleConnected()
        {
            _state = ConnectionState.Connected;
            AppendLog("[SYS] 已连接");
            RefreshStatus();
            RefreshButtons();
        }

        private void HandleDisconnected()
        {
            _state = ConnectionState.Disconnected;
            AppendLog("[SYS] 已断开");
            RefreshStatus();
            RefreshButtons();
        }

        private void HandleError(string err)
        {
            AppendLog($"[ERR] {err}");
        }

        private void HandleEnvelopeReceived(Envelope envelope)
        {
            AppendLog($"[RX] {envelope.Type} {Truncate(envelope.Payload)}");
        }

        private void HandleEnvelopeSent(Envelope envelope)
        {
            AppendLog($"[TX] {envelope.Type} {Truncate(envelope.Payload)}");
        }

        private void AppendLog(string line)
        {
            _logs.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
            if (_logs.Count > 50)
            {
                _logs.RemoveAt(0);
            }

            if (logText != null)
            {
                logText.text = string.Join("\n", _logs);
            }

            ScrollToBottomNextFrame();
        }

        private async void ScrollToBottomNextFrame()
        {
            await Task.Yield();
            if (logScrollRect != null)
            {
                logScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void RefreshStatus()
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = _state switch
            {
                ConnectionState.Connecting => "连接中",
                ConnectionState.Connected => "已连接",
                ConnectionState.Disconnected => "已断开",
                _ => "未连接"
            };
        }

        private void RefreshButtons()
        {
            if (connectButton != null)
            {
                connectButton.interactable = _state == ConnectionState.Unconnected ||
                                             _state == ConnectionState.Disconnected;
            }

            if (disconnectButton != null)
            {
                disconnectButton.interactable = _state == ConnectionState.Connecting ||
                                                _state == ConnectionState.Connected;
            }

            if (sendTestButton != null)
            {
                sendTestButton.interactable = _state == ConnectionState.Connected;
            }
        }

        private static string Truncate(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= 160)
            {
                return text;
            }

            return text.Substring(0, 160) + "...";
        }
    }
}