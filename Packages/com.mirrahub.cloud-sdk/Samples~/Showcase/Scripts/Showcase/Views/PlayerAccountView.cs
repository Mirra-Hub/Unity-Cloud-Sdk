using System;
using System.Collections.Generic;
using MirraCloud.Core.Auth;
using MirraCloud.Core.Enums;
using Plugins.MirraCloud.Core.Services.PlayerAccount.Dto;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Player Account screen, and the reference layout the other service views follow: the header
    /// carries the "who am I" status chip (the subtitle comes from <c>ServiceMeta.Description</c>),
    /// the toolbar reloads the screen and opens the <c>&lt;/&gt;</c> code drawer, and two tabs split
    /// the identity (Overview) from the sub-profiles the account owns (Profiles).
    /// <para>
    /// Read-only on purpose. Everything this service can change — nickname, username, gender, icon,
    /// segments — arrives later as actions; nothing on this screen writes.
    /// </para>
    /// </summary>
    public sealed class PlayerAccountView : ServiceView
    {
        private const string AccountSnippet =
@"// Overview tab — one call feeds the hero card, the KPI row and the details list.
var op = sdk.PlayerAccount.GetAccountAsync();
await op.Task();

var result = op.Result;
if (result.IsSuccess)
{
    PlayerAccountInfo a = result.Data;
    // a.Nickname, a.Username, a.Gender, a.Country, a.LanguageCode, a.TimeZone,
    // a.TotalSessions, a.TotalActiveDays, a.ConsecutiveActiveDays, a.MaxConsecutiveActiveDays,
    // a.SegmentKeys, a.AbTestKeys
}";

        private const string ProfilesSnippet =
@"// Profiles tab — every sub-profile the signed-in account owns.
var op = sdk.PlayerAccount.GetProfilesAsync();
await op.Task();

var result = op.Result;
if (result.IsSuccess)
{
    foreach (ProfileInfo p in result.Data)
    {
        // p.Id, p.Nickname, p.Username, p.Gender, p.IconUrl,
        // p.RoleKeys, p.SegmentKeys, p.AbTestKeys, p.LastLogin
    }
}";

        private const string PresenceSnippet =
@"// One row's presence badge, resolved per profile id.
var op = sdk.PlayerAccount.GetProfilePresenceStatusAsync(profileId);
await op.Task();

var result = op.Result;
if (result != null && result.IsSuccess)
{
    // Online / Offline / Away / Busy / Invisible / OnTheWay
    ProfilePresenceStatus status = result.Data;
}";

        // Presence is a call per profile, so the answers are cached: DataTable re-runs the cell
        // factories on every sort click, and without the cache each sort would re-issue one request
        // per row. A cached null means "asked, no usable answer" — see LoadPresence.
        private readonly Dictionary<string, ProfilePresenceStatus?> _presence =
            new Dictionary<string, ProfilePresenceStatus?>();

        // Newest cell element per profile id. A sort while a call is in flight rebuilds the cell, and
        // only the latest one is still in the tree — the earlier element is dropped by the re-render.
        private readonly Dictionary<string, VisualElement> _presenceCells =
            new Dictionary<string, VisualElement>();

        private readonly HashSet<string> _presenceInFlight = new HashSet<string>();

        public PlayerAccountView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            // Refresh() re-runs Populate(), so drop the presence answers here: a reload the reader
            // asked for has to really re-read them instead of repainting the previous ones.
            _presence.Clear();
            _presenceCells.Clear();
            _presenceInFlight.Clear();

            DeclareCall(new SdkCall("Read the account", AccountSnippet,
                "The screen runs this through ViewBind, which wraps it in the loading / empty / error states."));
            DeclareCall(new SdkCall("List the account's profiles", ProfilesSnippet));
            DeclareCall(new SdkCall("Read a profile's presence", PresenceSnippet,
                "Issued once per row and cached, so re-sorting the table does not ask the server again."));

            // The account cached at login paints the chip immediately; the Overview load overwrites
            // it with the freshly fetched one a moment later.
            SyncStatus(Sdk.PlayerAccount.PlayerAccountInfo);

            UseToolbar().WithRefresh(Refresh);

            UseTabs()
                .Add("Overview", LucideIcon.User, BuildOverview)
                .Add("Profiles", LucideIcon.Users, BuildProfilesPane);
        }

        // ----- header -------------------------------------------------------------------------

        /// <summary>
        /// Header verdict: who this screen is showing. An account with no name to print is an
        /// anonymous guest session — the only state where the chip is not a signed-in identity.
        /// </summary>
        private void SyncStatus(PlayerAccountInfo a)
        {
            string name = null;
            if (a != null)
            {
                name = !string.IsNullOrEmpty(a.Nickname)
                    ? a.Nickname
                    : (!string.IsNullOrEmpty(a.Username) ? "@" + a.Username : null);
            }

            if (name != null)
            {
                SetStatus("Signed in as " + Fmt.Truncate(name, 22), ChipTone.Ok);
                return;
            }
            if (a == null && Sdk.Authentication != null && Sdk.Authentication.IsAuth)
            {
                SetStatus("Signed in", ChipTone.Ok);
                return;
            }
            SetStatus("Guest", ChipTone.Warn);
        }

        // ----- Overview tab -------------------------------------------------------------------

        private VisualElement BuildOverview()
        {
            var slot = new VisualElement();
            ViewBind.Load(
                () => Sdk.PlayerAccount.GetAccountAsync(),
                slot,
                BuildAccount,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Account",
                    Snippet = AccountSnippet,
                    ServiceName = "Player Account",
                    AllowRetry = true,
                });
            return slot;
        }

        private VisualElement BuildAccount(PlayerAccountInfo a)
        {
            SyncStatus(a);

            var col = new VisualElement();
            col.Add(Hero(a));
            col.Add(Kpis(a));
            col.Add(Details(a));
            col.Add(Tags("Segments", a.SegmentKeys, ChipTone.Accent,
                "This account is not in any segment yet."));
            col.Add(Tags("A/B tests", a.AbTestKeys, ChipTone.Warn,
                "This account is not enrolled in any A/B test."));
            return col;
        }

        private VisualElement Hero(PlayerAccountInfo a)
        {
            var card = new Card(Meta.Accent);
            card.AddToClassList("sc-pa-block");

            var top = new VisualElement();
            top.AddToClassList("sc-hero__top");

            var avatar = new Avatar(72f);
            avatar.BindUrl(Images, a.AvatarUrl, a.Nickname);
            top.Add(avatar);

            var id = new VisualElement();
            id.AddToClassList("sc-hero__id");

            var name = new Label(Fmt.OrDash(a.Nickname));
            name.enableRichText = false;
            name.AddToClassList("sc-hero__name");
            id.Add(name);

            if (!string.IsNullOrEmpty(a.Username))
            {
                var handle = new Label("@" + a.Username);
                handle.enableRichText = false;
                handle.AddToClassList("sc-hero__handle");
                id.Add(handle);
            }

            var traits = new VisualElement();
            traits.AddToClassList("sc-chip-row");
            traits.AddToClassList("sc-pa-traits");
            if (a.Gender != Gender.Unspecified)
            {
                traits.Add(new Chip(a.Gender.ToString(), ChipTone.Info));
            }
            if (a.Age > 0)
            {
                traits.Add(new Chip(a.Age + " yrs"));
            }
            // CountryCode and LanguageCode have no "unset" member — their zero values are
            // Afghanistan and En. An account that never set a country would otherwise be labelled
            // Afghanistan, so the default value is treated as "not set" and the hint says so.
            bool countrySet = a.Country != default(CountryCode);
            bool languageSet = a.LanguageCode != default(LanguageCode);
            if (countrySet)
            {
                traits.Add(new Chip(a.Country.ToString()));
            }
            if (languageSet)
            {
                traits.Add(new Chip(a.LanguageCode.ToString()));
            }
            if (!countrySet || !languageSet)
            {
                traits.Add(new InfoHint("Country and language are plain enums in the SDK with no "
                    + "\"unset\" member, so their default values (" + default(CountryCode) + ", "
                    + default(LanguageCode) + ") are shown as not set."));
            }
            if (!string.IsNullOrEmpty(a.Status))
            {
                traits.Add(new Chip(a.Status, ChipTone.Ok));
            }
            id.Add(traits);

            top.Add(id);
            card.Body.Add(top);
            return card;
        }

        /// <summary>Lifetime activity. A counter that has never moved is added as a muted zero rather
        /// than dropped, so the strip keeps the same four columns on a brand-new account.</summary>
        private static VisualElement Kpis(PlayerAccountInfo a)
        {
            var row = new KpiRow();
            row.AddToClassList("sc-pa-block");

            if (a.TotalSessions > 0)
            {
                row.Add("Sessions", LucideIcon.Sigma, Fmt.Number(a.TotalSessions));
            }
            else
            {
                row.AddZero("Sessions", LucideIcon.Sigma);
            }

            if (a.TotalActiveDays > 0)
            {
                row.Add("Active days", LucideIcon.CalendarDays, Fmt.Number(a.TotalActiveDays));
            }
            else
            {
                row.AddZero("Active days", LucideIcon.CalendarDays);
            }

            if (a.ConsecutiveActiveDays > 0)
            {
                row.Add("Streak", LucideIcon.Flame, Fmt.Number(a.ConsecutiveActiveDays), null, true);
            }
            else
            {
                row.AddZero("Streak", LucideIcon.Flame);
            }

            if (a.MaxConsecutiveActiveDays > 0)
            {
                row.Add("Best streak", LucideIcon.Star, Fmt.Number(a.MaxConsecutiveActiveDays));
            }
            else
            {
                row.AddZero("Best streak", LucideIcon.Star);
            }

            return row;
        }

        private VisualElement Details(PlayerAccountInfo a)
        {
            var card = new Card();
            card.AddToClassList("sc-pa-block");
            card.WithTitle("Account details");

            var list = new VisualElement();
            list.AddToClassList("sc-kv-list");
            list.AddToClassList("sc-pa-kv");
            list.Add(Kv("Member since", Fmt.Date(a.CreatedDate)));
            list.Add(Kv("Last login", Fmt.DateTime2(a.LastLoginDate)));
            list.Add(Kv("Updated", Fmt.DateTime2(a.UpdatedDate)));
            list.Add(Kv("Time zone", a.TimeZone));
            list.Add(KvCopy("Account ID", a.Id));
            list.Add(KvCopy("Scope", a.ScopeId));
            card.Body.Add(list);
            return card;
        }

        // ----- Profiles tab -------------------------------------------------------------------

        private VisualElement BuildProfilesPane()
        {
            var slot = new VisualElement();
            ViewBind.Load(
                () => Sdk.PlayerAccount.GetProfilesAsync(),
                slot,
                BuildProfileTable,
                p => p == null || p.Length == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Profiles",
                    Snippet = ProfilesSnippet,
                    ServiceName = "Player Account",
                    AllowRetry = true,
                    // Same columns as the populated table: an account without sub-profiles still
                    // shows the shape it would have, plus the line that says how one gets there.
                    EmptyView = () => ZeroState.Table(ProfileColumns(),
                        "This account owns no sub-profiles yet. Create one with CreateProfileAsync "
                        + "and it will appear here."),
                });
            return slot;
        }

        private VisualElement BuildProfileTable(ProfileInfo[] profiles)
        {
            // Configure first, bind last: Bind/WithZebra/WithSort/WithRowClick each re-render, so
            // binding up front would build every row four times over.
            var table = new DataTable(ProfileColumns())
                .WithZebra()
                // Roomy enough that a normal account never scrolls the table inside the page scroller.
                .WithMaxHeight(520f)
                .WithSort(5, false);

            // No popup host means nowhere to open the profile card, so the rows stay inert instead
            // of looking clickable and doing nothing.
            if (Popup != null)
            {
                table.WithRowClick(OpenProfile);
            }

            table.Bind(profiles);
            return table;
        }

        private DataColumn[] ProfileColumns()
        {
            return new[]
            {
                new DataColumn { Header = string.Empty, FixedWidth = true, Px = 44, Align = "center", Cell = AvatarCell },
                new DataColumn
                {
                    Header = "NICKNAME", Grow = 1.4f, Cell = NicknameCell,
                    SortKey = r => Row(r) != null ? Row(r).Nickname : null,
                },
                new DataColumn
                {
                    Header = "USERNAME", Grow = 1.2f, Cell = UsernameCell,
                    SortKey = r => Row(r) != null ? Row(r).Username : null,
                },
                new DataColumn
                {
                    Header = "GENDER", FixedWidth = true, Px = 96, Cell = GenderCell,
                    SortKey = r => Row(r) != null ? Row(r).Gender.ToString() : null,
                },
                new DataColumn
                {
                    Header = "PRESENCE", FixedWidth = true, Px = 116, Cell = PresenceCell,
                    SortKey = PresenceSortKey,
                },
                new DataColumn
                {
                    Header = "LAST LOGIN", FixedWidth = true, Px = 132, Align = "right", Cell = LastLoginCell,
                    SortKey = r => Row(r) != null ? (IComparable)Row(r).LastLogin : null,
                },
            };
        }

        private VisualElement AvatarCell(object row)
        {
            var p = Row(row);
            var av = new Avatar(28f);
            av.BindUrl(Images, p != null ? p.IconUrl : null, p != null ? p.Nickname : null);
            return av;
        }

        private static VisualElement NicknameCell(object row)
        {
            var p = Row(row);
            var l = new Label(Fmt.OrDash(p != null ? p.Nickname : null));
            l.enableRichText = false;
            l.AddToClassList("sc-pa-cell--strong");
            return l;
        }

        private static VisualElement UsernameCell(object row)
        {
            var p = Row(row);
            string username = p != null ? p.Username : null;
            var l = new Label(string.IsNullOrEmpty(username) ? Fmt.Dash : "@" + username);
            l.enableRichText = false;
            l.AddToClassList("sc-pa-cell--dim");
            return l;
        }

        private static VisualElement GenderCell(object row)
        {
            var p = Row(row);
            bool known = p != null && p.Gender != Gender.Unspecified;
            var l = new Label(known ? p.Gender.ToString() : Fmt.Dash);
            l.enableRichText = false;
            l.AddToClassList("sc-pa-cell--dim");
            return l;
        }

        private static VisualElement LastLoginCell(object row)
        {
            var p = Row(row);
            return RelativeTime.Build(p != null ? p.LastLogin : default(DateTime));
        }

        // ----- presence -----------------------------------------------------------------------

        private VisualElement PresenceCell(object row)
        {
            var p = Row(row);
            var host = new VisualElement();
            host.AddToClassList("sc-pa-presence");

            if (p == null || string.IsNullOrEmpty(p.Id))
            {
                host.Add(PresenceChip(null));
                return host;
            }

            ProfilePresenceStatus? cached;
            if (_presence.TryGetValue(p.Id, out cached))
            {
                host.Add(PresenceChip(cached));
                return host;
            }

            var pending = new Chip("…");
            pending.tooltip = "Reading presence";
            host.Add(pending);

            _presenceCells[p.Id] = host;
            if (_presenceInFlight.Add(p.Id))
            {
                LoadPresence(p.Id);
            }
            return host;
        }

        private async void LoadPresence(string profileId)
        {
            var op = Sdk.PlayerAccount.GetProfilePresenceStatusAsync(profileId);
            await op.Task();
            var result = op.Result;

            _presenceInFlight.Remove(profileId);
            if (Log != null && result != null)
            {
                Log.Record("Profile presence", result, PresenceSnippet);
            }

            // A failure is cached as "unknown" deliberately: leaving the id out would make the next
            // re-render (one sort click) fire the same failing request again, once per row.
            bool ok = result != null && result.IsSuccess;
            _presence[profileId] = ok ? result.Data : (ProfilePresenceStatus?)null;

            VisualElement host;
            if (_presenceCells.TryGetValue(profileId, out host) && host != null)
            {
                host.Clear();
                host.Add(PresenceChip(_presence[profileId]));
            }
        }

        /// <summary>Sort rank, so ascending order reads Online → Offline → unknown rather than
        /// alphabetically. Rows still waiting for their call sort last.</summary>
        private IComparable PresenceSortKey(object row)
        {
            var p = Row(row);
            ProfilePresenceStatus? cached = null;
            if (p != null && !string.IsNullOrEmpty(p.Id))
            {
                _presence.TryGetValue(p.Id, out cached);
            }
            if (!cached.HasValue)
            {
                return int.MaxValue;
            }

            switch (cached.Value)
            {
                case ProfilePresenceStatus.Online: return 0;
                case ProfilePresenceStatus.OnTheWay: return 1;
                case ProfilePresenceStatus.Busy: return 2;
                case ProfilePresenceStatus.Away: return 3;
                case ProfilePresenceStatus.Invisible: return 4;
                default: return 5;
            }
        }

        private static Chip PresenceChip(ProfilePresenceStatus? status)
        {
            if (!status.HasValue)
            {
                var unknown = new Chip(Fmt.Dash);
                unknown.tooltip = "Presence unavailable";
                return unknown;
            }

            switch (status.Value)
            {
                case ProfilePresenceStatus.Online: return new Chip("Online", ChipTone.Ok);
                case ProfilePresenceStatus.Away: return new Chip("Away", ChipTone.Warn);
                case ProfilePresenceStatus.Busy: return new Chip("Busy", ChipTone.Bad);
                case ProfilePresenceStatus.OnTheWay: return new Chip("On the way", ChipTone.Info);
                case ProfilePresenceStatus.Invisible: return new Chip("Invisible");
                default: return new Chip("Offline");
            }
        }

        // ----- profile card -------------------------------------------------------------------

        /// <summary>
        /// Opens the clicked row. Everything shown comes from the list response already in hand —
        /// re-fetching the same profile would only add a spinner between the click and the card.
        /// </summary>
        private void OpenProfile(object row)
        {
            var p = Row(row);
            var popup = Popup;
            if (p == null || popup == null)
            {
                return;
            }
            popup.Open(ProfileCard(p), "Profile");
        }

        private VisualElement ProfileCard(ProfileInfo p)
        {
            var root = new VisualElement();
            root.AddToClassList("sc-pa-card");

            var top = new VisualElement();
            top.AddToClassList("sc-hero__top");

            var av = new Avatar(56f);
            av.BindUrl(Images, p.IconUrl, p.Nickname);
            top.Add(av);

            var id = new VisualElement();
            id.AddToClassList("sc-hero__id");

            var name = new Label(Fmt.OrDash(p.Nickname));
            name.enableRichText = false;
            name.AddToClassList("sc-pa-card__name");
            id.Add(name);

            var handle = new Label(string.IsNullOrEmpty(p.Username) ? Fmt.Dash : "@" + p.Username);
            handle.enableRichText = false;
            handle.AddToClassList("sc-hero__handle");
            id.Add(handle);

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.AddToClassList("sc-pa-traits");
            if (p.Gender != Gender.Unspecified)
            {
                chips.Add(new Chip(p.Gender.ToString(), ChipTone.Info));
            }
            ProfilePresenceStatus? presence;
            if (!string.IsNullOrEmpty(p.Id) && _presence.TryGetValue(p.Id, out presence))
            {
                chips.Add(PresenceChip(presence));
            }
            if (!string.IsNullOrEmpty(p.Status))
            {
                chips.Add(new Chip(p.Status, ChipTone.Ok));
            }
            id.Add(chips);

            top.Add(id);
            root.Add(top);

            // The dialog does not scroll on its own, and a profile with roles, segments and tests
            // is taller than a short screen.
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("sc-pa-card__scroll");

            var list = new VisualElement();
            list.AddToClassList("sc-kv-list");
            list.Add(KvCopy("Profile ID", p.Id));
            list.Add(KvCopy("Account ID", p.AccountId));
            list.Add(Kv("Created", Fmt.Date(p.CreatedDate)));
            list.Add(Kv("Last login", Fmt.DateTime2(p.LastLogin)));
            list.Add(Kv("Updated", Fmt.DateTime2(p.UpdatedDate)));
            scroll.Add(list);

            scroll.Add(Tags("Roles", p.RoleKeys, ChipTone.Info,
                "No player roles assigned to this profile."));
            scroll.Add(Tags("Segments", p.SegmentKeys, ChipTone.Accent,
                "This profile is not in any segment yet."));
            scroll.Add(Tags("A/B tests", p.AbTestKeys, ChipTone.Warn,
                "This profile is not enrolled in any A/B test."));

            root.Add(scroll);
            return root;
        }

        // ----- shared pieces ------------------------------------------------------------------

        /// <summary>A titled chip strip that keeps its heading when there is nothing to show, so the
        /// reader learns the account simply has none rather than wondering where the section went.</summary>
        private static VisualElement Tags(string title, string[] tags, ChipTone tone, string emptyNote)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("sc-pa-block");

            bool any = tags != null && tags.Length > 0;
            wrap.Add(new SectionHeader(title, any ? tags.Length.ToString() : null));

            if (!any)
            {
                var note = new Label(emptyNote);
                note.enableRichText = false;
                note.AddToClassList("sc-pa-note");
                wrap.Add(note);
                return wrap;
            }

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            foreach (var t in tags)
            {
                chips.Add(new Chip(t, tone));
            }
            wrap.Add(chips);
            return wrap;
        }

        private static VisualElement Kv(string key, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-kv");

            var k = new Label(key);
            k.enableRichText = false;
            k.AddToClassList("sc-kv__k");
            row.Add(k);

            var v = new Label(Fmt.OrDash(value));
            v.enableRichText = false;
            v.AddToClassList("sc-kv__v");
            row.Add(v);
            return row;
        }

        /// <summary>Same row with a clipboard button — for the opaque ids a reader needs to paste
        /// into the Mirra Hub console or a support ticket.</summary>
        private VisualElement KvCopy(string key, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-kv");

            var k = new Label(key);
            k.enableRichText = false;
            k.AddToClassList("sc-kv__k");
            row.Add(k);

            var side = new VisualElement();
            side.AddToClassList("sc-pa-kv__val");

            var v = new Label(Fmt.OrDash(value));
            v.enableRichText = false;
            v.AddToClassList("sc-kv__v");
            side.Add(v);

            if (!string.IsNullOrEmpty(value))
            {
                side.Add(new CopyButton(value, Toasts));
            }
            row.Add(side);
            return row;
        }

        /// <summary>DataTable hands cells and sort keys a boxed row; every accessor goes through this
        /// so a stray null in the response degrades to a dash instead of throwing mid-render.</summary>
        private static ProfileInfo Row(object row)
        {
            return row as ProfileInfo;
        }
    }
}
