using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MirraCloud.Core.AssetsStorage;
using MirraCloud.Core.Storage;
using UnityEditor;
using UnityEngine;

namespace MirraCloud.Editor
{
    /// <summary>
    /// What the asset cache holds on this machine: one row per cached asset — project, branch, asset id, version and
    /// size — largest first. Reading works in Play Mode too; clearing waits until it ends.
    /// </summary>
    internal sealed class AssetCacheWindow : EditorWindow
    {
        private const float VersionColumnWidth = 60f;
        private const float SizeColumnWidth = 80f;
        private const float RowPadding = 4f;

        private readonly struct Row
        {
            public readonly string Key;
            public readonly string Project;
            public readonly string Branch;
            public readonly string Asset;
            public readonly string Version;
            public readonly long Size;

            public Row(string key, long size)
            {
                Key = key;
                Size = size;

                if (AssetCacheKeys.TryParse(key, out string project, out string branch, out string stableId, out int version))
                {
                    Project = project;
                    Branch = branch;
                    Asset = stableId;
                    Version = version.ToString();
                }
                else
                {
                    // A key of an older shape: shown whole rather than hidden, it still takes space.
                    Project = "—";
                    Branch = "—";
                    Asset = key;
                    Version = "—";
                }
            }
        }

        private List<Row> _rows;
        private long _totalSize;
        private bool _isLoading;
        private string _error;
        private Vector2 _scroll;
        private GUIStyle _rightAligned;
        private GUIStyle _rightAlignedBold;

        public static void Open()
        {
            AssetCacheWindow window = GetWindow<AssetCacheWindow>();
            window.titleContent = new GUIContent("MirraCloud Cache");
            window.minSize = new Vector2(560, 240);
            window.Refresh();
        }

        private void OnEnable()
        {
            EditorLocalData.ContainerDeleted += OnContainerDeleted;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            if (_rows == null)
            {
                Refresh();
            }
        }

        private void OnDisable()
        {
            EditorLocalData.ContainerDeleted -= OnContainerDeleted;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnContainerDeleted(string containerId)
        {
            if (containerId == LocalDataContainers.AssetCache)
            {
                Refresh();
            }
        }

        // Clear turns on and off with Play Mode.
        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            Repaint();
        }

        private void Refresh()
        {
            if (_isLoading)
            {
                return;
            }

            _ = RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            _isLoading = true;
            _error = null;
            Repaint();

            try
            {
                IReadOnlyList<KeyValuePair<string, long>> entries = await EditorLocalData.ReadAssetCacheAsync();

                List<Row> rows = new List<Row>(entries.Count);
                long total = 0;

                foreach (KeyValuePair<string, long> entry in entries)
                {
                    rows.Add(new Row(entry.Key, entry.Value));
                    total += entry.Value;
                }

                rows.Sort((a, b) => a.Size != b.Size ? b.Size.CompareTo(a.Size) : string.CompareOrdinal(a.Key, b.Key));

                _rows = rows;
                _totalSize = total;
            }
            catch (Exception exception)
            {
                _rows = new List<Row>();
                _totalSize = 0;
                _error = $"The cache could not be read: {exception.Message}";
            }
            finally
            {
                _isLoading = false;
                Repaint();
            }
        }

        private void OnGUI()
        {
            _rightAligned ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
            _rightAlignedBold ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight };

            DrawToolbar();

            if (_error != null)
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }

            if (_rows == null)
            {
                EditorGUILayout.LabelField("Reading the cache…", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (_rows.Count == 0)
            {
                if (_error == null)
                {
                    EditorGUILayout.HelpBox("The asset cache is empty.", MessageType.Info);
                }

                return;
            }

            DrawHeader();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < _rows.Count; i++)
            {
                DrawRow(_rows[i], i);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(_isLoading))
                {
                    if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    {
                        Refresh();
                    }
                }

                bool canClear = _isLoading == false && EditorLocalData.IsPlaying == false && _rows != null && _rows.Count > 0;

                using (new EditorGUI.DisabledScope(canClear == false))
                {
                    GUIContent clear = new GUIContent("Clear", EditorLocalData.IsPlaying ? "Stop Play Mode to clear the cache." : null);

                    if (GUILayout.Button(clear, EditorStyles.toolbarButton, GUILayout.Width(50)) && EditorLocalData.RefuseWhilePlaying() == false)
                    {
                        _ = EditorLocalData.ClearAssetCacheAsync();
                    }
                }

                GUILayout.FlexibleSpace();

                if (_rows != null && _rows.Count > 0)
                {
                    string assets = _rows.Count == 1 ? "1 asset" : $"{_rows.Count} assets";
                    GUILayout.Label($"{assets} · {EditorUtility.FormatBytes(_totalSize)}", EditorStyles.miniLabel);
                    GUILayout.Space(8);
                }

                if (GUILayout.Button(EditorLocalData.RevealLabel, EditorStyles.toolbarButton))
                {
                    EditorLocalData.Reveal(LocalDataContainers.AssetCache);
                }
            }
        }

        private void DrawHeader()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight + RowPadding, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.05f) : new Color(0f, 0f, 0f, 0.06f));

            DrawColumns(rect, "Project", "Branch", "Asset", "Version", "Size", null, EditorStyles.boldLabel);
        }

        private void DrawRow(Row row, int index)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight + RowPadding, GUILayout.ExpandWidth(true));

            if (index % 2 == 1)
            {
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.025f) : new Color(0f, 0f, 0f, 0.03f));
            }

            DrawColumns(rect, row.Project, row.Branch, row.Asset, row.Version, EditorUtility.FormatBytes(row.Size), row.Key, EditorStyles.label);
        }

        private void DrawColumns(Rect rect, string project, string branch, string asset, string version, string size, string tooltip, GUIStyle style)
        {
            rect.xMin += 4f;
            rect.xMax -= 4f;

            float flexible = Mathf.Max(0f, rect.width - VersionColumnWidth - SizeColumnWidth);
            float projectWidth = flexible * 0.3f;
            float branchWidth = flexible * 0.25f;
            float assetWidth = flexible - projectWidth - branchWidth;

            float x = rect.x;

            GUI.Label(new Rect(x, rect.y, projectWidth, rect.height), new GUIContent(project, tooltip), style);
            x += projectWidth;

            GUI.Label(new Rect(x, rect.y, branchWidth, rect.height), new GUIContent(branch, tooltip), style);
            x += branchWidth;

            GUI.Label(new Rect(x, rect.y, assetWidth, rect.height), new GUIContent(asset, tooltip), style);
            x += assetWidth;

            GUIStyle right = style == EditorStyles.boldLabel ? _rightAlignedBold : _rightAligned;

            GUI.Label(new Rect(x, rect.y, VersionColumnWidth, rect.height), version, right);
            x += VersionColumnWidth;

            GUI.Label(new Rect(x, rect.y, SizeColumnWidth, rect.height), size, right);
        }
    }
}
