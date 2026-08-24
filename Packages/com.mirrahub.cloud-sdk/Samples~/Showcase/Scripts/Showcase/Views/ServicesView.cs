using System;
using System.Collections.Generic;
using System.Globalization;
using MirraCloud.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>Profile data shown in the services-screen header.</summary>
    public sealed class ProfileHeader
    {
        public string Nickname;
        public string Username;
        public string AvatarUrl;
    }

    /// <summary>
    /// The post-login home: a header (profile, the project/branch this build talks to, the request
    /// journal, logout) and a search box over the SDK modules grouped by
    /// <see cref="ServiceCategory"/>. Cards are built once and only toggled while filtering, so a
    /// keystroke never rebuilds the tree. Tapping a card raises <see cref="ModuleOpened"/>; the
    /// header avatar stays a placeholder until <see cref="SetProfile"/> gets the loaded account.
    /// </summary>
    public sealed class ServicesView : VisualElement
    {
        // Long enough to fill the two description lines the card reserves, short enough that a
        // third line can never appear and push the capability badges out of a fixed-height card.
        private const int DescriptionChars = 96;

        // Section order is the reading order of the screen, not the enum order — it goes from
        // "this player" outwards to "this project's tooling".
        private static readonly ServiceCategory[] CategoryOrder =
        {
            ServiceCategory.Player,
            ServiceCategory.Social,
            ServiceCategory.LiveOps,
            ServiceCategory.Data,
            ServiceCategory.Tools,
        };

        public event Action<ServiceMeta> ModuleOpened;
        public event Action LogoutRequested;

        private readonly ShowcaseContext _ctx;
        private readonly Avatar _avatar;
        private readonly Label _name;
        private readonly Label _handle;
        private readonly List<CategorySection> _sections = new List<CategorySection>();
        private readonly VisualElement _zeroSlot;
        private VisualElement _backendPill;
        private readonly int _total;

        public ServicesView(ProfileHeader profile, ShowcaseContext ctx)
        {
            _ctx = ctx;
            AddToClassList("sc-services");

            var bar = new VisualElement();
            bar.AddToClassList("sc-svc-topbar");

            _avatar = new Avatar(40);
            bar.Add(_avatar);

            var texts = new VisualElement();
            texts.AddToClassList("sc-svc-topbar__texts");
            _name = new Label();
            _name.enableRichText = false;
            _name.AddToClassList("sc-svc-topbar__name");
            _handle = new Label();
            _handle.enableRichText = false;
            _handle.AddToClassList("sc-svc-topbar__handle");
            texts.Add(_name);
            texts.Add(_handle);
            bar.Add(texts);

            var spacer = new VisualElement();
            spacer.AddToClassList("sc-svc-topbar__spacer");
            bar.Add(spacer);

            bar.Add(BuildConnection());

            var log = BuildLogButton();
            if (log != null)
            {
                bar.Add(log);
            }

            var logout = new Button(() => LogoutRequested?.Invoke()) { text = "Logout" };
            logout.AddToClassList("sc-btn");
            logout.AddToClassList("sc-svc-topbar__logout");
            bar.Add(logout);
            Add(bar);

            var toolbar = new Toolbar();
            toolbar.AddToClassList("sc-svc-search");
            toolbar.WithSearch("Search services by name or description", ApplyFilter);
            Add(toolbar);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("sc-svc-scroll");

            _total = BuildSections(scroll);

            _zeroSlot = new VisualElement();
            _zeroSlot.AddToClassList("sc-svc-zero");
            _zeroSlot.style.display = DisplayStyle.None;
            scroll.Add(_zeroSlot);

            Add(scroll);

            SetProfile(profile);
        }

        /// <summary>Update the header from the loaded account (avatar URL + nickname/@handle).</summary>
        public void SetProfile(ProfileHeader profile)
        {
            string nick = profile != null && !string.IsNullOrEmpty(profile.Nickname) ? profile.Nickname : "Player";
            string user = profile != null ? profile.Username : null;
            _name.text = nick;

            // The username handle is optional and mutable, so an account that simply never set one
            // is not a guest — hide the line instead of mislabelling a signed-in player.
            bool hasHandle = !string.IsNullOrEmpty(user);
            _handle.text = hasHandle ? "@" + user : string.Empty;
            _handle.style.display = hasHandle ? DisplayStyle.Flex : DisplayStyle.None;
            _avatar.BindUrl(_ctx != null ? _ctx.Images : null, profile != null ? profile.AvatarUrl : null, nick);
        }

        // One section per category, in CategoryOrder; a category nobody uses is not rendered at all.
        // Returns how many modules ended up on screen.
        private int BuildSections(VisualElement host)
        {
            int placed = 0;
            foreach (var category in CategoryOrder)
            {
                CategorySection section = null;
                foreach (var meta in ShowcaseModules.All)
                {
                    if (meta.Category != category)
                    {
                        continue;
                    }
                    if (section == null)
                    {
                        section = new CategorySection(CategoryTitle(category));
                    }
                    section.Add(meta, BuildCard(meta));
                    placed++;
                }

                if (section == null)
                {
                    continue;
                }
                section.SyncCount(section.Count, false);
                _sections.Add(section);
                host.Add(section.Root);
            }

            if (placed != ShowcaseModules.All.Length)
            {
                // A module with a category outside CategoryOrder would silently vanish from the home
                // screen — the only place it can be opened from.
                Debug.LogWarning("[Showcase] services home rendered " + placed + " of "
                                 + ShowcaseModules.All.Length + " modules — unmapped ServiceCategory?");
            }
            return placed;
        }

        private VisualElement BuildCard(ServiceMeta m)
        {
            var card = new VisualElement();
            card.AddToClassList("sc-svc-card");
            // The card shows a truncated description; the tooltip keeps the full sentence reachable.
            card.tooltip = m.Description;

            var head = new VisualElement();
            head.AddToClassList("sc-svc-card__head");

            var icon = new VisualElement();
            icon.AddToClassList("sc-svc-card__icon");
            icon.style.backgroundColor = new Color(m.Accent.r, m.Accent.g, m.Accent.b, 0.16f);
            var glyph = new Label(m.Glyph);
            glyph.AddToClassList("sc-svc-card__glyph");
            glyph.AddToClassList("sc-icon");
            glyph.style.color = m.Accent;
            icon.Add(glyph);
            head.Add(icon);

            var title = new Label(m.Title);
            title.enableRichText = false;
            title.AddToClassList("sc-svc-card__title");
            title.style.color = m.Accent;
            head.Add(title);

            var desc = new Label(Fmt.Truncate(m.Description, DescriptionChars));
            desc.enableRichText = false;
            desc.AddToClassList("sc-svc-card__desc");
            head.Add(desc);

            card.Add(head);

            var caps = new VisualElement();
            caps.AddToClassList("sc-svc-card__caps");
            AddCapBadges(caps, m.Caps);
            card.Add(caps);

            card.RegisterCallback<ClickEvent>(_ => ModuleOpened?.Invoke(m));
            return card;
        }

        // "Read" alone is worth saying out loud (it promises the screen cannot change anything),
        // but next to "Write" it is noise — every writable service reads too.
        private static void AddCapBadges(VisualElement host, ServiceCaps caps)
        {
            if ((caps & ServiceCaps.Write) != 0)
            {
                host.Add(new Badge("Write", ChipTone.Accent));
            }
            else if ((caps & ServiceCaps.Read) != 0)
            {
                host.Add(new Badge("Read-only", ChipTone.Neutral));
            }

            if ((caps & ServiceCaps.Realtime) != 0)
            {
                host.Add(new Badge("Realtime", ChipTone.Info));
            }
        }

        // Which backend this build talks to. Read from the Configuration asset rather than from the
        // SDK, because it is also the answer to "why is this screen empty" when the ids are wrong.
        private VisualElement BuildConnection()
        {
            string project = null;
            string branch = null;
            try
            {
                var cfg = MirraCloud.Configuration.Load();
                if (cfg != null)
                {
                    project = cfg.ProjectId;
                    branch = cfg.BranchId;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] config load failed: " + e.Message);
            }

            var row = new VisualElement();
            row.AddToClassList("sc-svc-conn");

            bool hasProject = !string.IsNullOrWhiteSpace(project);
            row.Add(Pill(LucideIcon.Hash,
                hasProject ? Fmt.Id(project, 10) : "no project id",
                hasProject ? "Project " + project : "Set ProjectId on the MirraCloud Configuration asset",
                !hasProject));

            bool hasBranch = !string.IsNullOrWhiteSpace(branch);
            row.Add(Pill(LucideIcon.GitBranch,
                hasBranch ? Fmt.Truncate(branch, 18) : "default branch",
                hasBranch ? "Branch " + branch : "No BranchId set — the project default is used",
                false));

            _backendPill = Pill(LucideIcon.Wifi, "checking…", "Contacting the backend", false);
            row.Add(_backendPill);

            return row;
        }

        /// <summary>
        /// Reports whether the backend answered, from the first real call the app makes. Two static
        /// pills (project, branch) only say what the game was configured with; this one says whether
        /// that configuration actually reaches a server — the difference between "wrong id" and
        /// "backend is down" is the first thing anyone debugging the example needs.
        /// </summary>
        public void SetBackendStatus(bool reachable, RestApiResult result)
        {
            if (_backendPill == null)
            {
                return;
            }

            var glyph = _backendPill.Q<Label>(className: "sc-svc-conn__glyph");
            var text = _backendPill.Q<Label>(className: "sc-svc-conn__text");
            if (glyph == null || text == null)
            {
                return;
            }

            _backendPill.EnableInClassList("sc-svc-conn__pill--warn", !reachable);
            if (reachable)
            {
                glyph.text = LucideIcon.Wifi;
                text.text = "connected";
                long ms = result != null ? result.DurationMs : 0L;
                _backendPill.tooltip = ms > 0L ? "Backend answered in " + ms + " ms" : "Backend reachable";
                return;
            }

            glyph.text = LucideIcon.WifiOff;
            text.text = "unreachable";
            var error = result != null ? result.Error : null;
            string why = error != null && !string.IsNullOrEmpty(error.Message) ? error.Message : "no response";
            _backendPill.tooltip = "Backend did not answer: " + why;
        }

        private static VisualElement Pill(string glyph, string text, string tip, bool warn)
        {
            var pill = new VisualElement();
            pill.AddToClassList("sc-svc-conn__pill");
            if (warn)
            {
                pill.AddToClassList("sc-svc-conn__pill--warn");
            }
            pill.tooltip = tip;

            var g = new Label(glyph);
            g.AddToClassList("sc-svc-conn__glyph");
            g.AddToClassList("sc-icon");
            pill.Add(g);

            var t = new Label(text);
            t.enableRichText = false;
            t.AddToClassList("sc-svc-conn__text");
            pill.Add(t);
            return pill;
        }

        // The journal is app-wide, so it belongs on the home screen too — without it, traffic from a
        // service screen is only visible while that screen is open.
        private Button BuildLogButton()
        {
            if (_ctx == null || _ctx.Log == null || _ctx.Popup == null)
            {
                return null;
            }

            var btn = new Button(() => _ctx.Popup.Open(_ctx.Log.BuildPanel(), "Request log"))
            {
                text = LucideIcon.History
            };
            btn.tooltip = "Request log";
            btn.AddToClassList("sc-btn");
            btn.AddToClassList("sc-icon");
            btn.AddToClassList("sc-svc-topbar__icon-btn");
            return btn;
        }

        private void ApplyFilter(string query)
        {
            string q = query == null ? string.Empty : query.Trim();
            bool filtering = q.Length > 0;

            int visibleTotal = 0;
            foreach (var section in _sections)
            {
                int visible = section.Filter(q);
                section.SyncCount(visible, filtering);
                visibleTotal += visible;
            }

            _zeroSlot.Clear();
            if (visibleTotal > 0)
            {
                _zeroSlot.style.display = DisplayStyle.None;
                return;
            }

            _zeroSlot.style.display = DisplayStyle.Flex;
            _zeroSlot.Add(ZeroState.Panel(
                LucideIcon.Search,
                "No service matches that",
                "Nothing is named or described like \"" + Fmt.Truncate(q, 40) + "\". "
                + "Search covers the service name and the line under it, so a shorter word usually finds it.",
                null,
                null,
                "Clear the search box to see all " + _total.ToString(CultureInfo.InvariantCulture) + " services."));
        }

        private static string CategoryTitle(ServiceCategory category)
        {
            switch (category)
            {
                case ServiceCategory.Player: return "Player";
                case ServiceCategory.Social: return "Social";
                case ServiceCategory.LiveOps: return "Live Ops";
                case ServiceCategory.Data: return "Data";
                case ServiceCategory.Tools: return "Tools";
                default: return category.ToString();
            }
        }

        /// <summary>One category block: its header, its grid, and the cards it can hide while filtering.</summary>
        private sealed class CategorySection
        {
            private readonly List<ServiceMeta> _metas = new List<ServiceMeta>();
            private readonly List<VisualElement> _cards = new List<VisualElement>();
            private readonly SectionHeader _header;
            private readonly VisualElement _grid;

            public CategorySection(string title)
            {
                Root = new VisualElement();
                Root.AddToClassList("sc-svc-section");

                _header = new SectionHeader(title, "0");
                Root.Add(_header);

                _grid = new VisualElement();
                _grid.AddToClassList("sc-svc-grid");
                Root.Add(_grid);
            }

            public VisualElement Root { get; }

            public int Count => _cards.Count;

            public void Add(ServiceMeta meta, VisualElement card)
            {
                _metas.Add(meta);
                _cards.Add(card);
                _grid.Add(card);
            }

            /// <summary>Hides the cards that do not match and returns how many are left visible.</summary>
            public int Filter(string query)
            {
                int visible = 0;
                for (int i = 0; i < _cards.Count; i++)
                {
                    bool show = Matches(_metas[i], query);
                    _cards[i].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                    if (show)
                    {
                        visible++;
                    }
                }
                Root.style.display = visible > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                return visible;
            }

            /// <summary>While filtering the count reads "3 of 6", so a section never looks smaller than it is.</summary>
            public void SyncCount(int visible, bool filtering)
            {
                string text = visible.ToString(CultureInfo.InvariantCulture);
                if (filtering)
                {
                    text += " of " + _cards.Count.ToString(CultureInfo.InvariantCulture);
                }
                _header.SetCount(text);
            }

            private static bool Matches(ServiceMeta meta, string query)
            {
                if (string.IsNullOrEmpty(query))
                {
                    return true;
                }
                return Contains(meta.Title, query) || Contains(meta.Description, query);
            }

            private static bool Contains(string haystack, string needle)
            {
                return !string.IsNullOrEmpty(haystack)
                       && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
