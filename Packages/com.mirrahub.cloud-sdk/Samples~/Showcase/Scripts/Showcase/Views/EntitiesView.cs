using System;
using System.Collections.Generic;
using MirraCloud.Core.Entities.Dto;
using MirraCloud.Json;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Entities screen: the whole entity-config snapshot of a branch, browsed as a tree of types over
    /// a table of keys, with the full definition of any row one click away.
    /// <para>
    /// One call fills the screen and the SDK's own cache, which is what a game reads from afterwards
    /// (<c>GetConfig&lt;T&gt;</c>). The snapshot carries no type field, so the tree derives one — from
    /// the namespace in front of a key, or from the shape of the fields, whichever the reader picks.
    /// Read-only: entities are authored in the console, never from the client.
    /// </para>
    /// </summary>
    public sealed class EntitiesView : ServiceView
    {
        private const string ConfigsSnippet =
@"// The whole snapshot for this branch in one call: every config, keyed by the key a designer
// gave it.
var op = sdk.Entities.GetConfigsAsync();
await op.Task();

var result = op.Result;
if (result.IsSuccess)
{
    EntitiesConfigsSnapshotDto snapshot = result.Data;
    foreach (var pair in snapshot.Configs)
    {
        string key = pair.Key;
        EntityConfigSdkDto config = pair.Value;
        // config.StableId, config.Name, config.Fields (JsonValue), config.Components
    }
}";

        private const string TypedSnippet =
@"// The call above also fills the service's cache, and that is what gameplay code reads: the
// raw JsonValue is mapped onto your own class instead of being walked by hand.
public sealed class GoblinConfig
{
    public int hp;
    public float speed;
}

GoblinConfig goblin = sdk.Entities.GetConfig<GoblinConfig>(""enemy_goblin"");   // throws if absent
if (sdk.Entities.TryGetConfig<GoblinConfig>(""enemy_goblin"", out var maybe))
{
}

// One named component off the same config, mapped the same way:
sdk.Entities.TryGetComponent<LootTable>(""enemy_goblin"", ""loot"", out var loot);

// Raw access, the cached snapshot, and dropping it:
sdk.Entities.TryGetConfigRaw(""enemy_goblin"", out EntityConfigSdkDto raw);
IReadOnlyDictionary<string, EntityConfigSdkDto> all = sdk.Entities.Configs;
sdk.Entities.ClearCache();";

        private const string GroupNamespace = "Type";
        private const string GroupShape = "Field shape";
        private const string GroupNone = "Flat";

        private const string NoNamespace = "(no namespace)";
        private const string NoFields = "(no fields)";

        // Designers namespace a key by what the entity is ("enemy/goblin", "weapon.sword",
        // "enemy_goblin"), which is the closest thing to a type the snapshot actually carries.
        private static readonly char[] NamespaceSeparators = { '/', '.', ':', '_', '-' };

        private readonly HashSet<string> _collapsed = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<Entry> _entries = new List<Entry>();

        private VisualElement _bodySlot;
        private string _search = string.Empty;
        private string _grouping = GroupNamespace;

        public EntitiesView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            _search = string.Empty;
            _grouping = GroupNamespace;
            _collapsed.Clear();
            _entries.Clear();
            _bodySlot = null;

            DeclareCall(new SdkCall("Read the entity snapshot", ConfigsSnippet,
                "The only request this screen makes; everything below is that one response."));
            DeclareCall(new SdkCall("Read a config as a typed object", TypedSnippet,
                "How a game consumes the snapshot. This screen renders the raw JSON instead, because it "
                + "cannot know your classes."));

            UseToolbar()
                .WithSearch("Search configs by key or name", OnSearch)
                .WithFilter("Group by", new[] { GroupNamespace, GroupShape, GroupNone }, OnGrouping,
                    GroupNamespace)
                .WithSpacer()
                .WithRefresh(Refresh);

            var slot = AddSlot();
            ViewBind.Load(
                () => Sdk.Entities.GetConfigsAsync(),
                slot,
                BuildSnapshot,
                snapshot => snapshot == null || snapshot.Configs == null || snapshot.Configs.Count == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Entity configs",
                    Snippet = ConfigsSnippet,
                    ServiceName = "Entities",
                    ConfigurationRequest = true,
                    AllowRetry = true,
                    EmptyView = NothingAuthored,
                });
        }

        private static VisualElement NothingAuthored()
        {
            return ZeroState.Panel(LucideIcon.Boxes, "This branch describes no entities",
                "Entities are authored in the Mirra Hub console: a template declares the fields an entity "
                + "type has, and a config fills them in under a key. Once configs exist on this branch they "
                + "arrive here as one snapshot, and the game reads them by key.",
                hint: "Nothing is created from the client — the SDK only reads the snapshot.");
        }

        // ----- snapshot -----------------------------------------------------------------------

        private VisualElement BuildSnapshot(EntitiesConfigsSnapshotDto snapshot)
        {
            Index(snapshot);
            SetStatus(_entries.Count + (_entries.Count == 1 ? " config" : " configs"),
                _entries.Count > 0 ? ChipTone.Ok : ChipTone.Neutral);

            var root = new VisualElement();
            _bodySlot = new VisualElement();
            root.Add(_bodySlot);
            RenderBody();
            return root;
        }

        /// <summary>Flattens the snapshot into rows sorted by key, so the tree, the chart and the
        /// tables all walk the same list in the same order.</summary>
        private void Index(EntitiesConfigsSnapshotDto snapshot)
        {
            _entries.Clear();
            if (snapshot == null || snapshot.Configs == null)
            {
                return;
            }

            foreach (var pair in snapshot.Configs)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                {
                    continue;
                }
                _entries.Add(new Entry
                {
                    Key = pair.Key,
                    Config = pair.Value,
                    Namespace = NamespaceOf(pair.Key),
                    Shape = ShapeOf(pair.Value.Fields),
                    Fields = FieldCount(pair.Value.Fields),
                    Components = pair.Value.Components != null ? pair.Value.Components.Length : 0,
                });
            }

            _entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        }

        private void OnSearch(string text)
        {
            _search = text == null ? string.Empty : text.Trim();
            RenderBody();
        }

        private void OnGrouping(string value)
        {
            _grouping = string.IsNullOrEmpty(value) ? GroupNamespace : value;
            // Group names mean something different under each mode, so a collapsed "weapon" must not
            // silently collapse a shape that happens to be called the same.
            _collapsed.Clear();
            RenderBody();
        }

        private void RenderBody()
        {
            if (_bodySlot == null)
            {
                return;
            }
            _bodySlot.Clear();

            // Reachable when the snapshot answers with entries the client cannot use (a null config,
            // a blank key). It is not an empty response, so ViewBind rendered instead of showing its
            // empty view — this screen has to say the same thing itself.
            if (_entries.Count == 0)
            {
                _bodySlot.Add(NothingAuthored());
                return;
            }

            var matched = Matched();
            var groups = Bucket(matched);

            _bodySlot.Add(Kpis());
            _bodySlot.Add(Caption());

            if (groups.Count > 1)
            {
                _bodySlot.Add(GroupChart(groups));
            }

            _bodySlot.Add(Headline(matched.Count));

            if (matched.Count == 0)
            {
                _bodySlot.Add(ZeroState.Table(EntryColumns(),
                    "No config on this branch matches \"" + Fmt.Truncate(_search, 24)
                    + "\". The search looks at the config key and its display name.",
                    3, "Clear the search", Refresh));
                return;
            }

            // One table per group keeps each type's rows sortable on their own, which is what makes
            // the grouping useful rather than decorative.
            bool dense = groups.Count > 6;
            foreach (var group in groups)
            {
                _bodySlot.Add(GroupBlock(group, dense));
            }
        }

        private List<Entry> Matched()
        {
            var matched = new List<Entry>();
            foreach (var entry in _entries)
            {
                if (_search.Length == 0 || Contains(entry.Key) || Contains(entry.Config.Name))
                {
                    matched.Add(entry);
                }
            }
            return matched;
        }

        private bool Contains(string value)
        {
            return !string.IsNullOrEmpty(value)
                   && value.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ----- kpis and chart -----------------------------------------------------------------

        private VisualElement Kpis()
        {
            var kpis = new KpiRow();

            int total = _entries.Count;
            int types = DistinctGroups();
            int fields = 0;
            int components = 0;
            foreach (var entry in _entries)
            {
                fields += entry.Fields;
                components += entry.Components;
            }

            if (total == 0)
            {
                kpis.AddZero("Entities", LucideIcon.Boxes)
                    .AddZero("Types", LucideIcon.ListTree)
                    .AddZero("Field keys", LucideIcon.Braces)
                    .AddZero("Components", LucideIcon.Component);
                return kpis;
            }

            kpis.Add("Entities", LucideIcon.Boxes, Fmt.Number(total))
                .Add("Types", LucideIcon.ListTree, types.ToString())
                .Add("Field keys", LucideIcon.Braces, Fmt.Number(fields))
                .Add("Components", LucideIcon.Component, components.ToString());
            return kpis;
        }

        private Label Caption()
        {
            var caption = new Label(_grouping == GroupShape
                ? "Grouped by field shape: the sorted list of top-level field names in a config. Configs "
                + "built from the same template expose the same fields, so a shape stands in for the "
                + "template the snapshot does not name."
                : "The snapshot carries no type field, so a type here is the text in front of the first "
                + "/ . : _ or - in a config key — the namespace designers give an entity kind. Switch "
                + "\"Group by\" to compare field shapes instead.");
            caption.AddToClassList("sc-fs-hint");
            return caption;
        }

        private VisualElement GroupChart(List<TypeGroup> groups)
        {
            var box = new VisualElement();
            box.AddToClassList("sc-ent-chart");

            var points = new List<ChartPoint>();
            int shown = 0;
            foreach (var group in groups)
            {
                if (shown >= 12)
                {
                    break;
                }
                points.Add(new ChartPoint(group.Name, group.Entries.Count));
                shown++;
            }

            var chart = new BarChart(170f);
            chart.SetData(points)
                .SetAccent(Meta.Accent)
                .SetEmptyText("Nothing to break down");
            box.Add(chart);

            if (groups.Count > shown)
            {
                var more = new Label("Showing the " + shown + " largest of " + groups.Count
                    + " groups; the tree below has all of them.");
                more.AddToClassList("sc-fs-hint");
                box.Add(more);
            }
            return box;
        }

        private VisualElement Headline(int matchedCount)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-ent-headline");

            string count = matchedCount == _entries.Count
                ? _entries.Count.ToString()
                : matchedCount + " of " + _entries.Count;
            row.Add(new SectionHeader("Configs", count));
            row.Add(new InfoHint("This snapshot has no per-config version: a config's identity across "
                + "renames is its stable id, which is what references between entities point at. The "
                + "snapshot as a whole is versioned on the server, and the SDK does not surface that "
                + "revision — re-issue the call to pick up a newer one."));
            return row;
        }

        // ----- tree ---------------------------------------------------------------------------

        private int DistinctGroups()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in _entries)
            {
                names.Add(NameOf(entry));
            }
            return names.Count;
        }

        private string NameOf(Entry entry)
        {
            return _grouping == GroupShape ? entry.Shape : entry.Namespace;
        }

        /// <summary>Buckets the rows for the current mode, largest group first (which is also the
        /// order the chart bars use). "Flat" is one bucket holding everything.</summary>
        private List<TypeGroup> Bucket(List<Entry> entries)
        {
            var groups = new List<TypeGroup>();
            if (entries.Count == 0)
            {
                return groups;
            }

            if (_grouping == GroupNone)
            {
                groups.Add(new TypeGroup { Name = "All configs", Entries = entries });
                return groups;
            }

            var byName = new Dictionary<string, TypeGroup>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                string name = NameOf(entry);
                TypeGroup group;
                if (!byName.TryGetValue(name, out group))
                {
                    group = new TypeGroup { Name = name, Entries = new List<Entry>() };
                    byName[name] = group;
                    groups.Add(group);
                }
                group.Entries.Add(entry);
            }

            groups.Sort((a, b) =>
            {
                int size = b.Entries.Count.CompareTo(a.Entries.Count);
                return size != 0 ? size : string.CompareOrdinal(a.Name, b.Name);
            });
            return groups;
        }

        private VisualElement GroupBlock(TypeGroup group, bool dense)
        {
            var box = new VisualElement();
            box.AddToClassList("sc-ent-group");

            bool open = !_collapsed.Contains(group.Name);
            bool flat = _grouping == GroupNone;

            if (!flat)
            {
                box.Add(GroupHead(group, open));
            }

            if (!open)
            {
                return box;
            }

            var table = new DataTable(EntryColumns())
                .WithZebra()
                .WithSort(0, true)
                .WithRowClick(row => ShowConfig((Entry)row));
            if (dense)
            {
                // With many groups on screen, a full-height table per group buries the ones below it.
                table.WithMaxHeight(240f);
            }
            table.Bind(group.Entries);
            box.Add(table);
            return box;
        }

        private VisualElement GroupHead(TypeGroup group, bool open)
        {
            var head = new VisualElement();
            head.AddToClassList("sc-ent-group__head");

            var chevron = new Label(open ? LucideIcon.ChevronDown : LucideIcon.ChevronRight);
            chevron.AddToClassList("sc-ent-group__chev");
            chevron.AddToClassList("sc-icon");
            head.Add(chevron);

            var glyph = new Label(LucideIcon.FolderTree);
            glyph.AddToClassList("sc-ent-group__glyph");
            glyph.AddToClassList("sc-icon");
            glyph.style.color = Meta.Accent;
            head.Add(glyph);

            var title = new Label(Fmt.Truncate(group.Name, 52));
            title.enableRichText = false;
            title.tooltip = group.Name;
            title.AddToClassList("sc-ent-group__title");
            head.Add(title);

            var count = new Badge(group.Entries.Count
                + (group.Entries.Count == 1 ? " config" : " configs"), ChipTone.Neutral);
            head.Add(count);

            string name = group.Name;
            head.RegisterCallback<ClickEvent>(_ => Toggle(name));
            return head;
        }

        private void Toggle(string groupName)
        {
            if (!_collapsed.Remove(groupName))
            {
                _collapsed.Add(groupName);
            }
            RenderBody();
        }

        // ----- table --------------------------------------------------------------------------

        private DataColumn[] EntryColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "KEY", Grow = 2.4f,
                    SortKey = o => ((Entry)o).Key,
                    Cell = KeyCell,
                },
                new DataColumn
                {
                    Header = "FIELDS", FixedWidth = true, Px = 84, Align = "right",
                    SortKey = o => ((Entry)o).Fields,
                    Cell = o => new Label(((Entry)o).Fields.ToString()),
                },
                new DataColumn
                {
                    Header = "COMPONENTS", FixedWidth = true, Px = 116, Align = "right",
                    SortKey = o => ((Entry)o).Components,
                    Cell = o =>
                    {
                        int components = ((Entry)o).Components;
                        return new Label(components == 0 ? Fmt.Dash : components.ToString());
                    },
                },
                new DataColumn
                {
                    Header = "STABLE ID", FixedWidth = true, Px = 168, Align = "right",
                    SortKey = o => ((Entry)o).Config.StableId,
                    Cell = StableIdCell,
                },
            };
        }

        private static VisualElement KeyCell(object row)
        {
            var entry = (Entry)row;

            var box = new VisualElement();
            var key = new Label(entry.Key);
            key.enableRichText = false;
            key.AddToClassList("sc-ent-key");
            box.Add(key);

            // The display name only earns a line when it says something the key does not.
            string name = entry.Config.Name;
            if (!string.IsNullOrEmpty(name) && !string.Equals(name, entry.Key, StringComparison.Ordinal))
            {
                var label = new Label(Fmt.Truncate(name, 64));
                label.enableRichText = false;
                label.AddToClassList("sc-ent-name");
                box.Add(label);
            }
            return box;
        }

        private VisualElement StableIdCell(object row)
        {
            var entry = (Entry)row;

            var box = new VisualElement();
            box.AddToClassList("sc-row-actions");

            var id = new Label(Fmt.Id(entry.Config.StableId, 10));
            id.enableRichText = false;
            id.AddToClassList("sc-ent-id");
            box.Add(id);

            if (!string.IsNullOrEmpty(entry.Config.StableId))
            {
                box.Add(new CopyButton(entry.Config.StableId, Toasts));
            }
            return box;
        }

        // ----- one config ---------------------------------------------------------------------

        private void ShowConfig(Entry entry)
        {
            if (Popup == null || entry == null)
            {
                return;
            }

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.style.maxHeight = 460f;

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip(entry.Fields + (entry.Fields == 1 ? " field" : " fields"), ChipTone.Accent));
            chips.Add(entry.Components == 0
                ? new Chip("no components", ChipTone.Neutral)
                : new Chip(entry.Components + (entry.Components == 1 ? " component" : " components"),
                    ChipTone.Info));
            body.Add(chips);

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Key", entry.Key, entry.Key));
            kv.Add(Kv("Name", Fmt.OrDash(entry.Config.Name), entry.Config.Name));
            kv.Add(Kv("Stable id", Fmt.OrDash(entry.Config.StableId), entry.Config.StableId));
            kv.Add(Kv(_grouping == GroupShape ? "Field shape" : "Type",
                Fmt.Truncate(NameOf(entry), 48), null));
            body.Add(kv);

            body.Add(new SectionHeader("Fields"));
            if (entry.Fields == 0)
            {
                body.Add(ZeroState.Panel(LucideIcon.Braces, "No fields on this config",
                    "The template behind it declares none, or every value it declares was left unset. A "
                    + "config with no fields still resolves by key, it just maps onto an empty object."));
            }
            else
            {
                var typed = new Label("This is the document GetConfig<T>() maps onto your class: the field "
                    + "names have to line up with your members for the mapping to fill them in.");
                typed.AddToClassList("sc-fs-hint");
                body.Add(typed);
                body.Add(new JsonViewer().SetRaw(Pretty(entry.Config.Fields)).SetMaxLines(24));
            }

            body.Add(new SectionHeader("Components", entry.Components.ToString()));
            if (entry.Components == 0)
            {
                body.Add(ZeroState.Panel(LucideIcon.Component, "No components",
                    "A component is a named block of data hanging off a config — a loot table, a spawn "
                    + "rule — read on its own with TryGetComponent<T>(key, componentKey, out …)."));
            }
            else
            {
                foreach (var component in entry.Config.Components)
                {
                    if (component == null)
                    {
                        continue;
                    }
                    body.Add(ComponentBlock(component));
                }
            }

            Popup.Open(body, Fmt.Truncate(entry.Key, 34));
        }

        private VisualElement ComponentBlock(EntityComponentSdkDto component)
        {
            var box = new VisualElement();
            box.AddToClassList("sc-ent-component");

            box.Add(new SectionHeader(string.IsNullOrEmpty(component.Key) ? "(unnamed)" : component.Key));

            if (!string.IsNullOrEmpty(component.TypeStableId))
            {
                var chips = new VisualElement();
                chips.AddToClassList("sc-chip-row");
                chips.Add(new Chip("type " + Fmt.Id(component.TypeStableId, 10), ChipTone.Neutral));
                chips.Add(new CopyButton(component.TypeStableId, Toasts, "type id"));
                box.Add(chips);
            }

            box.Add(new JsonViewer().SetRaw(Pretty(component.Data)).SetMaxLines(14));
            return box;
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

        // ----- json helpers -------------------------------------------------------------------

        private static int FieldCount(JsonValue fields)
        {
            // JsonValue.Keys/Count assume the value really is an object; anything else (a bare array
            // or scalar arriving where a document is expected) has no field keys to count.
            return fields != null && fields.Type == JsonValueType.Object ? fields.Count : 0;
        }

        private static string NamespaceOf(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return NoNamespace;
            }
            int cut = key.IndexOfAny(NamespaceSeparators);
            if (cut <= 0)
            {
                return NoNamespace;
            }
            return key.Substring(0, cut);
        }

        private static string ShapeOf(JsonValue fields)
        {
            if (fields == null || fields.Type != JsonValueType.Object || fields.Count == 0)
            {
                return NoFields;
            }

            var names = new List<string>(fields.Count);
            foreach (var name in fields.Keys)
            {
                names.Add(name);
            }
            names.Sort(StringComparer.Ordinal);
            return string.Join(" · ", names.ToArray());
        }

        // Fmt.Json deliberately summarises a tree ("{ 4 keys }"); this dialog is here for the actual
        // document, so it serializes instead.
        private static string Pretty(JsonValue value)
        {
            if (value == null)
            {
                return "null";
            }
            try
            {
                return new JsonService().ToJson(value, true);
            }
            catch (Exception e)
            {
                return "// could not render: " + e.Message;
            }
        }

        /// <summary>One row of the tables: the snapshot's key/value pair plus everything derived from
        /// it, so sorting and grouping never re-walk the JSON.</summary>
        private sealed class Entry
        {
            public string Key;
            public EntityConfigSdkDto Config;
            public string Namespace;
            public string Shape;
            public int Fields;
            public int Components;
        }

        /// <summary>One bucket of the tree. Named with a "2" because <c>Group</c> is the method that
        /// builds these.</summary>
        private sealed class TypeGroup
        {
            public string Name;
            public List<Entry> Entries;
        }
    }
}
