using UnityEditor;
using UnityEngine;

namespace MirraCloud.Editor
{
    public class MirraCloudEditorWindow : EditorWindow
    {
        private Texture2D logo;

        private const int PADDING_X = 5;
        private const int PADDING_Y = 10;

        // Resolved by guid rather than by path: the SDK can sit in Packages/ or, for anyone who
        // copied the folder in, anywhere under Assets/.
        private const string LOGO_GUID = "1a76f256c4b6d9a49b069da3395b1963";

        private Configuration _configuration;
        private GUIStyle _titleStyle;

        private EditorApiService _apiService;
        private LoginView _loginView;
        private ProjectSettingsView _settingsView;

        [MenuItem("Tools/Mirra Cloud/Manager")]
        public static void Open()
        {
            MirraCloudEditorWindow window = GetWindow<MirraCloudEditorWindow>();
            window.titleContent = new GUIContent("Mirra Cloud");
            window.minSize = new Vector2(350, 300);
        }

        private void OnEnable()
        {
            logo = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(LOGO_GUID));

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };

            EnsureInitialized();
        }

        /// <summary>
        /// Developer Settings can be switched while this window is open. Re-resolving on focus is
        /// enough: you change the asset, then click back here. A changed host drops the api service
        /// so <see cref="EnsureInitialized"/> rebuilds everything against the new one.
        /// </summary>
        private void OnFocus()
        {
            if (_configuration == null) return;

            string before = _configuration.EditorApiUrl;
            _configuration.ResolveEnvironment();

            if (_configuration.EditorApiUrl == before) return;

            _apiService = null;
            Repaint();
        }

        private void EnsureInitialized()
        {
            if (_apiService != null) return;

            _configuration = ConfigurationAsset.LoadOrCreate();
            _apiService = new EditorApiService(_configuration);

            _loginView = new LoginView(_apiService, Repaint);
            _loginView.OnConnected += OnConnected;

            _settingsView = new ProjectSettingsView(_apiService, _configuration, Repaint);
            _settingsView.OnDisconnectRequested += Disconnect;

            if (_apiService.IsAuthenticated)
            {
                _settingsView.LoadProjects();
            }
            else if (_loginView.HasSavedKey)
            {
                _loginView.AutoConnect();
            }
        }

        private void OnGUI()
        {
            EnsureInitialized();

            GUILayout.BeginHorizontal("box", GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            GUILayout.Space(PADDING_X);

            GUILayout.BeginVertical();
            GUILayout.Space(PADDING_Y);

            DrawHeader();
            GUILayout.Space(10);

            if (_apiService == null || !_apiService.IsAuthenticated)
            {
                _loginView.Draw();
            }
            else
            {
                _settingsView.Draw();
            }

            GUILayout.Space(PADDING_Y);
            GUILayout.EndVertical();

            GUILayout.Space(PADDING_X);
            GUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();

            if (logo != null)
            {
                GUILayout.Label(logo, GUILayout.Width(25), GUILayout.Height(25));
            }

            GUILayout.Label("Mirra Cloud", _titleStyle, GUILayout.Height(25));

            GUILayout.FlexibleSpace();

            // Which backend this window is talking to — the one thing you want to see at a glance
            // when you switch between a local host and production.
            GUILayout.Label(HostLabel(), EditorStyles.miniLabel, GUILayout.Height(25));

            GUILayout.EndHorizontal();
        }

        private string HostLabel()
        {
            string url = _configuration != null ? _configuration.EditorApiUrl : null;

            if (string.IsNullOrEmpty(url)) return "no host";

            return url.Replace("https://", "").Replace("http://", "").TrimEnd('/');
        }

        private void OnConnected()
        {
            _settingsView.LoadProjects();
        }

        private void Disconnect()
        {
            _apiService.Disconnect();
            _loginView.Reset();
            _settingsView.Reset();
            Repaint();
        }
    }
}
