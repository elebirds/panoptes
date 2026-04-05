/*************************************************
 * Project: Panoptes
 * File: LoginPanel.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Login panel placeholder.
 *************************************************/

using System;
using TMPro;
using Panoptes.Runtime.App;
using Panoptes.Runtime.Service;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Runtime.UI.Auth
{
    public sealed class LoginPanel : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Button loginTab;
        [SerializeField] private Button registerTab;

        [Header("Forms")]
        [SerializeField] private GameObject loginForm;
        [SerializeField] private GameObject registerForm;

        [Header("Login")]
        [SerializeField] private TMP_InputField loginUsernameField;
        [SerializeField] private TMP_InputField loginPasswordField;
        [SerializeField] private Button loginButton;
        [SerializeField] private TextMeshProUGUI loginErrorText;

        [Header("Register")]
        [SerializeField] private TMP_InputField registerUsernameField;
        [SerializeField] private TMP_InputField registerPasswordField;
        [SerializeField] private TMP_InputField registerConfirmPasswordField;
        [SerializeField] private Button registerButton;
        [SerializeField] private TextMeshProUGUI registerErrorText;

        private AuthService _authService;
        private Color _loginErrorOriginalColor;

        private void Awake()
        {
            _authService = new AuthService();

            if (loginErrorText != null)
            {
                _loginErrorOriginalColor = loginErrorText.color;
            }

            loginTab?.onClick.AddListener(SwitchToLoginTab);
            registerTab?.onClick.AddListener(SwitchToRegisterTab);
            loginButton?.onClick.AddListener(OnClickLogin);
            registerButton?.onClick.AddListener(OnClickRegister);
        }

        private void Start()
        {
            HideAllErrors();
            SwitchToLoginTab();
        }

        private void OnDestroy()
        {
            loginTab?.onClick.RemoveListener(SwitchToLoginTab);
            registerTab?.onClick.RemoveListener(SwitchToRegisterTab);
            loginButton?.onClick.RemoveListener(OnClickLogin);
            registerButton?.onClick.RemoveListener(OnClickRegister);
        }

        private void SwitchToLoginTab()
        {
            if (loginForm != null)
            {
                loginForm.SetActive(true);
            }

            if (registerForm != null)
            {
                registerForm.SetActive(false);
            }

            HideAllErrors();
        }

        private void SwitchToRegisterTab()
        {
            if (loginForm != null)
            {
                loginForm.SetActive(false);
            }

            if (registerForm != null)
            {
                registerForm.SetActive(true);
            }

            HideAllErrors();
        }

        private async void OnClickLogin()
        {
            var username = loginUsernameField != null ? loginUsernameField.text.Trim() : string.Empty;
            var password = loginPasswordField != null ? loginPasswordField.text : string.Empty;

            HideAllErrors();
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowLoginMessage("请输入用户名和密码", false);
                return;
            }

            SetButtonsInteractable(false);
            try
            {
                var result = await _authService.LoginAsync(username, password);
                if (!result.Success)
                {
                    ShowLoginMessage(MapErrorCode(result.ErrorCode), false);
                    return;
                }

                if (SessionManager.Instance == null)
                {
                    ShowLoginMessage("系统未初始化", false);
                    return;
                }

                SessionManager.Instance.SetSession(result.Token, result.PlayerID, result.Username);

                if (AppManager.Instance == null)
                {
                    ShowLoginMessage("系统未初始化", false);
                    return;
                }

                AppManager.Instance.TransitionTo(AppState.Lobby);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LoginPanel] Login failed: {e}");
                ShowLoginMessage("服务器错误，请稍后重试", false);
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private async void OnClickRegister()
        {
            var username = registerUsernameField != null ? registerUsernameField.text.Trim() : string.Empty;
            var password = registerPasswordField != null ? registerPasswordField.text : string.Empty;
            var confirmPassword = registerConfirmPasswordField != null
                ? registerConfirmPasswordField.text
                : string.Empty;

            HideAllErrors();
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowRegisterMessage("请输入用户名和密码");
                return;
            }

            if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
            {
                ShowRegisterMessage("两次密码输入不一致");
                return;
            }

            SetButtonsInteractable(false);
            try
            {
                var result = await _authService.RegisterAsync(username, password);
                if (!result.Success)
                {
                    ShowRegisterMessage(MapErrorCode(result.ErrorCode));
                    return;
                }

                SwitchToLoginTab();
                ShowLoginMessage("注册成功，请登录", true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LoginPanel] Register failed: {e}");
                ShowRegisterMessage("服务器错误，请稍后重试");
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (loginTab != null)
            {
                loginTab.interactable = interactable;
            }

            if (registerTab != null)
            {
                registerTab.interactable = interactable;
            }

            if (loginButton != null)
            {
                loginButton.interactable = interactable;
            }

            if (registerButton != null)
            {
                registerButton.interactable = interactable;
            }
        }

        private void HideAllErrors()
        {
            if (loginErrorText != null)
            {
                loginErrorText.gameObject.SetActive(false);
                loginErrorText.color = _loginErrorOriginalColor;
            }

            if (registerErrorText != null)
            {
                registerErrorText.gameObject.SetActive(false);
            }
        }

        private void ShowLoginMessage(string text, bool isSuccess)
        {
            if (loginErrorText == null)
            {
                return;
            }

            loginErrorText.text = text;
            loginErrorText.color = isSuccess ? new Color(0.16f, 0.62f, 0.28f, 1f) : _loginErrorOriginalColor;
            loginErrorText.gameObject.SetActive(true);
        }

        private void ShowRegisterMessage(string text)
        {
            if (registerErrorText == null)
            {
                return;
            }

            registerErrorText.text = text;
            registerErrorText.gameObject.SetActive(true);
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
