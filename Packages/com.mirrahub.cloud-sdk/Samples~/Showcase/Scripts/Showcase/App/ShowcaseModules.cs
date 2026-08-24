using System;
using UnityEngine;

namespace MirraCloud.Example.Showcase
{
    /// <summary>Groups modules on the services screen so related SDK features sit together
    /// instead of in one flat grid.</summary>
    public enum ServiceCategory
    {
        Player,
        Social,
        LiveOps,
        Data,
        Tools,
    }

    /// <summary>What a module's SDK service can actually do, so the UI can promise only what
    /// the service delivers. Flags, because most services both read and write.</summary>
    [Flags]
    public enum ServiceCaps
    {
        None = 0,
        Read = 1,
        Write = 2,
        Realtime = 4,
    }

    /// <summary>Display metadata for one SDK module card on the services screen.</summary>
    public struct ServiceMeta
    {
        public string Id;
        public string Title;
        public string Glyph;
        public Color Accent;
        public string Description;
        public ServiceCategory Category;
        public ServiceCaps Caps;
    }

    /// <summary>The full set of SDK modules shown as cards. Each opens its IServiceView
    /// (added per-milestone); until then it falls through to a "coming soon" detail.</summary>
    public static class ShowcaseModules
    {
        public static readonly ServiceMeta[] All = Build();

        private static ServiceMeta[] Build()
        {
            const ServiceCaps rw = ServiceCaps.Read | ServiceCaps.Write;

            return new[]
            {
                M("playerAccount", "Player Account", LucideIcon.User, "#4D8DFF",
                    "The signed-in account and the profiles it owns: nickname, username, gender, icon, country.",
                    ServiceCategory.Player, rw),
                M("friends", "Friends", LucideIcon.UserPlus, "#EC5FA8",
                    "Friend list, incoming and outgoing requests, removal and blocking — one player or many at once.",
                    ServiceCategory.Social, rw),
                M("groups", "Groups", LucideIcon.Users, "#6E8EF5",
                    "Player-made groups with members, roles, bans, invites and join requests.",
                    ServiceCategory.Social, rw),
                M("chats", "Chats", LucideIcon.MessageCircle, "#E0479E",
                    "Channels with message history and members, where new messages arrive as they are sent.",
                    ServiceCategory.Social, rw | ServiceCaps.Realtime),
                M("leaderboard", "Leaderboard", LucideIcon.Trophy, "#F0606A",
                    "Score tables ranked globally, by country or among friends, plus the player's own place.",
                    ServiceCategory.LiveOps, rw),
                M("tournaments", "Tournaments", LucideIcon.Swords, "#E89B3D",
                    "Competitions split into league tables, with standings around the player and end-of-run rewards.",
                    ServiceCategory.LiveOps, rw),
                M("challenges", "Challenges", LucideIcon.Target, "#B6D94C",
                    "Time-boxed goals a player joins, submits a score to, and claims a reward from.",
                    ServiceCategory.LiveOps, rw),
                M("dailyRewards", "Daily Rewards", LucideIcon.CalendarCheck, "#F2843B",
                    "Login calendars: which day the player has reached and what claiming it grants.",
                    ServiceCategory.LiveOps, rw),
                M("economy", "Economy", LucideIcon.Coins, "#5BD15B",
                    "Currencies, items and energy: the player's inventory and every operation that changes it.",
                    ServiceCategory.Player, rw),
                M("purchases", "Purchases", LucideIcon.ShoppingCart, "#F5A623",
                    "Store catalog, the player's orders and subscriptions, and the flow that starts a purchase.",
                    ServiceCategory.Player, rw),
                M("promoCodes", "Promo Codes", LucideIcon.TicketPercent, "#FF7AA8",
                    "Redeeming a code, the effects it leaves active, and the player's redemption history.",
                    ServiceCategory.Player, rw),
                M("cloudSave", "Cloud Save", LucideIcon.Database, "#6BD0E0",
                    "Saved values and files, kept per player, shared by everyone, or under a custom id.",
                    ServiceCategory.Player, rw),
                M("entities", "Entities", LucideIcon.Boxes, "#54C7C7",
                    "Game objects described on the server and read in the client as typed data with named components.",
                    ServiceCategory.Data, ServiceCaps.Read),
                M("remoteConfig", "Remote Config", LucideIcon.SlidersHorizontal, "#B7A0E8",
                    "Values delivered from the server so gameplay can be tuned without shipping a new build.",
                    ServiceCategory.Data, ServiceCaps.Read),
                M("localization", "Localization", LucideIcon.Languages, "#34D6A8",
                    "Translated text by collection and key, fetched for one language or for all of them at once.",
                    ServiceCategory.Data, ServiceCaps.Read),
                M("segments", "Segments", LucideIcon.ChartPie, "#5AB6F0",
                    "The audience groups a project defines, used to target config, offers and rewards.",
                    ServiceCategory.LiveOps, ServiceCaps.Read),
                M("assets", "Assets Storage", LucideIcon.FolderOpen, "#6FC0F0",
                    "Files shipped from the dashboard — textures, audio, text, bundles — loaded by id.",
                    ServiceCategory.Data, ServiceCaps.Read),
                M("analytics", "Analytics", LucideIcon.ChartLine, "#7FB0C0",
                    "Reports gameplay events, session starts and playtime — one event at a time or as a batch.",
                    ServiceCategory.Data, ServiceCaps.Write),
                M("profanity", "Profanity Filter", LucideIcon.Shield, "#E86A6A",
                    "Checks player-written text against a project word list and returns a masked version of it.",
                    ServiceCategory.Tools, ServiceCaps.Read),
                M("cloudCode", "Cloud Code", LucideIcon.Code, "#7FD97F",
                    "Runs a named server-side script with your input and hands back whatever it returns.",
                    ServiceCategory.Tools, rw),
                M("deployment", "Deployment", LucideIcon.Rocket, "#9AA0A6",
                    "Tells the client which branch its build version should be talking to.",
                    ServiceCategory.Tools, ServiceCaps.Read),
                M("webview", "WebView", LucideIcon.Globe, "#46C0B0",
                    "Shows web pages inside the game and reports back what happens on them.",
                    ServiceCategory.Tools, ServiceCaps.None),
            };
        }

        private static ServiceMeta M(string id, string title, string glyph, string hex,
            string description, ServiceCategory category, ServiceCaps caps)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return new ServiceMeta
            {
                Id = id,
                Title = title,
                Glyph = glyph,
                Accent = c,
                Description = description,
                Category = category,
                Caps = caps,
            };
        }
    }
}
