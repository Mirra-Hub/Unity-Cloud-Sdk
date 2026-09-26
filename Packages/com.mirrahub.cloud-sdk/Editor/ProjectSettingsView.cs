using System;
using System.Collections.Generic;
using MirraCloud.Core;
using MirraCloud.Core.Errors;
using MirraCloud.Editor.Dto;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEditor;
using UnityEngine;

namespace MirraCloud.Editor
{
    internal class ProjectSettingsView
    {
        private readonly EditorApiService _apiService;
        private readonly Configuration _configuration;
        private readonly Action _repaint;

        private GUIStyle _sectionStyle;

        private List<EditorProjectDto> _projects;
        private List<EditorBranchDto> _branches;
        private List<EditorPlatformDto> _platforms;
        private List<EditorApiTokenDto> _tokens;

        private int _selectedProjectIndex = -1;
        private int _selectedBranchIndex = -1;
        private int _selectedPlatformIndex = -1;
        private int _selectedTokenIndex = -1;

        private bool _isLoadingProjects;
        private bool _isLoadingBranches;
        private bool _isLoadingPlatforms;
        private bool _isLoadingTokens;

        private string _platformsError;

        private bool _isCreatingToken;
        private string _newTokenName = "";
        private string _createTokenError;
        private bool _showCreateToken;

        public event Action OnDisconnectRequested;

        public ProjectSettingsView(EditorApiService apiService, Configuration configuration, Action repaint)
        {
            _apiService = apiService;
            _configuration = configuration;
            _repaint = repaint;
        }

        public void Draw()
        {
            _sectionStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };

            var prevColor = GUI.color;
            GUI.color = new Color(0.4f, 0.8f, 0.4f);
            EditorGUILayout.LabelField("Connected", _sectionStyle);
            GUI.color = prevColor;

            GUILayout.Space(6);

            DrawProjectDropdown();
            GUILayout.Space(4);

            DrawBranchDropdown();
            GUILayout.Space(4);

            DrawPlatformDropdown();
            GUILayout.Space(4);

