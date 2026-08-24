using System;
using MirraCloud.Core;
using MirraCloud.Editor.Dto;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEditor;
using UnityEngine;

namespace MirraCloud.Editor
{
    internal class LoginView
    {
        private readonly EditorApiService _apiService;
        private readonly Action _repaint;

        private GUIStyle _sectionStyle;
        private string _saKeyInput = "";
        private bool _isConnecting;
        private string _connectError;

        public event Action OnConnected;
        public bool HasSavedKey => !string.IsNullOrEmpty(_saKeyInput);

        public LoginView(EditorApiService apiService, Action repaint)
        {
            _apiService = apiService;
            _repaint = repaint;
            _saKeyInput = EditorApiService.GetSavedKey();
        }

        public void Draw()
        {
            _sectionStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };

            GUILayout.Label("Connect with Service Account", _sectionStyle);
            GUILayout.Space(6);

            EditorGUILayout.LabelField("Service Account Key", EditorStyles.label);
            _saKeyInput = EditorGUILayout.PasswordField(_saKeyInput);

            GUILayout.Space(8);

            if (!string.IsNullOrEmpty(_connectError))
            {
                var prevColor = GUI.color;
                GUI.color = Color.red;
                EditorGUILayout.LabelField(_connectError, EditorStyles.wordWrappedLabel);
                GUI.color = prevColor;
                GUILayout.Space(4);
            }

            EditorGUI.BeginDisabledGroup(_isConnecting || string.IsNullOrEmpty(_saKeyInput));
            if (GUILayout.Button(_isConnecting ? "Connecting..." : "Connect", GUILayout.Height(28)))
            {
                Connect();
            }
            EditorGUI.EndDisabledGroup();
        }

        public void AutoConnect()
        {
            Connect();
        }

        public void Reset()
        {
            _saKeyInput = "";
            _connectError = null;
            _isConnecting = false;
        }

        private void Connect()
        {
            _isConnecting = true;
            _connectError = null;
            _repaint();

            var op = _apiService.ExchangeKeyAsync(_saKeyInput);
            op.OnCompleted += _ =>
            {
                _isConnecting = false;

                if (op.Result.IsSuccess)
                {
                    OnConnected?.Invoke();
                }
                else
                {
                    _connectError = op.Result.Error?.Message ?? "Authentication failed";
                }

                _repaint();
            };
        }
    }
}
