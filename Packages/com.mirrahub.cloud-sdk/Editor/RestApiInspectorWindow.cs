using System.IO;
using System.Text;
using MirraCloud.Core.Debugging;
using MirraCloud.Json;
using UnityEditor;
using UnityEngine;

namespace MirraCloud.Editor
{
    public sealed class RestApiInspectorWindow : EditorWindow
    {
        private const string EnabledPrefKey = "MirraCloud.RestApiInspector.Enabled";

        private Vector2 _listScroll;
        private Vector2 _requestBodyScroll;
        private Vector2 _responseBodyScroll;
        private int _selectedIndex = -1;
        private string _filter;
        private int _activeBodyTab;
        private static readonly JsonService JsonService = new JsonService();

        private static GUIStyle _listItemRightStyle;
        private static GUIStyle _listItemRouteStyle;
        private static GUIStyle _bodyStyle;

        private sealed class BodyLayoutCache
        {
            public int EntryId;
            public string Text;
            public float Width;
            public float Height;
        }

        private readonly BodyLayoutCache _requestBodyLayout = new BodyLayoutCache();
        private readonly BodyLayoutCache _responseBodyLayout = new BodyLayoutCache();

        [MenuItem("MirraCloud/Request Inspector")]
        public static void Open()
        {
            GetWindow<RestApiInspectorWindow>("MirraCloud Requests");
        }

        private void OnEnable()
        {
            RestApiTraceBus.IsEnabled = EditorPrefs.GetBool(EnabledPrefKey, true);
            RestApiTraceBus.OnChanged += Repaint;
        }

        private void OnDisable()
        {
            RestApiTraceBus.OnChanged -= Repaint;
        }

        private void OnGUI()
        {
            DrawToolbar();

            var entries = RestApiTraceBus.Entries;
            if (_selectedIndex >= entries.Count)
            {
                _selectedIndex = entries.Count - 1;
            }

            EditorGUILayout.BeginHorizontal();
            DrawList(entries);
            DrawDetails(entries);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var enabled = GUILayout.Toggle(RestApiTraceBus.IsEnabled, "Capture", EditorStyles.toolbarButton, GUILayout.Width(70));
                if (enabled != RestApiTraceBus.IsEnabled)
                {
                    RestApiTraceBus.IsEnabled = enabled;
                    EditorPrefs.SetBool(EnabledPrefKey, enabled);
                }

                GUILayout.Space(8);
                _filter = GUILayout.TextField(_filter ?? string.Empty, EditorStyles.toolbarSearchField, GUILayout.MinWidth(200));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    RestApiTraceBus.Clear();
                    _selectedIndex = -1;
                }

                if (GUILayout.Button("Export", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    Export();
                }
            }
        }

