using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Builds a scrollable gallery of every design-system component with dummy data — the M0
    /// visual proof. Mount the result into a UIDocument whose UXML links Showcase.uss.
    /// </summary>
    public static class ComponentGallery
    {
        public static VisualElement Build(RemoteImageLoader images = null)
        {
            var root = new ScrollView(ScrollViewMode.Vertical);
            root.AddToClassList("sc-root");
            root.AddToClassList("sc-gallery");

            root.Add(Title("MirraCloud — Component Gallery"));
            root.Add(Group("Avatars", Avatars(images)));
            root.Add(Group("Chips & rewards", ChipsRow()));
            root.Add(Group("Stat tiles", Stats()));
            root.Add(Group("Progress", Progress()));
            root.Add(Group("List rows", Lists()));
            root.Add(Group("Table", Table()));
            root.Add(Group("Card", Cards()));
            root.Add(Group("States", States()));
            root.Add(Group("Zero states", ZeroStates()));
            root.Add(Group("KPI row", Kpis()));
            root.Add(Group("Charts", ChartsRow()));
            root.Add(Group("Toolbar", ToolbarRow()));
            root.Add(Group("Tabs", TabsRow()));
            root.Add(Group("Pager", PagerRow()));
            root.Add(Group("Micro", MicroRow()));
            root.Add(Group("Form fields", FormRow()));
            root.Add(Group("Action card", ActionRow()));
            root.Add(Group("SDK call book", CallBook()));
            return root;
        }

        private static VisualElement Title(string text)
        {
            var l = new Label(text);
            l.AddToClassList("sc-gallery__title");
            return l;
        }

        private static VisualElement Group(string title, VisualElement content)
        {
            var g = new VisualElement();
            g.AddToClassList("sc-gallery__group");
            g.Add(new SectionHeader(title));
            g.Add(content);
            return g;
        }

        private static VisualElement Row()
        {
            var r = new VisualElement();
            r.AddToClassList("sc-gallery__row");
            return r;
        }

        private static VisualElement Avatars(RemoteImageLoader images)
        {
            var wrap = Row();
            wrap.Add(new Avatar(56).SetInitialsFor("Ada Lovelace"));
            var a2 = new Avatar(56).SetInitialsFor("Grace Hopper");
            a2.SetPresence(new Color(0.18f, 0.81f, 0.63f));
            wrap.Add(a2);
            var a3 = new Avatar(56);
            a3.BindUrl(images, "https://api.dicebear.com/7.x/identicon/png?seed=mirra", "Mirra");
            wrap.Add(a3);
            return wrap;
        }

        private static VisualElement ChipsRow()
        {
            var wrap = Row();
            wrap.Add(new Chip("Neutral", ChipTone.Neutral));
            wrap.Add(new Chip("Online", ChipTone.Ok));
            wrap.Add(new Chip("Away", ChipTone.Warn));
            wrap.Add(new Chip("Busy", ChipTone.Bad));
            wrap.Add(new Chip("Info", ChipTone.Info));
            wrap.Add(new Chip("Accent", ChipTone.Accent));
            wrap.Add(new RewardChip(LucideIcon.Gem, "x250"));
            wrap.Add(new CountdownChip(DateTime.UtcNow.AddHours(28)));
            return wrap;
        }

        private static VisualElement Stats()
        {
            var wrap = Row();
            wrap.Add(new StatTile("Sessions").Set("128"));
            wrap.Add(new StatTile("Active days").Set("42"));
            wrap.Add(new StatTile("Streak", LucideIcon.Flame).Set("7").Highlight(true));
            wrap.Add(new StatTile("Best streak").Set("19"));
            return wrap;
        }

        private static VisualElement Progress()
        {
            var wrap = new VisualElement();
            wrap.style.minWidth = 320;
            wrap.Add(new ProgressBar().Set(72, 100).SetLabel("XP 72 / 100"));
            wrap.Add(new ProgressBar().Set(3, 10).SetLabel("Energy 3 / 10").SetAccent(new Color(0.35f, 0.82f, 0.35f)));
            return wrap;
        }

        private static VisualElement Lists()
        {
            var wrap = new VisualElement();
            wrap.style.minWidth = 360;
            for (int i = 0; i < 3; i++)
            {
                var row = new ListRow();
                row.SetLead(new Avatar(36).SetInitialsFor("Player " + (i + 1)));
                row.SetTitle("Player " + (i + 1));
                row.SetSubtitle("@player_" + (i + 1));
                row.SetTrailing(new Chip(i == 0 ? "Online" : "Offline", i == 0 ? ChipTone.Ok : ChipTone.Neutral));
                wrap.Add(row);
            }
            return wrap;
        }

        private static VisualElement Table()
        {
            var cols = new[]
            {
                new DataColumn { Header = "#", FixedWidth = true, Px = 48, Cell = o => new Label(((string[])o)[0]) },
                new DataColumn { Header = "Player", Grow = 2f, Cell = o => new Label(((string[])o)[1]) },
                new DataColumn { Header = "Score", Grow = 1f, Align = "right", Cell = o => new Label(((string[])o)[2]) },
            };
            var table = new DataTable(cols);
            var rows = new List<string[]>
            {
                new[] { "1", "Ada", "9820" },
                new[] { "2", "Grace", "8740" },
                new[] { "3", "Alan", "8010" },
            };
            table.Bind(rows, o => ((string[])o)[0] == "2");
            table.style.minWidth = 360;
            return table;
        }

        private static VisualElement Cards()
        {
            var card = new Card(new Color(0.66f, 0.55f, 0.98f));
            card.WithTitle("Account");
            card.Body.Add(new Label("Body content goes here."));
            card.style.minWidth = 320;
            return card;
        }

        // The zero states are the point of the refactor, so the gallery shows them next to the
        // populated widgets: an empty screen has to keep the shape of a full one.
        private static VisualElement ZeroStates()
        {
            var wrap = Row();

            var t = new VisualElement();
            t.style.minWidth = 360;
            t.Add(ZeroState.Table(GalleryColumns(), "Scores will appear once a player submits one.",
                3, "Submit a demo score", () => Debug.Log("[Gallery] CTA")));
            wrap.Add(t);

            var c = new VisualElement();
            c.style.minWidth = 300;
            c.Add(ZeroState.Cards(LucideIcon.Package, "No items in this inventory yet.", 3));
            wrap.Add(c);

            var p = new VisualElement();
            p.style.minWidth = 300;
            p.Add(ZeroState.NotConfigured("Tournaments"));
            wrap.Add(p);

            return wrap;
        }

        private static VisualElement Kpis()
        {
            var wrap = new VisualElement();
            wrap.style.minWidth = 520;
            wrap.Add(new KpiRow()
                .Add("Sessions", LucideIcon.Activity, "1 240", "+12%")
                .Add("Retention", LucideIcon.TrendingUp, "38%", "-4%")
                .Add("Streak", LucideIcon.Flame, "7", null, true));
            wrap.Add(new KpiRow()
                .AddZero("Sessions", LucideIcon.Activity)
                .AddZero("Retention", LucideIcon.TrendingUp)
                .AddZero("Streak", LucideIcon.Flame));
            return wrap;
        }

        private static VisualElement ChartsRow()
        {
            var wrap = Row();

            var spark = new VisualElement();
            spark.style.minWidth = 280;
            spark.Add(new Sparkline().SetData(new[] { 3f, 7f, 5f, 9f, 12f, 8f, 14f }).SetArea(true));
            spark.Add(new Sparkline().SetData(null));
            wrap.Add(spark);

            var bars = new VisualElement();
            bars.style.minWidth = 300;
            bars.Add(new BarChart().SetData(new[]
            {
                new ChartPoint("Mon", 12f), new ChartPoint("Tue", 19f), new ChartPoint("Wed", 7f),
                new ChartPoint("Thu", 22f), new ChartPoint("Fri", 15f)
            }));
            wrap.Add(bars);

            var donut = new VisualElement();
            donut.style.minWidth = 300;
            donut.Add(new DonutChart().SetData(new[]
            {
                new ChartPoint("Images", 42f), new ChartPoint("Audio", 12f),
                new ChartPoint("Docs", 7f), new ChartPoint("Other", 3f)
            }).SetCenter("64", "assets"));
            wrap.Add(donut);

            var empty = new VisualElement();
            empty.style.minWidth = 300;
            empty.Add(new BarChart().SetData(null));
            wrap.Add(empty);

            return wrap;
        }

        private static VisualElement ToolbarRow()
        {
            var wrap = new VisualElement();
            wrap.style.minWidth = 520;
            wrap.Add(new Toolbar()
                .WithSearch("Search players…", s => Debug.Log("[Gallery] search: " + s))
                .WithFilter("Status", new[] { "All", "Online", "Offline" }, s => Debug.Log("[Gallery] filter: " + s))
                .WithSpacer()
                .WithRefresh(() => Debug.Log("[Gallery] refresh"))
                .WithAction("Invite", LucideIcon.UserPlus, () => Debug.Log("[Gallery] invite"), true)
                .WithSdkCall(() => Debug.Log("[Gallery] sdk call")));
            return wrap;
        }

        private static VisualElement TabsRow()
        {
            var wrap = new VisualElement();
            wrap.style.minWidth = 520;
            wrap.Add(new Tabs()
                .Add("Overview", LucideIcon.Gauge, () => new Label("Overview pane"))
                .Add("Data", LucideIcon.Table, () => new Label("Data pane"))
                .Add("Actions", LucideIcon.Zap, () => new Label("Actions pane")));
            return wrap;
        }

        private static VisualElement PagerRow()
        {
            var wrap = new VisualElement();
            wrap.style.minWidth = 360;
            var pager = new Pager(10);
            pager.PageRequested += p => Debug.Log("[Gallery] page " + p);
            pager.SetTotal(137);
            wrap.Add(pager);
            return wrap;
        }

        private static VisualElement MicroRow()
        {
            var wrap = Row();
            wrap.Add(new Badge("12", ChipTone.Accent));
            wrap.Add(new Badge("beta", ChipTone.Info));
            wrap.Add(new CopyButton("000000000000000000000000", null, "Copy id"));
            wrap.Add(RelativeTime.Build(DateTime.UtcNow.AddMinutes(-12)));
            wrap.Add(new InfoHint("Scores are branch-scoped: submitting here only affects this branch."));

            var json = new VisualElement();
            json.style.minWidth = 320;
            json.Add(new JsonViewer().SetRaw("{\"level\":7,\"coins\":1240,\"tags\":[\"beta\",\"vip\"]}"));
            wrap.Add(json);
            return wrap;
        }

        private static VisualElement FormRow()
        {
            var wrap = new VisualElement();
            wrap.style.minWidth = 420;
            wrap.Add(new FormView(new[]
            {
                FormField.Text("nickname", "Nickname", "Ada", true),
                FormField.Int("score", "Score", 100),
                FormField.Choice("region", "Region", new[] { "EU", "NA", "APAC" }, "EU"),
                FormField.Bool("dryRun", "Dry run", true),
            }));
            return wrap;
        }

        private static VisualElement ActionRow()
        {
            var wrap = new VisualElement();
            wrap.style.minWidth = 420;
            var card = new ActionCard("Submit score", "Writes a score into the selected leaderboard.", LucideIcon.Trophy)
                .WithFields(FormField.Int("score", "Score", 100))
                .WithSnippet("await sdk.Leaderboard.SubmitScoreAsync(\"weekly\", 100).Task();")
                .OnRun("Submit", v => Task.FromResult(ActionOutcome.Success("Submitted " + v.Int("score"))));
            wrap.Add(card);
            return wrap;
        }

        private static VisualElement CallBook()
        {
            var wrap = new VisualElement();
            wrap.style.minWidth = 520;
            wrap.Add(SdkCallDrawer.Build(new[]
            {
                new SdkCall("Load the leaderboard", "var op = sdk.Leaderboard.GetLeaderboardTopEntries(\"weekly\");\nawait op.Task();", "Top entries for one config."),
            }, null));
            return wrap;
        }

        private static DataColumn[] GalleryColumns()
        {
            return new[]
            {
                new DataColumn { Header = "#", FixedWidth = true, Px = 48, Cell = o => new Label(((string[])o)[0]) },
                new DataColumn { Header = "Player", Grow = 2f, Cell = o => new Label(((string[])o)[1]) },
                new DataColumn { Header = "Score", Grow = 1f, Align = "right", Cell = o => new Label(((string[])o)[2]) },
            };
        }

        private static VisualElement States()
        {
            var wrap = Row();
            var s1 = new VisualElement();
            s1.style.minWidth = 200;
            Skeleton.Into(s1, 3);
            var s2 = new VisualElement();
            s2.style.minWidth = 200;
            s2.Add(EmptyState.Default());
            var s3 = new VisualElement();
            s3.style.minWidth = 200;
            s3.Add(ErrorState.Message("Something went wrong"));
            wrap.Add(s1);
            wrap.Add(s2);
            wrap.Add(s3);
            return wrap;
        }
    }
}
