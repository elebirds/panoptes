/*************************************************
 * Project: Panoptes
 * File: LoginPanel.cs
 * Author: Panoptes Team
 * Date: 2026-04-05
 * Description: Login Panel Logics.
 *************************************************/

using System;
using Panoptes.Core.Application.App;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Presentation.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.Auth
{
    public sealed class LoginPanel : MonoBehaviour
    {
        public TMP_InputField usernameInput;
        public TMP_InputField passwordInput;
        
        public Button loginButton;
        public Button registerButton;
        
        private AuthService _authService;
        private SessionManager _sessionManager;
        private AppManager _appManager;
        private ErrorToast _errorToast;

        [Inject]
        public void Construct(
            AuthService authService,
            SessionManager sessionManager,
            AppManager appManager,
            ErrorToast errorToast)
        {
            _authService = authService;
            _sessionManager = sessionManager;
            _appManager = appManager;
            _errorToast = errorToast;
        }

        private string Username => usernameInput != null ? usernameInput.text.Trim() : string.Empty;
        private string Password => passwordInput != null ? passwordInput.text : string.Empty;

        private void Awake()
        {
            loginButton?.onClick.AddListener(OnClickLogin);
            registerButton?.onClick.AddListener(OnClickRegister);
        }

        private void OnDestroy()
        {
            loginButton?.onClick.RemoveListener(OnClickLogin);
            registerButton?.onClick.RemoveListener(OnClickRegister);
        }

        private void SetButtonsInteractable(bool value)
        {
            if (loginButton != null)
            {
                loginButton.interactable = value;
            }

            if (registerButton != null)
            {
                registerButton.interactable = value;
            }
        }

        private bool IsValid(bool showTip)
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                if (showTip)
                {
                    ShowTip("请输入用户名和密码", false);
                }
                // TODO: 显示提示
                return false;
            }
            // TODO: 其他的用户名、密码规则
            return true;
        }

        private void ShowTip(string message, bool success)
        {
            if (_errorToast != null)
            {
                _errorToast.Show(message, success);
                return;
            }

            if (success)
            {
                Debug.Log(message);
                return;
            }

            Debug.LogWarning(message);
        }
        
        private async void OnClickLogin()
        {
            if (!IsValid(true))
            {
                return;
            }
            try
            {
                SetButtonsInteractable(false);
                if (_authService == null)
                {
                    ShowTip("系统未初始化", false);
                    return;
                }

                var result = await _authService.LoginAsync(Username, Password);
                if (!result.Success)
                {
                    ShowTip(MapErrorCode(result.ErrorCode), false);
                    return;
                }

                if (_sessionManager == null)
                {
                    ShowTip("系统未初始化", false);
                    return;
                }

                _sessionManager.SetSession(result.Token, result.PlayerID, result.Username);

                if (_appManager == null)
                {
                    ShowTip("系统未初始化", false);
                    return;
                }

                _appManager.TransitionTo(AppState.Lobby);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LoginPanel] Login failed: {e}");
                ShowTip("服务器错误，请稍后重试", false);
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private async void OnClickRegister()
        {
            if (!IsValid(true))
            {
                return;
            }

            try
            {
                SetButtonsInteractable(false);
                if (_authService == null)
                {
                    ShowTip("系统未初始化", false);
                    return;
                }

                var result = await _authService.RegisterAsync(Username, Password);
                if (!result.Success)
                {
                    ShowTip(MapErrorCode(result.ErrorCode), false);
                    SetButtonsInteractable(true);
                    return;
                }

                ShowTip("注册成功，请登录", true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LoginPanel] Register failed: {e}");
                ShowTip("服务器错误，请稍后重试" + e.Message, false);
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }
        private static string MapErrorCode(string code)
        {
            return code switch
            {
                "invalid_request" => "请求格式错误",
                "invalid_credentials" => "用户名或密码错误",
                "user_exists" => "用户名已被占用",
                "internal_error" => "服务器错误，请稍后重试",
                _ => "未知错误"
            };
        }
    }
}
