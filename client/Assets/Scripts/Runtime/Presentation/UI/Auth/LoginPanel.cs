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

namespace Panoptes.Presentation.UI.Auth
{
    public sealed class LoginPanel : MonoBehaviour
    {
        public TMP_InputField usernameInput;
        public TMP_InputField passwordInput;
        
        public Button loginButton;
        public Button registerButton;
        
        private AuthService _authService;


        private string Username => usernameInput != null ? usernameInput.text.Trim() : string.Empty;
        private string Password => passwordInput != null ? passwordInput.text : string.Empty;

        private void Awake()
        {
            _authService = new AuthService();
            loginButton.onClick.AddListener(OnClickLogin);
            registerButton.onClick.AddListener(OnClickRegister);
        }

        private void SetButtonsInteractable(bool value)
        {
            loginButton.interactable = value;
            registerButton.interactable = value;
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
            if (ErrorToast.Instance != null)
            {
                ErrorToast.Instance.Show(message, success);
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
                var result = await _authService.LoginAsync(Username, Password);
                if (!result.Success)
                {
                    ShowTip(MapErrorCode(result.ErrorCode), false);
                    return;
                }

                if (SessionManager.Instance == null)
                {
                    ShowTip("系统未初始化", false);
                    return;
                }

                SessionManager.Instance.SetSession(result.Token, result.PlayerID, result.Username);

                if (AppManager.Instance == null)
                {
                    ShowTip("系统未初始化", false);
                    return;
                }

                AppManager.Instance.TransitionTo(AppState.Lobby);
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