        private void DrawList(System.Collections.Generic.IReadOnlyList<RestApiTraceEntry> entries)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.45f)))
            {
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

                for (var i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (IsVisibleByFilter(e) == false)
                    {
                        continue;
                    }

                    EnsureListStyles();

                    var routeText = e.Route ?? "-";
                    var statusText = e.HttpStatusCode?.ToString() ?? "Unknown";
                    var durationText = $"{e.DurationMs}ms";

                    var rect = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight + 8, GUILayout.ExpandWidth(true));
                    var isSelected = i == _selectedIndex;

                    if (isSelected)
                    {
                        EditorGUI.DrawRect(rect, new Color(0.35f, 0.55f, 0.9f, 0.35f));
                    }

                    const float iconSize = 16f;
                    var iconRect = new Rect(rect.x + 4, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize);
                    DrawStatusIcon(iconRect, e);

                    const float rightColumnWidth = 70f;
                    const float statusColumnWidth = 60f;

                    var routeRect = new Rect(rect.x + 24, rect.y, rect.width - 24 - statusColumnWidth - rightColumnWidth, rect.height);
                    var statusRect = new Rect(rect.x + rect.width - statusColumnWidth - rightColumnWidth, rect.y, statusColumnWidth, rect.height);
                    var durationRect = new Rect(rect.x + rect.width - rightColumnWidth, rect.y, rightColumnWidth, rect.height);

                    GUI.Label(routeRect, routeText, _listItemRouteStyle);
                    GUI.Label(statusRect, statusText, _listItemRightStyle);
                    GUI.Label(durationRect, durationText, _listItemRightStyle);
                    EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        _selectedIndex = i;
                        _activeBodyTab = 0;
                        Event.current.Use();
                        GUI.FocusControl(null);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDetails(System.Collections.Generic.IReadOnlyList<RestApiTraceEntry> entries)
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (_selectedIndex < 0 || _selectedIndex >= entries.Count)
                {
                    EditorGUILayout.HelpBox("Select a request from the list to see details.", MessageType.Info);
                    return;
                }

                var e = entries[_selectedIndex];

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.TextField("Time (UTC)", e.TimestampUtc.ToString("O"));
                    EditorGUILayout.TextField("Method", e.Method);
                    EditorGUILayout.TextField("Route", e.Route);
                    EditorGUILayout.TextField("Url", e.Url);
                    EditorGUILayout.TextField("Status", e.HttpStatusCode?.ToString() ?? "Unknown");
                    EditorGUILayout.TextField("Duration", $"{e.DurationMs} ms");
                    EditorGUILayout.TextField("Retries", e.RetryCount.ToString());

                    if (e.Error != null)
                    {
                        EditorGUILayout.LabelField("Error", $"{e.Error.Type}: {e.Error.Message}");
                    }
                }

                GUILayout.Space(6);
                _activeBodyTab = GUILayout.Toolbar(_activeBodyTab, new[] { "Response", "Request" });

                EnsureBodyStyle();

                if (_activeBodyTab == 0)
                {
                    var responseText = FormatJsonOrRaw(e.ResponseBody) ?? string.Empty;
                    DrawBody(ref _responseBodyScroll, _responseBodyLayout, e.Id, responseText);
                }
                else
                {
                    var requestText = FormatJsonOrRaw(e.RequestBody) ?? string.Empty;
                    DrawBody(ref _requestBodyScroll, _requestBodyLayout, e.Id, requestText);
                }
            }
        }

        private static void EnsureListStyles()
        {
            if (_listItemRouteStyle != null && _listItemRightStyle != null)
            {
                return;
            }

            _listItemRightStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip,
                padding = new RectOffset(4, 8, 2, 2),
                wordWrap = false
            };

            _listItemRouteStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(2, 4, 2, 2),
                wordWrap = false
            };
        }

        private static void EnsureBodyStyle()
        {
            if (_bodyStyle != null)
            {
                return;
            }

            _bodyStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = false
            };
        }

        private static GUIContent GetStatusIconContent(RestApiTraceEntry entry)
        {
            if (entry?.HttpStatusCode is null)
            {
                return EditorGUIUtility.IconContent("console.infoicon") ?? GUIContent.none;
            }

            var code = entry.HttpStatusCode.Value;
            if (code >= 200 && code <= 299)
            {
                return EditorGUIUtility.IconContent("TestPassed") ?? EditorGUIUtility.IconContent("console.infoicon") ?? GUIContent.none;
            }

            return EditorGUIUtility.IconContent("TestFailed") ?? EditorGUIUtility.IconContent("console.erroricon") ?? GUIContent.none;
        }

        private static void DrawStatusIcon(Rect rect, RestApiTraceEntry entry)
        {
            var prev = GUI.color;

            if (entry?.HttpStatusCode is null)
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            }
            else if (entry.HttpStatusCode.Value >= 200 && entry.HttpStatusCode.Value <= 299)
            {
                GUI.color = new Color(0.25f, 0.75f, 0.25f, 1f);
            }
            else
            {
                GUI.color = new Color(0.9f, 0.25f, 0.25f, 1f);
            }

            GUI.Label(rect, GetStatusIconContent(entry));
            GUI.color = prev;
        }

        private static float CalcMaxLineWidth(GUIStyle style, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            var lines = text.Split('\n');
            var max = 0f;
            for (var i = 0; i < lines.Length; i++)
            {
                var lineWidth = style.CalcSize(new GUIContent(lines[i] ?? string.Empty)).x;
                if (lineWidth > max)
                {
                    max = lineWidth;
                }
            }

            return max;
        }

        private static void UpdateBodyLayoutCache(BodyLayoutCache cache, int entryId, string text)
        {
            if (cache.EntryId == entryId && string.Equals(cache.Text, text, System.StringComparison.Ordinal))
            {
                return;
            }

            cache.EntryId = entryId;
            cache.Text = text;
            cache.Width = Mathf.Max(1f, CalcMaxLineWidth(_bodyStyle, text) + 30f);
            cache.Height = Mathf.Max(1f, _bodyStyle.CalcHeight(new GUIContent(text), 100000f) + 10f);
        }

        private void DrawBody(ref Vector2 scroll, BodyLayoutCache cache, int entryId, string text)
        {
            UpdateBodyLayoutCache(cache, entryId, text);

            scroll = EditorGUILayout.BeginScrollView(scroll, false, false, GUILayout.ExpandHeight(true));
            GUILayout.TextArea(text, _bodyStyle, GUILayout.Width(cache.Width), GUILayout.MinHeight(cache.Height));
            EditorGUILayout.EndScrollView();
        }

        private static string FormatJsonOrRaw(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return raw;
            }

            var trimmed = raw.TrimStart();
            if (trimmed.Length == 0)
            {
                return raw;
            }

            var first = trimmed[0];
            if (first != '{' && first != '[')
            {
                return raw;
            }

            try
            {
                var jsonValue = JsonService.FromJson<JsonValue>(raw);
                return JsonService.ToJson(jsonValue, prettyPrint: true);
            }
            catch
            {
                return raw;
            }
        }

        private bool IsVisibleByFilter(RestApiTraceEntry entry)
        {
            if (string.IsNullOrWhiteSpace(_filter))
            {
                return true;
            }

            var f = _filter.Trim();
            return (entry.Route != null && entry.Route.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (entry.Url != null && entry.Url.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (entry.Method != null && entry.Method.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void Export()
        {
            var path = EditorUtility.SaveFilePanel(
                "Export MirraCloud requests",
                "",
                "mirracloud_requests.txt",
                "txt");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var sb = new StringBuilder();
            foreach (var e in RestApiTraceBus.Entries)
            {
                if (IsVisibleByFilter(e) == false)
                {
                    continue;
                }

                sb.AppendLine($"#{e.Id} [{e.TimestampUtc:O}] {(e.IsSuccess ? "OK" : "ERR")} {e.Method} {e.Url}");
                sb.AppendLine($"Status: {e.HttpStatusCode?.ToString() ?? "-"}, Duration: {e.DurationMs}ms, Retries: {e.RetryCount}");
                if (e.Error != null)
                {
                    sb.AppendLine($"Error: {e.Error.Type} {e.Error.Message}");
                }
                sb.AppendLine("RequestBody:");
                sb.AppendLine(FormatJsonOrRaw(e.RequestBody) ?? string.Empty);
                sb.AppendLine("ResponseBody:");
                sb.AppendLine(FormatJsonOrRaw(e.ResponseBody) ?? string.Empty);
                sb.AppendLine(new string('-', 80));
            }

            File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();
        }
    }
}
