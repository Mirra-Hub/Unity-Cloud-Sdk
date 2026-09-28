using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MirraCloud.Core.Economy.Dto;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Rewards waiting for the player, read and claimed through Economy — where leaderboards, tournaments,
    /// challenges and the other sources pay out when a session ends. A screen of one source lists only that source's
    /// rewards; claiming takes every pending reward at once, whatever its source, because that is what the call does.
    /// </summary>
    public sealed class PendingRewardsPanel : VisualElement
    {
        public const string ReadSnippet = @"// what the player was granted and has not claimed yet — reading claims nothing
var op = sdk.Economy.GetPendingRewardsAsync();
await op.Task();
if (!op.Result.IsSuccess) { return; }

foreach (var container in op.Result.Data)
{
    // container.SourceType: Leaderboard, Tournament, Challenge, …
    // container.SourceId:   the config that granted it, e.g. LeaderboardConfig.Id
    foreach (var reward in container.Rewards)
    {
        Debug.Log(reward.RewardKey + "" x"" + reward.Count + "" ("" + reward.EconomyResourceKind + "")"");
    }
}";

        public const string ClaimSnippet = @"// claim everything pending: the resources go to the wallet, items and energies
var op = sdk.Economy.ClaimRewardsAsync();
await op.Task();

if (op.Result.IsSuccess)
{
    // op.Result.Data: what was claimed, by source — an empty list when nothing was pending
}";

        private readonly ShowcaseContext _ctx;
        private readonly RewardSourceType? _source;
        private readonly string _emptyText;
        private readonly VisualElement _slot = new VisualElement();

        /// <param name="ctx">The screen's context.</param>
        /// <param name="source">The source to list; null lists every source.</param>
        /// <param name="intro">One paragraph on where these rewards come from.</param>
        /// <param name="emptyText">What the empty list says.</param>
        public PendingRewardsPanel(ShowcaseContext ctx, RewardSourceType? source, string intro, string emptyText)
        {
            _ctx = ctx;
            _source = source;
            _emptyText = emptyText;

            var hint = new Label(intro);
            hint.enableRichText = false;
            hint.AddToClassList("sc-fs-hint");
            Add(hint);

            Add(new SectionHeader("Pending rewards"));
            Add(_slot);

            Add(new SectionHeader("Claiming"));
            Add(new ActionCard("Claim the pending rewards",
                    "Claims every pending reward — of every source, that is what the call does — then re-reads "
                    + "the list above.", LucideIcon.Gift)
                .WithSnippet(ClaimSnippet)
                .OnRun("Claim", Claim));

            Load();
        }

        /// <summary>Re-reads the pending rewards, e.g. after a reset that may have granted some.</summary>
        public void Load()
        {
            ViewBind.Load(
                () => _ctx.Sdk.Economy.GetPendingRewardsAsync(),
                _slot,
                Render,
                isEmpty: containers => Rows(containers).Count == 0,
                options: new BindOptions
                {
                    Log = _ctx.Log,
                    Label = "Economy: pending rewards",
                    Snippet = ReadSnippet,
                    ServiceName = "Economy reward",
                    AllowRetry = true,
                    EmptyView = () => ZeroState.Table(Columns(), _emptyText, 3),
                });
        }

        private VisualElement Render(List<RewardContainerDto> containers)
        {
            var rows = Rows(containers);

            var sources = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                sources.Add(row.Source + ":" + row.SourceId);
            }

            var col = new VisualElement();
            col.Add(new KpiRow()
                .Add("Pending rewards", LucideIcon.Gift, rows.Count.ToString(), null, rows.Count > 0)
                .Add("Granted by", LucideIcon.Trophy, sources.Count == 1 ? "1 source" : sources.Count + " sources"));

            var table = new DataTable(Columns()).WithZebra().WithMaxHeight(320f);
            table.Bind(rows);
            col.Add(table);
            return col;
        }

        private async Task<ActionOutcome> Claim(FormValues values)
        {
            var op = _ctx.Sdk.Economy.ClaimRewardsAsync();
            if (op == null)
            {
                return ActionOutcome.Failure("the call could not be started");
            }
            await op.Task();

            var result = op.Result;
            if (_ctx.Log != null && result != null)
            {
                _ctx.Log.Record("Economy: claim rewards", result, ClaimSnippet);
            }

            if (result == null || !result.IsSuccess)
            {
                string message = result != null && result.Error != null && !string.IsNullOrEmpty(result.Error.Message)
                    ? result.Error.Message
                    : "no response";
                return ActionOutcome.Failure(message);
            }

            int claimed = 0;
            if (result.Data != null)
            {
                foreach (var container in result.Data)
                {
                    claimed += container != null && container.Rewards != null ? container.Rewards.Count : 0;
                }
            }

            _ctx.Toasts?.Ok(claimed == 0 ? "Nothing was pending" : "Claimed " + claimed + (claimed == 1 ? " reward" : " rewards"));
            Load();
            return ActionOutcome.Success(claimed == 0
                ? "Nothing was pending"
                : "Claimed " + claimed + " — the wallet, items and energies have them now; the list above was re-read");
        }

        private List<Row> Rows(List<RewardContainerDto> containers)
        {
            var rows = new List<Row>();
            if (containers == null)
            {
                return rows;
            }

            foreach (var container in containers)
            {
                if (container == null || container.Rewards == null ||
                    (_source.HasValue && container.SourceType != _source.Value))
                {
                    continue;
                }

                foreach (var reward in container.Rewards)
                {
                    if (reward != null)
                    {
                        rows.Add(new Row { Source = container.SourceType, SourceId = container.SourceId, Reward = reward });
                    }
                }
            }
            return rows;
        }

        private static DataColumn[] Columns()
        {
            return new[]
            {
                new DataColumn
                {
                    Header = "GRANTED BY", Grow = 1.4f,
                    SortKey = o => ((Row)o).Source.ToString(),
                    Cell = o =>
                    {
                        var row = (Row)o;
                        var label = new Label(row.Source + " · " + Fmt.Id(row.SourceId, 8));
                        label.enableRichText = false;
                        label.tooltip = "The config that granted it: " + Fmt.OrDash(row.SourceId);
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "RESOURCE", Grow = 2f,
                    SortKey = o => ((Row)o).Reward.RewardKey,
                    Cell = o =>
                    {
                        var reward = ((Row)o).Reward;
                        var label = new Label(Fmt.OrDash(reward.RewardKey) + " · " + reward.EconomyResourceKind);
                        label.enableRichText = false;
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "AMOUNT", FixedWidth = true, Px = 110, Align = "right",
                    SortKey = o => ((Row)o).Reward.Count,
                    Cell = o => new Label("×" + ((Row)o).Reward.Count),
                },
            };
        }

        private sealed class Row
        {
            public RewardSourceType Source;
            public string SourceId;
            public RewardEntryDto Reward;
        }
    }
}
