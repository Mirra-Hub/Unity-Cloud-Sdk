using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MirraCloud.Core;
using MirraCloud.Core.CloudSave;
using MirraCloud.Core.CloudSave.Requests;
using MirraCloud.Core.CloudSave.Responses;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Cloud Save screen: the three key/value scopes (the player's own data, project-wide global
    /// data, and a custom-id bucket), the file slots, and the index query.
    /// <para>
    /// The three scopes are the same shape with different endpoints, so one <see cref="Scope"/>
    /// switch drives read, write and delete for all of them rather than three near-copies.
    /// </para>
    /// <para>
    /// Files have no listing endpoint — every file call is addressed by key — so that tab is
    /// key-driven and says so, instead of showing an empty list that can never fill.
    /// </para>
    /// </summary>
    public sealed class CloudSaveView : ServiceView
    {
        private const string ReadSnippet =
@"// The player's own records. Keys, offset and limit are all optional.
var op = sdk.CloudSave.GetPlayerDataAsync();
await op.Task();

foreach (DataItemResponse d in op.Result.Data)
{
    // d.key, d.value (JsonValue), d.fieldType, d.readMask, d.writeMask,
    // d.updatedAtUtc, d.version
}

// Global is project-wide; custom addresses any bucket you name.
var global = sdk.CloudSave.LoadGlobalDataAsync();
var custom = sdk.CloudSave.LoadCustomDataAsync(""season_3"");

// Another player's data, subject to its read mask:
var theirs = sdk.CloudSave.GetOtherPlayerDataAsync(profileId);";

        private const string WriteSnippet =
@"// One request carries many keys. The builder picks the field type for you and the masks
// decide who may read and write each key afterwards.
var data = new CloudSaveDataRequest()
    .AddInt(""level"", 7)
    .AddFloat(""progress"", 0.42f)
    .AddBool(""tutorialDone"", true)
    .AddString(""nickname"", ""Ada"", readMask: AccessMask.Owner | AccessMask.Other);

await sdk.CloudSave.UpsertPlayerDataAsync(data).Task();
await sdk.CloudSave.SaveGlobalDataAsync(data).Task();
await sdk.CloudSave.SaveCustomDataAsync(""season_3"", data).Task();

// expectedVersion makes a write conditional — it fails if someone else wrote first.
var guarded = new CloudSaveDataRequest().AddInt(""level"", 8, expectedVersion: 3);";

        private const string DeleteSnippet =
@"// Delete takes keys, not whole scopes.
await sdk.CloudSave.DeletePlayerDataAsync(""level"", ""progress"").Task();
await sdk.CloudSave.DeleteGlobalDataAsync(""motd"").Task();
await sdk.CloudSave.DeleteCustomDataAsync(""season_3"", ""leaderboardSnapshot"").Task();";

        private const string QuerySnippet =
@"// Index queries run over an index defined in the console, not over arbitrary keys.
var request = new QueryIndexRequest
{
    indexId = ""by_level"",
    limit = 20,
    filters = { new QueryFilter { key = ""level"", op = CloudSaveIndexOp.GreaterThan, value = 5 } },
    returnKeys = new[] { ""level"", ""nickname"" }
};

var op = sdk.CloudSave.QueryPlayerDataAsync(request);
await op.Task();

foreach (QueryIndexItem item in op.Result.Data.items)
{
    // item.entityId, item.data
}
// op.Result.Data.sampled tells you whether the answer was sampled rather than exhaustive.";

        private const string FilesSnippet =
@"// A file slot is addressed by key; there is no ""list my files"" endpoint.
byte[] bytes = Encoding.UTF8.GetBytes(""save blob"");
var up = sdk.CloudSave.UploadPlayerFileAsync(""save1"", bytes, ""save1.json"", ""application/json"",
    meta: new Dictionary<string, string> { { ""slot"", ""1"" } });
await up.Task();

var info = sdk.CloudSave.GetPlayerFileAsync(""save1"");     // metadata, not the bytes
var url = sdk.CloudSave.GetPlayerFileUrlAsync(""save1"");   // a URL to download from

await sdk.CloudSave.UpdatePlayerFileMetaAsync(""save1"", newMeta).Task();
await sdk.CloudSave.UpdatePlayerFileContentAsync(""save1"", bytes, ""save1.json"", ""application/json"").Task();
await sdk.CloudSave.DeletePlayerFileAsync(""save1"").Task();

// Global files, and another player's files, mirror the same set.
var globalUp = sdk.CloudSave.UploadGlobalFileAsync(""motd"", bytes, ""motd.txt"", ""text/plain"");
var theirs = sdk.CloudSave.GetOtherPlayerFileAsync(profileId, ""save1"");";

        private enum Scope
        {
            Player,
            Global,
            Custom,
        }

        private Tabs _tabs;
        private string _customId = "demo";
        private string _fileScope = "Player";

        public CloudSaveView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            DeclareCall(new SdkCall("Read a scope", ReadSnippet));
            DeclareCall(new SdkCall("Write keys", WriteSnippet,
                "expectedVersion turns a write into a compare-and-set."));
            DeclareCall(new SdkCall("Delete keys", DeleteSnippet));
            DeclareCall(new SdkCall("Query an index", QuerySnippet,
                "The index has to exist in the console; this is not a free-form query."));
            DeclareCall(new SdkCall("Files", FilesSnippet,
                "Every file call is by key — the service has no listing endpoint."));

            UseToolbar().WithSpacer().WithRefresh(Refresh);

            _tabs = UseTabs();
            _tabs.Add("Player", LucideIcon.User, () => BuildScope(Scope.Player))
                .Add("Global", LucideIcon.Globe, () => BuildScope(Scope.Global))
                .Add("Custom", LucideIcon.Boxes, () => BuildScope(Scope.Custom))
                .Add("Files", LucideIcon.HardDrive, BuildFiles)
                .Add("Query", LucideIcon.FileSearch, BuildQuery);
        }

        // ----- scope plumbing -------------------------------------------------------------------

        private AsyncOperation<RestApiResult<DataItemResponse[]>> Read(Scope scope)
        {
            switch (scope)
            {
                case Scope.Global: return Sdk.CloudSave.LoadGlobalDataAsync();
                case Scope.Custom: return Sdk.CloudSave.LoadCustomDataAsync(_customId);
                default: return Sdk.CloudSave.GetPlayerDataAsync();
            }
        }

        private AsyncOperation<RestApiResult> Write(Scope scope, CloudSaveDataRequest data)
        {
            switch (scope)
            {
                case Scope.Global: return Sdk.CloudSave.SaveGlobalDataAsync(data);
                case Scope.Custom: return Sdk.CloudSave.SaveCustomDataAsync(_customId, data);
                default: return Sdk.CloudSave.UpsertPlayerDataAsync(data);
            }
        }

        private AsyncOperation<RestApiResult> Delete(Scope scope, string key)
        {
            switch (scope)
            {
                case Scope.Global: return Sdk.CloudSave.DeleteGlobalDataAsync(key);
                case Scope.Custom: return Sdk.CloudSave.DeleteCustomDataAsync(_customId, key);
                default: return Sdk.CloudSave.DeletePlayerDataAsync(key);
            }
        }

        private static int TabOf(Scope scope)
        {
            switch (scope)
            {
                case Scope.Global: return 1;
                case Scope.Custom: return 2;
                default: return 0;
            }
        }

        private static string NameOf(Scope scope)
        {
            switch (scope)
            {
                case Scope.Global: return "Global";
                case Scope.Custom: return "Custom";
                default: return "Player";
            }
        }

        // ----- scope tab ------------------------------------------------------------------------

        private VisualElement BuildScope(Scope scope)
        {
            var host = new VisualElement();

            if (scope == Scope.Custom)
            {
                var picker = new VisualElement();
                picker.AddToClassList("sc-chat-lookup");
                var field = new TextField { label = "Custom id", value = _customId };
                field.AddToClassList("sc-field");
                picker.Add(field);
                var load = new Button(() =>
                {
                    _customId = string.IsNullOrWhiteSpace(field.value) ? "demo" : field.value.Trim();
                    _tabs.Invalidate(2);
                })
                {
                    text = "Load",
                };
                load.AddToClassList("sc-btn");
                picker.Add(load);
                host.Add(picker);

                var hint = new Label("A custom bucket is any id you choose — a season, a guild, a live "
                    + "event. The same keys can exist independently under each one.");
                hint.AddToClassList("sc-fs-hint");
                host.Add(hint);
            }

            var header = new VisualElement();
            header.AddToClassList("sc-row-actions");
            header.style.justifyContent = Justify.SpaceBetween;
            header.Add(new SectionHeader(NameOf(scope) + " keys"));
            var add = new Button(() => OpenWriteDialog(scope, null)) { text = "Add a key" };
            add.AddToClassList("sc-btn");
            add.AddToClassList("sc-btn--primary");
            header.Add(add);
            host.Add(header);

            var slot = new VisualElement();
            host.Add(slot);
            ViewBind.Load(
                () => Read(scope),
                slot,
                rows => BuildScopeBody(scope, rows),
                d => d == null || d.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = NameOf(scope) + " data",
                    Snippet = ReadSnippet,
                    ServiceName = "Cloud Save",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Table(ScopeColumns(scope),
                        scope == Scope.Custom
                            ? "Nothing stored under \"" + _customId + "\" yet."
                            : "Nothing stored in this scope yet. A key appears here the moment the game "
                                + "writes it — the console does not have to declare it first.",
                        3, "Save a demo value", () => SaveDemo(scope)),
                });
            return host;
        }

        private VisualElement BuildScopeBody(Scope scope, DataItemResponse[] rows)
        {
            var col = new VisualElement();

            long bytes = 0L;
            var types = new Dictionary<CloudSaveFieldType, int>();
            foreach (var row in rows)
            {
                bytes += Raw(row.value).Length;
                int had;
                types.TryGetValue(row.fieldType, out had);
                types[row.fieldType] = had + 1;
            }

            if (scope == Scope.Player)
            {
                SetStatus(rows.Length + (rows.Length == 1 ? " key" : " keys"), ChipTone.Ok);
            }

            col.Add(new KpiRow()
                .Add("Keys", LucideIcon.Database, rows.Length.ToString())
                .Add("Value size", LucideIcon.HardDrive, Fmt.Bytes(bytes))
                .Add("Types", LucideIcon.Hash, types.Count.ToString()));

            var table = new DataTable(ScopeColumns(scope))
                .WithZebra()
                .WithMaxHeight(520f)
                .WithRowClick(o => OpenValue(scope, (DataItemResponse)o));
            table.Bind(rows);
            col.Add(table);
            return col;
        }

        private DataColumn[] ScopeColumns(Scope scope)
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "KEY", Grow = 1.2f,
                    SortKey = o => ((DataItemResponse)o).key,
                    Cell = o =>
                    {
                        var label = new Label(Fmt.OrDash(((DataItemResponse)o).key));
                        label.enableRichText = false;
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "TYPE", FixedWidth = true, Px = 90,
                    SortKey = o => ((DataItemResponse)o).fieldType.ToString(),
                    Cell = o => new Chip(((DataItemResponse)o).fieldType.ToString(), ChipTone.Info),
                },
                new DataColumn
                {
                    Header = "VALUE", Grow = 2f,
                    Cell = o =>
                    {
                        var label = new Label(Fmt.Truncate(Raw(((DataItemResponse)o).value), 70));
                        label.enableRichText = false;
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "ACCESS", FixedWidth = true, Px = 156,
                    Cell = o =>
                    {
                        var item = (DataItemResponse)o;
                        var box = new VisualElement();
                        box.AddToClassList("sc-row-actions");
                        box.style.justifyContent = Justify.FlexStart;
                        box.Add(new Badge("R " + item.readMask, ChipTone.Neutral));
                        box.Add(new Badge("W " + item.writeMask, ChipTone.Warn));
                        return box;
                    },
                },
                new DataColumn
                {
                    Header = "VER", FixedWidth = true, Px = 56, Align = "right",
                    SortKey = o => ((DataItemResponse)o).version,
                    Cell = o => new Label(((DataItemResponse)o).version.ToString()),
                },
                new DataColumn
                {
                    Header = string.Empty, FixedWidth = true, Px = 150, Align = "right",
                    Cell = o =>
                    {
                        var item = (DataItemResponse)o;
                        var box = new VisualElement();
                        box.AddToClassList("sc-row-actions");

                        var edit = new Button(() => OpenWriteDialog(scope, item)) { text = "Edit" };
                        edit.AddToClassList("sc-btn");
                        box.Add(edit);

                        var remove = new Button(() => ConfirmDeleteKey(scope, item.key)) { text = "Delete" };
                        remove.AddToClassList("sc-btn");
                        remove.AddToClassList("sc-btn--danger");
                        box.Add(remove);
                        return box;
                    },
                },
            };
        }

        /// <summary>
        /// The stored value as text. <c>Fmt.Json</c> deliberately summarises containers
        /// ("{ 3 keys }"), which is useless in a value column, so this serializes instead.
        /// </summary>
        private static string Raw(JsonValue value)
        {
            if (value == null)
            {
                return "null";
            }
            if (value.Type == JsonValueType.String)
            {
                return (string)value;
            }
            try
            {
                return new JsonService().ToJson(value);
            }
            catch (Exception)
            {
                return Fmt.Json(value);
            }
        }

        private void OpenValue(Scope scope, DataItemResponse item)
        {
            if (Popup == null)
            {
                return;
            }

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.style.maxHeight = 460f;

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Key", Fmt.OrDash(item.key), item.key));
            kv.Add(Kv("Type", item.fieldType.ToString(), null));
            kv.Add(Kv("Read mask", item.readMask.ToString(), null));
            kv.Add(Kv("Write mask", item.writeMask.ToString(), null));
            kv.Add(Kv("Version", item.version.ToString(), null));
            kv.Add(Kv("Updated", Fmt.OrDash(item.updatedAtUtc), null));
            body.Add(kv);

            body.Add(new SectionHeader("Value"));
            body.Add(new JsonViewer().SetRaw(Raw(item.value)).SetMaxLines(18));

            var actions = new VisualElement();
            actions.AddToClassList("sc-chip-row");
            var edit = new Button(() => OpenWriteDialog(scope, item)) { text = "Edit" };
            edit.AddToClassList("sc-btn");
            edit.AddToClassList("sc-btn--primary");
            actions.Add(edit);
            var remove = new Button(() => ConfirmDeleteKey(scope, item.key)) { text = "Delete" };
            remove.AddToClassList("sc-btn");
            remove.AddToClassList("sc-btn--danger");
            actions.Add(remove);
            body.Add(actions);

            Popup.Open(body, Fmt.Truncate(Fmt.OrDash(item.key), 34));
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

        // ----- writing --------------------------------------------------------------------------

        private void OpenWriteDialog(Scope scope, DataItemResponse existing)
        {
            if (Popup == null)
            {
                return;
            }

            bool editing = existing != null;
            var fields = new List<FormField>
            {
                FormField.Text("key", "Key", editing ? existing.key : null, true),
                FormField.Choice("type", "Type", new[] { "String", "Int", "Float", "Boolean" },
                    editing ? existing.fieldType.ToString() : "String"),
                FormField.LongText("value", "Value", editing ? Raw(existing.value) : null),
                FormField.Choice("read", "Readable by", MaskOptions(),
                    editing ? existing.readMask.ToString() : "Owner"),
                FormField.Choice("write", "Writable by", MaskOptions(),
                    editing ? existing.writeMask.ToString() : "Owner"),
            };

            if (editing)
            {
                // A conditional write is the point of `version`, so editing offers it rather than
                // silently overwriting whatever is there now.
                fields.Add(FormField.Bool("guard", "Only if still version " + existing.version, false));
            }

            FormDialog.Open(Popup, editing ? "Edit " + existing.key : "Add a key",
                fields.ToArray(), editing ? "Save" : "Add",
                values => SaveKey(scope, values, editing ? existing.version : (long?)null));
        }

        private static string[] MaskOptions()
        {
            return new[] { "Owner", "Owner, Other", "Owner, Server", "Owner, Other, Server" };
        }

        private static AccessMask ParseMask(string text)
        {
            var mask = AccessMask.Owner;
            if (string.IsNullOrEmpty(text))
            {
                return mask;
            }
            if (text.Contains("Other"))
            {
                mask |= AccessMask.Other;
            }
            if (text.Contains("Server"))
            {
                mask |= AccessMask.Server;
            }
            return mask;
        }

        private async void SaveKey(Scope scope, FormValues values, long? currentVersion)
        {
            string key = values.Text("key");
            string raw = values.Text("value");
            var read = ParseMask(values.Choice("read"));
            var write = ParseMask(values.Choice("write"));
            ulong? guard = values.Has("guard") && values.Bool("guard") && currentVersion.HasValue
                ? (ulong?)currentVersion.Value
                : null;

            var data = new CloudSaveDataRequest();
            switch (values.Choice("type"))
            {
                case "Int":
                {
                    int parsed;
                    if (!int.TryParse(raw, out parsed))
                    {
                        Fail("\"" + raw + "\" is not a whole number.");
                        return;
                    }
                    data.AddInt(key, parsed, read, write, guard);
                    break;
                }
                case "Float":
                {
                    float parsed;
                    if (!float.TryParse(raw, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out parsed))
                    {
                        Fail("\"" + raw + "\" is not a number.");
                        return;
                    }
                    data.AddFloat(key, parsed, read, write, guard);
                    break;
                }
                case "Boolean":
                {
                    bool parsed;
                    if (!bool.TryParse(raw, out parsed))
                    {
                        Fail("Use true or false.");
                        return;
                    }
                    data.AddBool(key, parsed, read, write, guard);
                    break;
                }
                default:
                    data.AddString(key, raw ?? string.Empty, read, write, guard);
                    break;
            }

            var outcome = await Await(Write(scope, data), "Cloud Save · write");
            if (!outcome.Ok)
            {
                Fail(outcome.Message);
                return;
            }

            if (Toasts != null)
            {
                Toasts.Ok("Saved " + key);
            }
            if (Popup != null)
            {
                Popup.Close();
            }
            _tabs.Invalidate(TabOf(scope));
        }

        private void Fail(string message)
        {
            if (Toasts != null)
            {
                Toasts.Fail("Not saved · " + message);
            }
        }

        private void ConfirmDeleteKey(Scope scope, string key)
        {
            if (Popup == null)
            {
                return;
            }
            ConfirmDialog.Open(Popup, "Delete key",
                "Removes \"" + key + "\" from the " + NameOf(scope).ToLowerInvariant()
                + " scope. Other keys are untouched.",
                "Delete",
                async () =>
                {
                    var outcome = await Await(Delete(scope, key), "Cloud Save · delete");
                    if (!outcome.Ok)
                    {
                        if (Toasts != null)
                        {
                            Toasts.Fail("Not deleted · " + outcome.Message);
                        }
                        return;
                    }
                    if (Toasts != null)
                    {
                        Toasts.Ok("Deleted " + key);
                    }
                    if (Popup != null)
                    {
                        Popup.Close();
                    }
                    _tabs.Invalidate(TabOf(scope));
                });
        }

        private async void SaveDemo(Scope scope)
        {
            var data = new CloudSaveDataRequest()
                .AddInt("demo_level", 7)
                .AddString("demo_note", "written from the Showcase example");

            var outcome = await Await(Write(scope, data), "Cloud Save · demo write");
            if (!outcome.Ok)
            {
                Fail(outcome.Message);
                return;
            }
            if (Toasts != null)
            {
                Toasts.Ok("Wrote two demo keys");
            }
            _tabs.Invalidate(TabOf(scope));
        }

        // ----- files ----------------------------------------------------------------------------

        private VisualElement BuildFiles()
        {
            var col = new VisualElement();

            var hint = new Label("A file slot is addressed by key and there is no listing endpoint, so "
                + "this tab looks one up rather than showing a browser. Uploads here are built from "
                + "typed text because the runtime has no file picker — a game would pass real bytes.");
            hint.AddToClassList("sc-fs-hint");
            col.Add(hint);

            var scopeRow = new VisualElement();
            scopeRow.AddToClassList("sc-chip-row");
            foreach (var name in new[] { "Player", "Global" })
            {
                string captured = name;
                var button = new Button(() =>
                {
                    _fileScope = captured;
                    _tabs.Invalidate(3);
                })
                {
                    text = name + " files",
                };
                button.AddToClassList("sc-btn");
                if (_fileScope == name)
                {
                    button.AddToClassList("sc-btn--primary");
                }
                scopeRow.Add(button);
            }
            col.Add(scopeRow);

            bool global = _fileScope == "Global";

            col.Add(new ActionCard("Look up a file",
                    "Reads the slot's metadata — size, mime type, masks — not its bytes.",
                    LucideIcon.FileSearch)
                .WithFields(FormField.Text("key", "File key", "save1", true))
                .WithSnippet(FilesSnippet)
                .OnRun("Read", v => FileInfo(global
                    ? Sdk.CloudSave.GetGlobalFileAsync(v.Text("key"))
                    : Sdk.CloudSave.GetPlayerFileAsync(v.Text("key")))));

            col.Add(new ActionCard("Get a download URL",
                    "Hands back a URL the game can stream the bytes from.", LucideIcon.Link)
                .WithFields(FormField.Text("key", "File key", "save1", true))
                .WithSnippet(FilesSnippet)
                .OnRun("Get URL", v => FileUrl(global
                    ? Sdk.CloudSave.GetGlobalFileUrlAsync(v.Text("key"))
                    : Sdk.CloudSave.GetPlayerFileUrlAsync(v.Text("key")))));

            col.Add(new ActionCard("Upload a file",
                    "Creates or replaces the slot, with optional metadata.", LucideIcon.Upload)
                .WithFields(
                    FormField.Text("key", "File key", "save1", true),
                    FormField.Text("name", "File name", "save1.json"),
                    FormField.Text("mime", "MIME type", "application/json"),
                    FormField.LongText("content", "Content", "{\n  \"slot\": 1\n}"),
                    FormField.Text("metaKey", "Metadata key (optional)"),
                    FormField.Text("metaValue", "Metadata value (optional)"))
                .WithSnippet(FilesSnippet)
                .OnRun("Upload", v => Upload(global, v)));

            col.Add(new ActionCard("Replace the content",
                    "Keeps the slot and its metadata, swaps the bytes.", LucideIcon.RefreshCw)
                .WithFields(
                    FormField.Text("key", "File key", "save1", true),
                    FormField.Text("name", "File name", "save1.json"),
                    FormField.Text("mime", "MIME type", "application/json"),
                    FormField.LongText("content", "Content", "{\n  \"slot\": 2\n}"))
                .WithSnippet(FilesSnippet)
                .OnRun("Replace", v => FileInfo(global
                    ? Sdk.CloudSave.UpdateGlobalFileContentAsync(v.Text("key"),
                        Bytes(v.Text("content")), v.Text("name"), v.Text("mime"))
                    : Sdk.CloudSave.UpdatePlayerFileContentAsync(v.Text("key"),
                        Bytes(v.Text("content")), v.Text("name"), v.Text("mime")))));

            col.Add(new ActionCard("Replace the metadata",
                    "Metadata is a flat string dictionary; this call overwrites all of it.",
                    LucideIcon.Tag)
                .WithFields(
                    FormField.Text("key", "File key", "save1", true),
                    FormField.Text("metaKey", "Metadata key", "slot", true),
                    FormField.Text("metaValue", "Metadata value", "1"))
                .WithSnippet(FilesSnippet)
                .OnRun("Update", v =>
                {
                    var meta = new Dictionary<string, string> { { v.Text("metaKey"), v.Text("metaValue") } };
                    return FileInfo(global
                        ? Sdk.CloudSave.UpdateGlobalFileMetaAsync(v.Text("key"), meta)
                        : Sdk.CloudSave.UpdatePlayerFileMetaAsync(v.Text("key"), meta));
                }));

            col.Add(new ActionCard("Delete the file", "Frees the slot for good.", LucideIcon.Trash)
                .WithFields(FormField.Text("key", "File key", "save1", true))
                .WithSnippet(FilesSnippet)
                .OnRun("Delete", async v =>
                {
                    var outcome = await Await(global
                        ? Sdk.CloudSave.DeleteGlobalFileAsync(v.Text("key"))
                        : Sdk.CloudSave.DeletePlayerFileAsync(v.Text("key")), "Cloud Save · delete file");
                    return outcome.Ok
                        ? ActionOutcome.Success("Deleted " + v.Text("key"))
                        : ActionOutcome.Failure(outcome.Message);
                }, true));

            col.Add(new ActionCard("Another player's file",
                    "Readable when the slot's mask allows it.", LucideIcon.Users)
                .WithFields(
                    FormField.Text("profileId", "Profile id", null, true),
                    FormField.Text("key", "File key", "save1", true))
                .WithSnippet(FilesSnippet)
                .OnRun("Read", v => FileInfo(
                    Sdk.CloudSave.GetOtherPlayerFileAsync(v.Text("profileId"), v.Text("key")))));

            return col;
        }

        private static byte[] Bytes(string text)
        {
            return Encoding.UTF8.GetBytes(text ?? string.Empty);
        }

        private Task<ActionOutcome> Upload(bool global, FormValues values)
        {
            Dictionary<string, string> meta = null;
            string metaKey = values.Text("metaKey");
            if (!string.IsNullOrWhiteSpace(metaKey))
            {
                meta = new Dictionary<string, string> { { metaKey.Trim(), values.Text("metaValue") } };
            }

            var bytes = Bytes(values.Text("content"));
            return FileInfo(global
                ? Sdk.CloudSave.UploadGlobalFileAsync(values.Text("key"), bytes, values.Text("name"),
                    values.Text("mime"), meta)
                : Sdk.CloudSave.UploadPlayerFileAsync(values.Text("key"), bytes, values.Text("name"),
                    values.Text("mime"), meta));
        }

        private async Task<ActionOutcome> FileInfo(AsyncOperation<RestApiResult<FileItemResponse>> op)
        {
            var outcome = await AwaitData(op, "Cloud Save · file");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            var file = op.Result.Data;
            if (file == null)
            {
                return ActionOutcome.Success("Done, with no file body in the answer");
            }

            var detail = new VisualElement();
            detail.AddToClassList("sc-kv-list");
            detail.Add(Kv("Key", Fmt.OrDash(file.key), file.key));
            detail.Add(Kv("Size", Fmt.Bytes(file.fileSize), null));
            detail.Add(Kv("MIME", Fmt.OrDash(file.mimeType), null));
            detail.Add(Kv("Extension", Fmt.OrDash(file.extension), null));
            detail.Add(Kv("Access", "R " + file.readMask + " · W " + file.writeMask, null));
            detail.Add(Kv("Updated", Fmt.OrDash(file.updatedAtUtc), null));
            if (file.meta != null)
            {
                foreach (var pair in file.meta)
                {
                    detail.Add(Kv("meta." + pair.Key, Fmt.OrDash(pair.Value), null));
                }
            }
            return ActionOutcome.Success(Fmt.Bytes(file.fileSize) + " in slot \"" + file.key + "\"", detail);
        }

        private async Task<ActionOutcome> FileUrl(AsyncOperation<RestApiResult<FileUrlResponse>> op)
        {
            var outcome = await AwaitData(op, "Cloud Save · file url");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            string url = op.Result.Data != null ? op.Result.Data.url : null;
            if (string.IsNullOrEmpty(url))
            {
                return ActionOutcome.Failure("The answer carried no URL.");
            }

            var box = new VisualElement();
            box.AddToClassList("sc-row-actions");
            box.style.justifyContent = Justify.FlexStart;
            var label = new Label(Fmt.Truncate(url, 60));
            label.enableRichText = false;
            box.Add(label);
            box.Add(new CopyButton(url, Toasts, "copy"));
            return ActionOutcome.Success("URL ready", box);
        }

        // ----- query ----------------------------------------------------------------------------

        private VisualElement BuildQuery()
        {
            var col = new VisualElement();

            var hint = new Label("An index query runs over an index defined in the console, so the index "
                + "id has to exist. Filters are key/operator/value triples; the answer can be sampled "
                + "rather than exhaustive, which the result reports.");
            hint.AddToClassList("sc-fs-hint");
            col.Add(hint);

            col.Add(new ActionCard("Run an index query",
                    "One filter here; the request takes a list, so a real game can send several.",
                    LucideIcon.FileSearch)
                .WithFields(
                    FormField.Choice("scope", "Scope", new[] { "Player", "Global", "Custom" }, "Player"),
                    FormField.Text("customId", "Custom id (Custom scope only)", "demo"),
                    FormField.Text("indexId", "Index id", null, true),
                    FormField.Text("filterKey", "Filter key"),
                    FormField.Choice("op", "Operator",
                        new[] { "Equal", "NotEqual", "GreaterThan", "GreaterThanOrEqual",
                            "LessThan", "LessThanOrEqual" }, "Equal"),
                    FormField.Text("filterValue", "Filter value"),
                    FormField.Text("returnKeys", "Return keys (comma-separated)"),
                    FormField.Int("limit", "Limit", 20))
                .WithSnippet(QuerySnippet)
                .OnRun("Query", RunQuery));

            return col;
        }

        private async Task<ActionOutcome> RunQuery(FormValues values)
        {
            var request = new QueryIndexRequest
            {
                indexId = values.Text("indexId"),
                limit = Math.Max(1, values.Int("limit")),
            };

            string filterKey = values.Text("filterKey");
            if (!string.IsNullOrWhiteSpace(filterKey))
            {
                CloudSaveIndexOp op;
                try
                {
                    op = (CloudSaveIndexOp)Enum.Parse(typeof(CloudSaveIndexOp), values.Choice("op"));
                }
                catch (Exception)
                {
                    op = CloudSaveIndexOp.Equal;
                }
                request.filters.Add(new QueryFilter
                {
                    key = filterKey.Trim(),
                    op = op,
                    // Numbers are sent as numbers so a range operator compares numerically rather
                    // than lexically; anything else goes as text.
                    value = Coerce(values.Text("filterValue")),
                });
            }

            string returnKeys = values.Text("returnKeys");
            if (!string.IsNullOrWhiteSpace(returnKeys))
            {
                var keys = new List<string>();
                foreach (var part in returnKeys.Split(','))
                {
                    string trimmed = part.Trim();
                    if (trimmed.Length > 0)
                    {
                        keys.Add(trimmed);
                    }
                }
                request.returnKeys = keys.ToArray();
            }

            string scope = values.Choice("scope");
            AsyncOperation<RestApiResult<QueryIndexResponse>> call;
            if (scope == "Global")
            {
                call = Sdk.CloudSave.QueryGlobalDataAsync(request);
            }
            else if (scope == "Custom")
            {
                string customId = values.Text("customId");
                call = Sdk.CloudSave.QueryCustomDataAsync(
                    string.IsNullOrWhiteSpace(customId) ? _customId : customId.Trim(), request);
            }
            else
            {
                call = Sdk.CloudSave.QueryPlayerDataAsync(request);
            }

            var outcome = await AwaitData(call, "Cloud Save · query");
            if (!outcome.Ok)
            {
                return ActionOutcome.Failure(outcome.Message);
            }

            var response = call.Result.Data;
            var items = response != null ? response.items : null;
            if (items == null || items.Length == 0)
            {
                return ActionOutcome.Success("The index matched nothing");
            }

            var box = new VisualElement();
            if (response.sampled)
            {
                var sampled = new Label("The server sampled this answer — it is not exhaustive.");
                sampled.AddToClassList("sc-fs-hint");
                box.Add(sampled);
            }

            foreach (var item in items)
            {
                var row = new ListRow();
                row.SetTitle(Fmt.Id(item.entityId, 14));
                row.SetSubtitle(Describe(item.data));
                box.Add(row);
            }
            return ActionOutcome.Success(items.Length + (items.Length == 1 ? " match" : " matches"), box);
        }

        private static object Coerce(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }
            string text = raw.Trim();
            int asInt;
            if (int.TryParse(text, out asInt))
            {
                return asInt;
            }
            double asDouble;
            if (double.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out asDouble))
            {
                return asDouble;
            }
            bool asBool;
            if (bool.TryParse(text, out asBool))
            {
                return asBool;
            }
            return text;
        }

        private static string Describe(DataItemResponse[] data)
        {
            if (data == null || data.Length == 0)
            {
                return "no returned keys";
            }
            var parts = new List<string>();
            foreach (var item in data)
            {
                parts.Add(item.key + " = " + Fmt.Truncate(Raw(item.value), 18));
            }
            return string.Join(" · ", parts.ToArray());
        }

        // ----- shared plumbing ------------------------------------------------------------------

        private async Task<Outcome> Await(AsyncOperation<RestApiResult> op, string label)
        {
            if (op == null)
            {
                return new Outcome { Ok = false, Message = "the call could not be started" };
            }
            await op.Task();
            return Fold(op.Result, label);
        }

        private async Task<Outcome> AwaitData<T>(AsyncOperation<RestApiResult<T>> op, string label)
        {
            if (op == null)
            {
                return new Outcome { Ok = false, Message = "the call could not be started" };
            }
            await op.Task();
            return Fold(op.Result, label);
        }

        private Outcome Fold(RestApiResult result, string label)
        {
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record(label, result);
            }
            if (result != null && result.IsSuccess)
            {
                return new Outcome { Ok = true };
            }
            string message = result != null && result.Error != null && !string.IsNullOrEmpty(result.Error.Message)
                ? result.Error.Message
                : "no response";
            return new Outcome { Ok = false, Message = message };
        }

        private struct Outcome
        {
            public bool Ok;
            public string Message;
        }
    }
}
