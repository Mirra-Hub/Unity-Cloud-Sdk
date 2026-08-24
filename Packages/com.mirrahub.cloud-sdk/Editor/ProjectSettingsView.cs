using System;
using System.Collections.Generic;
using MirraCloud.Core;
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
        private List<EditorApiTokenDto> _tokens;

        private int _selectedProjectIndex = -1;
        private int _selectedBranchIndex = -1;
        private int _selectedTokenIndex = -1;

        private bool _isLoadingProjects;
        private bool _isLoadingBranches;
        private bool _isLoadingTokens;

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
                names[i] = _projects[i].name;
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
                names[i] = _branches[i].name;
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
                    names[i] = t.isEnabled ? t.name : $"{t.name} (disabled)";
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
            ResetCreateToken();

            LoadBranches(project.id);
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