            DrawTokenDropdown();
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Height(24)))
            {
                LoadProjects();
            }
            if (GUILayout.Button("Disconnect", GUILayout.Height(24)))
            {
                OnDisconnectRequested?.Invoke();
            }
            GUILayout.EndHorizontal();
        }

        public void LoadProjects()
        {
            var orgId = _apiService.OrgId;
            if (string.IsNullOrEmpty(orgId)) return;

            _isLoadingProjects = true;
            _projects = null;
            _branches = null;
            _tokens = null;
            _selectedProjectIndex = -1;
            _selectedBranchIndex = -1;
            _selectedTokenIndex = -1;
            ResetPlatforms();
            ResetCreateToken();
            _repaint();

            var op = _apiService.GetProjectsAsync(orgId);
            op.OnCompleted += _ =>
            {
                _isLoadingProjects = false;
                if (op.Result.IsSuccess)
                {
                    _projects = op.Result.Data ?? new List<EditorProjectDto>();
                    var idx = FindCurrentIndex(_projects, _configuration.ProjectId, p => p.id);
                    if (idx >= 0)
                    {
                        _selectedProjectIndex = idx;
                        OnProjectSelected();
                    }
                }
                _repaint();
            };
        }

        public void Reset()
        {
            _projects = null;
            _branches = null;
            _tokens = null;
            _selectedProjectIndex = -1;
            _selectedBranchIndex = -1;
            _selectedTokenIndex = -1;
            ResetPlatforms();
            ResetCreateToken();
        }

        private void DrawProjectDropdown()
        {
            EditorGUILayout.LabelField("Project", EditorStyles.label);

            if (_isLoadingProjects)
            {
                EditorGUILayout.LabelField("Loading projects...", EditorStyles.miniLabel);
                return;
            }

            if (_projects == null || _projects.Count == 0)
            {
                EditorGUILayout.LabelField("No projects available", EditorStyles.miniLabel);
                return;
            }

            var names = new string[_projects.Count];
            for (int i = 0; i < _projects.Count; i++)
            {
                names[i] = PopupLabel(_projects[i].name);
            }

            var currentIndex = FindCurrentIndex(_projects, _configuration.ProjectId, p => p.id);
            if (_selectedProjectIndex < 0) _selectedProjectIndex = currentIndex;

            EditorGUI.BeginChangeCheck();
            _selectedProjectIndex = EditorGUILayout.Popup(_selectedProjectIndex, names);
            if (EditorGUI.EndChangeCheck())
            {
                OnProjectSelected();
            }
        }

        private void DrawBranchDropdown()
        {
            EditorGUILayout.LabelField("Branch", EditorStyles.label);

            if (_isLoadingBranches)
            {
                EditorGUILayout.LabelField("Loading branches...", EditorStyles.miniLabel);
                return;
            }

            if (_branches == null || _branches.Count == 0)
            {
                EditorGUILayout.LabelField(_selectedProjectIndex >= 0 ? "No branches available" : "Select a project first", EditorStyles.miniLabel);
                return;
            }

            var names = new string[_branches.Count];
            for (int i = 0; i < _branches.Count; i++)
            {
                names[i] = PopupLabel(_branches[i].name);
            }

            var currentIndex = FindCurrentIndex(_branches, _configuration.BranchId, b => b.name);
            if (_selectedBranchIndex < 0) _selectedBranchIndex = currentIndex;

            EditorGUI.BeginChangeCheck();
            _selectedBranchIndex = EditorGUILayout.Popup(_selectedBranchIndex, names);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyBranch();
            }
        }

        /// <summary>
        /// The platform this build runs on. Its key goes with every sign-in and analytics request, so it decides which
        /// sign-in methods the player gets; a project without platforms refuses every sign-in.
        /// </summary>
        private void DrawPlatformDropdown()
        {
            EditorGUILayout.LabelField("Platform", EditorStyles.label);

            if (_isLoadingPlatforms)
            {
                EditorGUILayout.LabelField("Loading platforms...", EditorStyles.miniLabel);
                return;
            }

            if (_selectedProjectIndex < 0)
            {
                EditorGUILayout.LabelField("Select a project first", EditorStyles.miniLabel);
                return;
            }

            if (!string.IsNullOrEmpty(_platformsError))
            {
                EditorGUILayout.HelpBox(_platformsError, MessageType.Warning);
                return;
            }

            if (_platforms == null || _platforms.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "This project has no platforms yet, so every sign-in is refused. Create one in the Cloud " +
                    "console (Platforms), then press Refresh.",
                    MessageType.Warning);
                return;
            }

            var names = new string[_platforms.Count];
            for (int i = 0; i < _platforms.Count; i++)
            {
                names[i] = PlatformLabel(_platforms[i]);
            }

            var currentIndex = FindCurrentIndex(_platforms, _configuration.PlatformKey, p => p.key);
            if (_selectedPlatformIndex < 0) _selectedPlatformIndex = currentIndex;

            EditorGUI.BeginChangeCheck();
            _selectedPlatformIndex = EditorGUILayout.Popup(_selectedPlatformIndex, names);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyPlatform();
            }

            if (_selectedPlatformIndex < 0 || _selectedPlatformIndex >= _platforms.Count) return;
            var platform = _platforms[_selectedPlatformIndex];

            if (!platform.isEnabled)
            {
                EditorGUILayout.HelpBox(
                    "This platform is switched off in the console: sign-in on it is refused until it is switched on.",
                    MessageType.Warning);
            }

            DrawBuildTargetCheck(platform);
        }

        /// <summary>
        /// An error when the active build target is of a type the platform is not set up for (console → Platforms →
        /// types): the build would sign its players in on a platform meant for other builds. Read on every redraw, so it
        /// follows a switch of the build target without a Refresh.
        /// </summary>
        private static void DrawBuildTargetCheck(EditorPlatformDto platform)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var targetType = BuildTargetPlatformType.ForTarget(target, EditorUserBuildSettings.standaloneBuildSubtarget);
            if (!BuildTargetPlatformType.IsMismatch(platform, targetType)) return;

            EditorGUILayout.HelpBox(
                $"The build target is {BuildTargetPlatformType.TargetDisplayName(target)} " +
                $"({BuildTargetPlatformType.DisplayName(targetType)}), but the platform \"{platform.name}\" is set up for " +
                $"{BuildTargetPlatformType.DisplayNames(platform.platformTypes)}. Switch the build target or pick another " +
                "platform.",
                MessageType.Error);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Build Settings…", EditorStyles.miniButton))
            {
                BuildPlayerWindow.ShowBuildPlayerWindow();
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>"Name (key) · Web, PC", and "— disabled" for a platform switched off in the console.</summary>
        private static string PlatformLabel(EditorPlatformDto platform)
        {
            var label = $"{platform.name} ({platform.key})";

            var types = BuildTargetPlatformType.DisplayNames(platform.platformTypes);
            if (types != null) label += $" · {types}";

            if (!platform.isEnabled) label += " — disabled";

            return PopupLabel(label);
        }

        private void DrawTokenDropdown()
        {
            EditorGUILayout.LabelField("API Token", EditorStyles.label);

            if (_isLoadingTokens)
            {
                EditorGUILayout.LabelField("Loading tokens...", EditorStyles.miniLabel);
                return;
            }

            if (_tokens != null && _tokens.Count > 0)
            {
                var names = new string[_tokens.Count];
                for (int i = 0; i < _tokens.Count; i++)
                {
                    var t = _tokens[i];
                    names[i] = PopupLabel(t.isEnabled ? t.name : $"{t.name} (disabled)");
                }

                var currentIndex = FindCurrentIndex(_tokens, _configuration.Token, t => t.token);
                if (_selectedTokenIndex < 0) _selectedTokenIndex = currentIndex;

                EditorGUI.BeginChangeCheck();
                _selectedTokenIndex = EditorGUILayout.Popup(_selectedTokenIndex, names);
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyToken();
                }
            }
            else if (_selectedProjectIndex >= 0)
            {
                EditorGUILayout.LabelField("No tokens available", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Select a project first", EditorStyles.miniLabel);
                return;
            }

            if (_selectedProjectIndex < 0) return;

            DrawCreateTokenSection();
        }

        private void DrawCreateTokenSection()
        {
            if (!_showCreateToken)
            {
                if (GUILayout.Button("+ Create Token", EditorStyles.miniButton))
                {
                    _showCreateToken = true;
                    _newTokenName = "";
                    _createTokenError = null;
                }
                return;
            }

            GUILayout.Space(4);
            EditorGUILayout.LabelField("New Token Name", EditorStyles.miniLabel);
            _newTokenName = EditorGUILayout.TextField(_newTokenName);

            if (!string.IsNullOrEmpty(_createTokenError))
            {
                var prevColor = GUI.color;
                GUI.color = Color.red;
                EditorGUILayout.LabelField(_createTokenError, EditorStyles.wordWrappedLabel);
                GUI.color = prevColor;
            }

            GUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(_isCreatingToken || string.IsNullOrEmpty(_newTokenName));
            if (GUILayout.Button(_isCreatingToken ? "Creating..." : "Create", EditorStyles.miniButton))
            {
                CreateToken();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Cancel", EditorStyles.miniButton))
            {
                ResetCreateToken();
            }

            GUILayout.EndHorizontal();
        }

        private void CreateToken()
        {
            var project = _projects[_selectedProjectIndex];
            var orgId = _apiService.OrgId;

            _isCreatingToken = true;
            _createTokenError = null;
            _repaint();

            var op = _apiService.CreateTokenAsync(orgId, project.id, _newTokenName);
            op.OnCompleted += _ =>
            {
                _isCreatingToken = false;

                if (op.Result.IsSuccess && op.Result.Data != null)
                {
                    ResetCreateToken();
                    LoadTokens(project.id);
                }
                else
                {
                    _createTokenError = op.Result.Error?.Message ?? "Failed to create token";
                }

                _repaint();
            };
        }

        private void ResetCreateToken()
        {
            _showCreateToken = false;
            _newTokenName = "";
            _createTokenError = null;
            _isCreatingToken = false;
        }

        private void OnProjectSelected()
        {
            if (_projects == null || _selectedProjectIndex < 0 || _selectedProjectIndex >= _projects.Count) return;

            var project = _projects[_selectedProjectIndex];
            _configuration.ProjectId = project.id;
            SaveConfiguration();

            _branches = null;
            _tokens = null;
            _selectedBranchIndex = -1;
            _selectedTokenIndex = -1;
            ResetPlatforms();
            ResetCreateToken();

            LoadBranches(project.id);
            LoadPlatforms(project.id);
            LoadTokens(project.id);
        }

        private void LoadBranches(string projectId)
        {
            _isLoadingBranches = true;
            _repaint();

            var op = _apiService.GetBranchesAsync(projectId);
            op.OnCompleted += _ =>
            {
                _isLoadingBranches = false;
                if (op.Result.IsSuccess)
                {
                    _branches = op.Result.Data ?? new List<EditorBranchDto>();
                    var idx = FindCurrentIndex(_branches, _configuration.BranchId, b => b.name);
                    _selectedBranchIndex = idx >= 0 ? idx : (_branches.Count > 0 ? 0 : -1);
                    if (_selectedBranchIndex >= 0)
                    {
                        ApplyBranch();
                    }
                }
                _repaint();
            };
        }

        private void LoadPlatforms(string projectId)
        {
            _isLoadingPlatforms = true;
            _repaint();

            var op = _apiService.GetPlatformsAsync(projectId);
            op.OnCompleted += _ =>
            {
                // The project was switched while this was in flight: the list belongs to the previous one, and
                // applying it would write that project's platform key into the configuration of this one.
                if (_configuration.ProjectId != projectId)
                {
                    return;
                }

                _isLoadingPlatforms = false;
                if (op.Result.IsSuccess)
                {
                    _platforms = op.Result.Data?.items ?? new List<EditorPlatformDto>();
                    var idx = FindCurrentIndex(_platforms, _configuration.PlatformKey, p => p.key);
                    _selectedPlatformIndex = idx >= 0
                        ? idx
                        : BuildTargetPlatformType.PickDefault(_platforms, BuildTargetPlatformType.Current());
                    if (_selectedPlatformIndex >= 0)
                    {
                        ApplyPlatform();
                    }
                }
                else
                {
                    _platformsError = PlatformsErrorMessage(op.Result);
                }
                _repaint();
            };
        }

        private static string PlatformsErrorMessage(RestApiResult result)
        {
            if (result.HttpStatusCode == 403)
            {
                return "The service account may not read this project's platforms (it needs the platforms.viewer " +
                       "permission). Grant it, or type the platform key into the Configuration asset by hand.";
            }

            if (result.HttpStatusCode >= 500)
            {
                // The window says what to do; what went wrong is for the console, where it can be copied to support.
                var code = result.Error.FirstCloudError()?.Code;
                Debug.LogWarning(
                    $"[MirraCloud Editor] Loading the platforms failed: HTTP {result.HttpStatusCode}" +
                    (code != null ? $" {code}" : "") + $" ({result.Error?.Message}), {result.Error?.Url}");
                return "The server could not return the platforms. Press Refresh later; if this keeps happening, " +
                       "contact support.";
            }

            return $"Could not load the platforms: {result.Error?.Message ?? "unknown error"}.";
        }

        private void ResetPlatforms()
        {
            _platforms = null;
            _selectedPlatformIndex = -1;
            _isLoadingPlatforms = false;
            _platformsError = null;
        }

        private void LoadTokens(string projectId)
        {
            var orgId = _apiService.OrgId;
            if (string.IsNullOrEmpty(orgId)) return;

            _isLoadingTokens = true;
            _repaint();

            var op = _apiService.GetTokensAsync(orgId, projectId);
            op.OnCompleted += _ =>
            {
                _isLoadingTokens = false;
                if (op.Result.IsSuccess)
                {
                    _tokens = op.Result.Data ?? new List<EditorApiTokenDto>();
                    var idx = FindCurrentIndex(_tokens, _configuration.Token, t => t.token);
                    _selectedTokenIndex = idx >= 0 ? idx : (_tokens.Count > 0 ? 0 : -1);
                    if (_selectedTokenIndex >= 0)
                    {
                        ApplyToken();
                    }
                }
                _repaint();
            };
        }

        private void ApplyBranch()
        {
            if (_branches == null || _selectedBranchIndex < 0 || _selectedBranchIndex >= _branches.Count) return;
            _configuration.BranchId = _branches[_selectedBranchIndex].name;
            SaveConfiguration();
        }

        private void ApplyPlatform()
        {
            if (_platforms == null || _selectedPlatformIndex < 0 || _selectedPlatformIndex >= _platforms.Count) return;
            _configuration.PlatformKey = _platforms[_selectedPlatformIndex].key;
            SaveConfiguration();
        }

        private void ApplyToken()
        {
            if (_tokens == null || _selectedTokenIndex < 0 || _selectedTokenIndex >= _tokens.Count) return;
            _configuration.Token = _tokens[_selectedTokenIndex].token;
            SaveConfiguration();
        }

        private void SaveConfiguration()
        {
            EditorUtility.SetDirty(_configuration);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// A popup reads <c>/</c> in an item as a submenu separator, so "Unity Editor / PC" would open a submenu. The
        /// look-alike division slash is shown as is.
        /// </summary>
        private static string PopupLabel(string text)
        {
            return text?.Replace('/', '\u2215');
        }

        private static int FindCurrentIndex<T>(List<T> items, string currentValue, Func<T, string> getId)
        {
            if (items == null || string.IsNullOrEmpty(currentValue)) return -1;
            for (int i = 0; i < items.Count; i++)
            {
                if (getId(items[i]) == currentValue) return i;
            }
            return -1;
        }
    }
}
