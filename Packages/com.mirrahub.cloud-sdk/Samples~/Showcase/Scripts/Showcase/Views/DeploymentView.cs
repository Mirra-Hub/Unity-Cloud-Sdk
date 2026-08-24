using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Plugins.MirraCloud.Core.Services.Deployment.Dto;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Deployment screen: what this build is configured to talk to, and which branch the server
    /// actually routes a given client version to.
    /// <para>
    /// The two are not the same thing, which is the point of the screen: the config asset names a
    /// branch, but a project can route builds by version, so what a player really gets is whatever
    /// <c>ResolveBranchAsync</c> answers.
    /// </para>
    /// </summary>
    public sealed class DeploymentView : ServiceView
    {
        private const string ResolveSnippet =
@"// Ask the server which branch this client version belongs on. Call it at startup, before
// anything else reads remote config or assets, and use the answer as your branch.
var op = sdk.Deployment.ResolveBranchAsync(Application.version);
await op.Task();

if (op.Result.IsSuccess)
{
    ResolveBranchResponseDto r = op.Result.Data;
    // r.branchId, r.branchName, r.buildVersion
}";

        private sealed class Resolution
        {
            public string Version;
            public string BranchName;
            public string BranchId;
            public string BuildVersion;
            public bool Ok;
            public string Error;
            public DateTime At;
        }

        private readonly List<Resolution> _history = new List<Resolution>();
        private VisualElement _historySlot;

        public DeploymentView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            _history.Clear();

            DeclareCall(new SdkCall("Resolve the branch for a version", ResolveSnippet,
                "The config asset names a branch; this call says which one the server will actually use."));

            UseToolbar().WithSpacer().WithRefresh(Refresh);

            Content.Add(new SectionHeader("This build"));
            Content.Add(BuildConfigCard());

            Content.Add(new SectionHeader("Resolve a version"));
            Content.Add(BuildResolveCard());

            Content.Add(new SectionHeader("Resolved in this session"));
            _historySlot = AddSlot();
            RenderHistory();
        }

        // ----- local configuration --------------------------------------------------------------

        private VisualElement BuildConfigCard()
        {
            string project = null;
            string branch = null;
            string url = null;
            string platform = null;
            try
            {
                var config = MirraCloud.Configuration.Load();
                if (config != null)
                {
                    project = config.ProjectId;
                    branch = config.BranchId;
                    url = config.Url;
                    platform = config.AnalyticsPlatformId;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] config load failed: " + e.Message);
            }

            bool configured = !string.IsNullOrWhiteSpace(project);
            SetStatus(configured ? "Configured" : "No project id", configured ? ChipTone.Ok : ChipTone.Bad);

            var card = new Card(Meta.Accent);
            card.WithTitle("Configuration asset", Meta.Accent);

            var hint = new Label("Read from Resources/Configuration.asset at startup. Everything the SDK "
                + "sends is scoped to this project and branch.");
            hint.AddToClassList("sc-fs-hint");
            card.Body.Add(hint);

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Project id", project, project));
            kv.Add(Kv("Branch", branch, branch));
            kv.Add(Kv("Backend URL", url, url));
            kv.Add(Kv("Analytics platform", platform, platform));
            kv.Add(Kv("Client version", Application.version, Application.version));
            card.Body.Add(kv);

            if (!configured)
            {
                card.Body.Add(ZeroState.Panel(LucideIcon.TriangleAlert, "No project id set",
                    "Fill ProjectId on the MirraCloud Configuration asset — without it every call in "
                    + "this example will fail before it leaves the client."));
            }
            return card;
        }

        // ----- resolve ---------------------------------------------------------------------------

        private VisualElement BuildResolveCard()
        {
            return new ActionCard("Resolve a client version",
                    "Maps a build version onto the branch the server routes it to. A project that ships "
                    + "several versions at once uses this so an old build keeps talking to the branch it "
                    + "was released against.", LucideIcon.GitBranch)
                .WithFields(FormField.Text("version", "Client version", Application.version, true))
                .WithSnippet(ResolveSnippet)
                .OnRun("Resolve", Resolve);
        }

        private async Task<ActionOutcome> Resolve(FormValues values)
        {
            string version = values.Text("version");
            var op = Sdk.Deployment.ResolveBranchAsync(version);
            if (op == null)
            {
                return ActionOutcome.Failure("The call could not be started.");
            }
            await op.Task();
            var result = op.Result;
            if (Ctx.Log != null && result != null)
            {
                Ctx.Log.Record("Resolve branch", result, ResolveSnippet);
            }

            var entry = new Resolution { Version = version, At = DateTime.Now };

            if (result == null || !result.IsSuccess || result.Data == null)
            {
                entry.Ok = false;
                entry.Error = result != null && result.Error != null && !string.IsNullOrEmpty(result.Error.Message)
                    ? result.Error.Message
                    : "no response";
                Remember(entry);
                return ActionOutcome.Failure(entry.Error);
            }

            var data = result.Data;
            entry.Ok = true;
            entry.BranchId = data.branchId;
            entry.BranchName = data.branchName;
            entry.BuildVersion = data.buildVersion;
            Remember(entry);

            var detail = new VisualElement();
            detail.AddToClassList("sc-kv-list");
            detail.Add(Kv("Branch name", data.branchName, data.branchName));
            detail.Add(Kv("Branch id", data.branchId, data.branchId));
            detail.Add(Kv("Build version", data.buildVersion, data.buildVersion));

            string configured = ConfiguredBranch();
            if (!string.IsNullOrEmpty(configured) && !string.IsNullOrEmpty(data.branchName)
                && !string.Equals(configured, data.branchName, StringComparison.OrdinalIgnoreCase))
            {
                // Worth calling out: the asset and the server disagree, which is exactly the case
                // this call exists for and the one that confuses people in the field.
                var note = new Label("The server routes this version to \"" + data.branchName
                    + "\", while the configuration asset says \"" + configured
                    + "\". A game should follow the resolved branch.");
                note.AddToClassList("sc-fs-hint");
                detail.Add(note);
            }

            if (Toasts != null)
            {
                Toasts.Ok("Resolved to " + Fmt.OrDash(data.branchName));
            }
            return ActionOutcome.Success("Routes to " + Fmt.OrDash(data.branchName), detail);
        }

        private static string ConfiguredBranch()
        {
            try
            {
                var config = MirraCloud.Configuration.Load();
                return config != null ? config.BranchId : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Remember(Resolution entry)
        {
            _history.Insert(0, entry);
            if (_history.Count > 20)
            {
                _history.RemoveAt(_history.Count - 1);
            }
            RenderHistory();
        }

        private void RenderHistory()
        {
            if (_historySlot == null)
            {
                return;
            }
            _historySlot.Clear();

            if (_history.Count == 0)
            {
                _historySlot.Add(ZeroState.Table(HistoryColumns(),
                    "Each resolve you run is kept here, so you can compare what different client "
                    + "versions map to without leaving the screen.", 3));
                return;
            }

            var table = new DataTable(HistoryColumns()).WithZebra().WithMaxHeight(360f);
            table.Bind(_history, o => !((Resolution)o).Ok);
            _historySlot.Add(table);
        }

        private static DataColumn[] HistoryColumns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "VERSION", Grow = 1f,
                    SortKey = o => ((Resolution)o).Version,
                    Cell = o =>
                    {
                        var label = new Label(Fmt.OrDash(((Resolution)o).Version));
                        label.enableRichText = false;
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "BRANCH", Grow = 1.4f,
                    Cell = o =>
                    {
                        var entry = (Resolution)o;
                        if (!entry.Ok)
                        {
                            var failed = new Label(Fmt.Truncate(Fmt.OrDash(entry.Error), 46));
                            failed.enableRichText = false;
                            return failed;
                        }
                        var label = new Label(Fmt.OrDash(entry.BranchName));
                        label.enableRichText = false;
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "BUILD", Grow = 1f,
                    Cell = o => new Label(Fmt.OrDash(((Resolution)o).BuildVersion)),
                },
                new DataColumn
                {
                    Header = "RESULT", FixedWidth = true, Px = 100,
                    Cell = o =>
                    {
                        var entry = (Resolution)o;
                        return new Chip(entry.Ok ? "resolved" : "failed",
                            entry.Ok ? ChipTone.Ok : ChipTone.Bad);
                    },
                },
                new DataColumn
                {
                    Header = "WHEN", FixedWidth = true, Px = 80, Align = "right",
                    SortKey = o => ((Resolution)o).At,
                    Cell = o => new Label(Fmt.Time(((Resolution)o).At)),
                },
            };
        }

        private VisualElement Kv(string key, string value, string copyable)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-kv");

            var k = new Label(key);
            k.AddToClassList("sc-kv__k");
            row.Add(k);

            var v = new Label(Fmt.OrDash(value));
            v.enableRichText = false;
            v.AddToClassList("sc-kv__v");
            row.Add(v);

            if (!string.IsNullOrEmpty(copyable))
            {
                row.Add(new CopyButton(copyable, Toasts));
            }
            return row;
        }
    }
}
