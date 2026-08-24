using System;
using System.Collections.Generic;
using System.Globalization;
using MirraCloud.Core.RemoteConfig;
using MirraCloud.Core.RemoteConfig.Responses;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Remote Config screen: every key the server sent this client, flattened out of its config groups
    /// into one searchable table, with the whole value one click away.
    /// <para>
    /// The service is read-only — keys are authored in the Mirra Hub console — and the response carries
    /// only the groups that match this player's segments. So this screen shows what *this* player
    /// resolves to, which is why the group each key came from is a column rather than a heading.
    /// </para>
    /// <para>
    /// One call feeds everything here; the search box filters the rows already in memory instead of
    /// asking again, because the endpoint has no query parameters to narrow it with.
    /// </para>
    /// </summary>
    public sealed class RemoteConfigView : ServiceView
    {
        /// <summary>Shown in the table and in the group chips for the group whose key is blank.</summary>
        private const string DefaultGroup = "(default)";

        private const string ConfigSnippet =
@"// One call brings the whole config for this project, branch and player.
var op = sdk.RemoteConfig.LoadConfigAsync();
await op.Task();
if (!op.Result.IsSuccess) { return; }

foreach (FetchRemoteConfigResponse.RemoteConfigData group in op.Result.Data.configs)
{
    foreach (FetchRemoteConfigResponse.Field field in group.fields)
    {
        // field.key, field.name, field.fieldType, field.value
        Debug.Log(group.key + ""."" + field.key + "" = "" + field.value);
    }
}

// Every value arrives as a string; fieldType (Int / Float / Boolean / String) is what says
// how to read it — and a value the designer typed by hand may not parse at all.
int lives = int.TryParse(field.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
    ? n
    : 3;   // keep a build-time default for exactly this case

// The service also caches the FIRST group after the call, for the common single-group project:
RemoteConfig cached = sdk.RemoteConfig.Config;   // cached.Key, cached.Fields";

        private readonly List<ConfigRow> _rows = new List<ConfigRow>();
        private readonly List<GroupInfo> _groups = new List<GroupInfo>();

        private string _query = string.Empty;
        private VisualElement _host;

        public RemoteConfigView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            _query = string.Empty;
            // The previous pass' container is detached by Refresh(); keeping the reference would let a
            // later search paint into an orphaned subtree.
            _host = null;
            _rows.Clear();
            _groups.Clear();
            SetStatus(null);

            DeclareCall(new SdkCall("Read the remote config", ConfigSnippet,
                "Values always arrive as strings — fieldType is what tells you how to read one."));

            UseToolbar()
                .WithSearch("Search by key or name", OnSearch)
                .WithSpacer()
                .WithRefresh(Refresh);

            var slot = AddSlot(0f);
            ViewBind.Load(
                () => Sdk.RemoteConfig.LoadConfigAsync(),
                slot,
                BuildBody,
                data => CountFields(data) == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Remote config",
                    Snippet = ConfigSnippet,
                    ServiceName = "Remote Config",
                    // This *is* the service's configuration call, so a 404/501 really does mean
                    // "nothing has been published for this project".
                    ConfigurationRequest = true,
                    AllowRetry = true,
                    EmptyView = NoKeys,
                });
        }

        private void OnSearch(string text)
        {
            _query = text == null ? string.Empty : text.Trim();
            Render();
        }

        // ----- data -----------------------------------------------------------------------------

        private VisualElement BuildBody(FetchRemoteConfigResponse data)
        {
            Flatten(data);
            SetStatus(_rows.Count + (_rows.Count == 1 ? " key" : " keys") + " · "
                + _groups.Count + (_groups.Count == 1 ? " group" : " groups"), ChipTone.Ok);

            _host = new VisualElement();
            Render();
            return _host;
        }

        private VisualElement NoKeys()
        {
            SetStatus("Nothing published", ChipTone.Warn);
            return ZeroState.Table(Columns(),
                "Remote config is authored in the Mirra Hub console: you define groups of typed keys "
                + "there, publish them to this branch, and the client receives the groups that match "
                + "the player's segments. Nothing has reached this build yet.",
                4);
        }

        /// <summary>
        /// Turns the grouped response into one flat row list plus a per-group tally. Groups are kept
        /// separately because an empty group still says something ("published, nothing in it") and
        /// would otherwise vanish with its rows.
        /// </summary>
        private void Flatten(FetchRemoteConfigResponse data)
        {
            _rows.Clear();
            _groups.Clear();
            if (data == null || data.configs == null)
            {
                return;
            }

            foreach (var group in data.configs)
            {
                if (group == null)
                {
                    continue;
                }
                string name = string.IsNullOrWhiteSpace(group.key) ? DefaultGroup : group.key;
                int keys = 0;

                if (group.fields != null)
                {
                    foreach (var field in group.fields)
                    {
                        if (field == null)
                        {
                            continue;
                        }
                        _rows.Add(new ConfigRow
                        {
                            Group = name,
                            Key = field.key,
                            Name = field.name,
                            Value = field.value,
                            Type = field.fieldType,
                        });
                        keys++;
                    }
                }

                _groups.Add(new GroupInfo { Name = name, Keys = keys });
            }
        }

        private static int CountFields(FetchRemoteConfigResponse data)
        {
            if (data == null || data.configs == null)
            {
                return 0;
            }
            int total = 0;
            foreach (var group in data.configs)
            {
                if (group != null && group.fields != null)
                {
                    total += group.fields.Length;
                }
            }
            return total;
        }

        // ----- rendering ------------------------------------------------------------------------

        private void Render()
        {
            if (_host == null)
            {
                return;
            }
            _host.Clear();

            var hint = new Label("Every value below was resolved for the signed-in player: the server "
                + "sends only the groups their segments match, so another account can legitimately see "
                + "a different value under the same key.");
            hint.AddToClassList("sc-fs-hint");
            _host.Add(hint);

            var types = TypeCounts();
            _host.Add(new KpiRow()
                .Add("Keys", LucideIcon.Braces, Fmt.Number(_rows.Count))
                .Add("Types in use", LucideIcon.Binary, types.Count.ToString())
                .Add("Groups", LucideIcon.Layers, _groups.Count.ToString()));

            var donut = new DonutChart(150f);
            donut.AddToClassList("sc-rcfg-chart");
            donut.SetData(types)
                .SetCenter(Fmt.Number(_rows.Count), _rows.Count == 1 ? "key" : "keys")
                .SetEmptyText("No keys");
            _host.Add(donut);

            _host.Add(GroupChips());

            var filtered = Filter();
            _host.Add(new SectionHeader("Keys", filtered.Count == _rows.Count
                ? _rows.Count.ToString()
                : filtered.Count + " of " + _rows.Count));

            if (filtered.Count == 0)
            {
                _host.Add(ZeroState.Table(Columns(),
                    "No key or name contains \"" + Fmt.Truncate(_query, 24) + "\". Clear the search box "
                    + "to see the whole config again.",
                    3));
                return;
            }

            var table = new DataTable(Columns())
                .WithZebra()
                .WithMaxHeight(520f)
                .WithRowClick(o => ShowField((ConfigRow)o))
                .WithSort(0, true);
            table.Bind(filtered);
            _host.Add(table);
        }

        private List<ConfigRow> Filter()
        {
            if (_query.Length == 0)
            {
                return new List<ConfigRow>(_rows);
            }

            var kept = new List<ConfigRow>();
            foreach (var row in _rows)
            {
                if (Contains(row.Key, _query) || Contains(row.Name, _query))
                {
                    kept.Add(row);
                }
            }
            return kept;
        }

        private static bool Contains(string haystack, string needle)
        {
            return !string.IsNullOrEmpty(haystack)
                && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Key counts per field type, in enum order so the donut colors stay put between
        /// loads. Types nobody used are left out rather than drawn as zero slices.</summary>
        private List<ChartPoint> TypeCounts()
        {
            var order = new[]
            {
                RemoteConfigFieldType.Int,
                RemoteConfigFieldType.Float,
                RemoteConfigFieldType.Boolean,
                RemoteConfigFieldType.String,
            };

            var points = new List<ChartPoint>();
            foreach (var type in order)
            {
                int count = 0;
                foreach (var row in _rows)
                {
                    if (row.Type == type)
                    {
                        count++;
                    }
                }
                if (count > 0)
                {
                    points.Add(new ChartPoint(type.ToString(), count));
                }
            }
            return points;
        }

        private VisualElement GroupChips()
        {
            var row = new VisualElement();
            row.AddToClassList("sc-chip-row");
            row.AddToClassList("sc-rcfg-groups");

            foreach (var group in _groups)
            {
                row.Add(new Chip(group.Name + " · " + group.Keys,
                    group.Keys == 0 ? ChipTone.Warn : ChipTone.Neutral));
            }
            return row;
        }

        private DataColumn[] Columns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "KEY", Grow = 1.6f,
                    SortKey = o => ((ConfigRow)o).Key,
                    Cell = o =>
                    {
                        var row = (ConfigRow)o;
                        var box = new VisualElement();
                        box.AddToClassList("sc-rcfg-cell");

                        var key = new Label(Fmt.OrDash(row.Key));
                        key.enableRichText = false;
                        key.AddToClassList("sc-rcfg-key");
                        box.Add(key);

                        if (!string.IsNullOrWhiteSpace(row.Name) && row.Name != row.Key)
                        {
                            var name = new Label(Fmt.Truncate(row.Name, 40));
                            name.enableRichText = false;
                            name.AddToClassList("sc-rcfg-name");
                            box.Add(name);
                        }
                        return box;
                    },
                },
                new DataColumn
                {
                    Header = "TYPE", FixedWidth = true, Px = 96, Align = "center",
                    SortKey = o => ((ConfigRow)o).Type.ToString(),
                    Cell = o =>
                    {
                        var row = (ConfigRow)o;
                        return new Chip(row.Type.ToString(), ToneFor(row.Type));
                    },
                },
                new DataColumn
                {
                    Header = "VALUE", Grow = 2.2f,
                    SortKey = o => ((ConfigRow)o).Value,
                    Cell = o =>
                    {
                        var row = (ConfigRow)o;
                        string value = row.Value ?? string.Empty;
                        var label = new Label(value.Length == 0 ? Fmt.Dash : Fmt.Truncate(value, 64));
                        label.enableRichText = false;
                        label.AddToClassList("sc-rcfg-value");
                        // The cell truncates; the tooltip is the only place the untouched value is
                        // readable without opening the dialog.
                        label.tooltip = value;
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "GROUP", FixedWidth = true, Px = 150,
                    SortKey = o => ((ConfigRow)o).Group,
                    Cell = o => new Badge(Fmt.Truncate(((ConfigRow)o).Group, 18), ChipTone.Neutral),
                },
            };
        }

        private static ChipTone ToneFor(RemoteConfigFieldType type)
        {
            switch (type)
            {
                case RemoteConfigFieldType.Boolean: return ChipTone.Info;
                case RemoteConfigFieldType.Int:
                case RemoteConfigFieldType.Float: return ChipTone.Accent;
                default: return ChipTone.Neutral;
            }
        }

        // ----- one key --------------------------------------------------------------------------

        private void ShowField(ConfigRow row)
        {
            if (Popup == null || row == null)
            {
                return;
            }

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.style.maxHeight = 460f;

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Key", Fmt.OrDash(row.Key), row.Key));
            kv.Add(Kv("Name", Fmt.OrDash(row.Name), null));
            kv.Add(Kv("Type", row.Type.ToString(), null));
            kv.Add(Kv("Group", Fmt.OrDash(row.Group), null));
            kv.Add(Kv("Reads as", Reading(row), null));
            body.Add(kv);

            body.Add(new SectionHeader("Value"));
            // JsonViewer over a plain string is deliberate: a config value is often a JSON document,
            // and for the ones that are not it still gives the full text plus a copy button.
            body.Add(new JsonViewer().SetRaw(row.Value ?? string.Empty).SetMaxLines(20));

            Popup.Open(body, Fmt.Truncate(Fmt.OrDash(row.Key), 34));
        }

        /// <summary>
        /// What the typed getter in a game would end up with. Worth showing: the wire format is always
        /// a string, so a value typed in the console can be published and only fail to parse in the
        /// client — and that failure is silent unless someone looks for it.
        /// </summary>
        private static string Reading(ConfigRow row)
        {
            string raw = row.Value == null ? string.Empty : row.Value.Trim();
            switch (row.Type)
            {
                case RemoteConfigFieldType.Int:
                    int i;
                    return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)
                        ? i.ToString(CultureInfo.InvariantCulture)
                        : "not a valid Int — the game falls back to its default";

                case RemoteConfigFieldType.Float:
                    float f;
                    return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out f)
                        ? f.ToString("0.####", CultureInfo.InvariantCulture)
                        : "not a valid Float — the game falls back to its default";

                case RemoteConfigFieldType.Boolean:
                    bool b;
                    if (bool.TryParse(raw, out b))
                    {
                        return b ? "true" : "false";
                    }
                    // "1"/"0" is a common hand-typed spelling and bool.TryParse rejects it, so it is
                    // reported as what it is rather than as a broken value.
                    if (raw == "1" || raw == "0")
                    {
                        return raw == "1" ? "true (from \"1\")" : "false (from \"0\")";
                    }
                    return "not a valid Boolean — the game falls back to its default";

                default:
                    return raw.Length == 0 ? "an empty string" : Fmt.Truncate(raw, 60);
            }
        }

        private VisualElement Kv(string key, string value, string copyable)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-kv");

            var k = new Label(key);
            k.AddToClassList("sc-kv__k");
            row.Add(k);

            var v = new Label(value);
            v.enableRichText = false;
            v.AddToClassList("sc-kv__v");
            row.Add(v);

            if (!string.IsNullOrEmpty(copyable))
            {
                row.Add(new CopyButton(copyable, Toasts));
            }
            return row;
        }

        /// <summary>One config field plus the group it was delivered in — the table's row object.</summary>
        private sealed class ConfigRow
        {
            public string Group;
            public string Key;
            public string Name;
            public string Value;
            public RemoteConfigFieldType Type;
        }

        /// <summary>A delivered group and how many keys it carried (zero is a valid, telling answer).</summary>
        private sealed class GroupInfo
        {
            public string Name;
            public int Keys;
        }
    }
}
