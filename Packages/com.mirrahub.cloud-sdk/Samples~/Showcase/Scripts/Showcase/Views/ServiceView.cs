using System;
using System.Collections.Generic;
using MirraCloud.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Shared chrome for a per-service detail screen, laid out like the Cloud dashboard:
    /// header (back · accent glyph · title · description · capability badges · status chip),
    /// then an optional toolbar, then an optional tab strip, then the scrollable content column.
    /// <para>
    /// Everything past the header is opt-in: a view that only appends to <see cref="Content"/>
    /// gets exactly the old chrome. <see cref="UseToolbar"/>, <see cref="UseTabs"/>,
    /// <see cref="SetStatus"/> and <see cref="DeclareCall"/> each add one band when first called.
    /// </para>
    /// <para>
    /// There is exactly one scroller on the screen — <see cref="Content"/>. <see cref="UseTabs"/>
    /// keeps the tab strip pinned above it and moves the tab panes *into* it, so tabbed and
    /// untabbed screens scroll identically and no nested ScrollView can steal the wheel.
    /// </para>
    /// <see cref="Populate"/> runs once from the constructor, and again on every
    /// <see cref="Refresh"/>.
    /// </summary>
    public abstract class ServiceView : VisualElement
    {
        // Populate() only *starts* the async loads, so a refresh has no completion to wait for.
        // This is how long the toolbar stays in its busy look afterwards — enough for the click to
        // register visually, short enough not to pretend it is tracking real progress.
        private const long BusyReleaseMs = 350;

        protected readonly ServiceMeta Meta;

        /// <summary>Ambient services (SDK, toasts, dialogs, navigation, request log).</summary>
        protected readonly ShowcaseContext Ctx;

        /// <summary>
        /// Scrollable column the concrete view appends sections into — and, when the view calls
        /// <see cref="UseTabs"/>, the host of the tab panes as well.
        /// </summary>
        protected readonly ScrollView Content;

        private readonly List<SdkCall> _calls = new List<SdkCall>();
        private readonly Label _subtitle;
        private readonly VisualElement _statusSlot;

        private Toolbar _toolbar;
        private Tabs _tabs;
        private bool _toolbarStale;
        private bool _toolbarSettled;
        private bool _sdkButtonAdded;
        private bool _rebuilding;
        private bool _busy;
        private int _busyToken;

        protected ServiceView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
        {
            Meta = meta;
            if (ctx == null)
            {
                // Every view issues SDK calls from Populate(); a context without an Sdk would only
                // turn that into a NullReferenceException several frames later, in async code.
                throw new ArgumentNullException(nameof(ctx), "ServiceView needs a ShowcaseContext with an SDK");
            }
            Ctx = ctx;

            AddToClassList("sc-detail");

            var header = new VisualElement();
            header.AddToClassList("sc-detail__header");
            header.AddToClassList("sc-svc-head");

            var back = new Button(() => onBack?.Invoke()) { text = LucideIcon.ArrowLeft };
            back.AddToClassList("sc-back-btn");
            back.AddToClassList("sc-icon");
            header.Add(back);

            // same accent-tinted tile as the module card on the services screen, so opening a card
            // reads as a zoom-in rather than a jump to an unrelated page
            var iconBox = new VisualElement();
            iconBox.AddToClassList("sc-svc-head__icon");
            iconBox.style.backgroundColor = new Color(meta.Accent.r, meta.Accent.g, meta.Accent.b, 0.16f);
            var glyph = new Label(meta.Glyph);
            glyph.AddToClassList("sc-svc-head__glyph");
            glyph.AddToClassList("sc-icon");
            glyph.style.color = meta.Accent;
            iconBox.Add(glyph);
            header.Add(iconBox);

            var texts = new VisualElement();
            texts.AddToClassList("sc-svc-head__texts");

            var titleRow = new VisualElement();
            titleRow.AddToClassList("sc-svc-head__title-row");
            var title = new Label(meta.Title);
            title.enableRichText = false;
            title.AddToClassList("sc-detail__title");
            title.style.color = meta.Accent;
            titleRow.Add(title);

            var caps = BuildCaps(meta.Caps);
            if (caps != null)
            {
                titleRow.Add(caps);
            }
            texts.Add(titleRow);

            _subtitle = new Label();
            _subtitle.enableRichText = false;
            _subtitle.AddToClassList("sc-svc-head__subtitle");
            texts.Add(_subtitle);
            header.Add(texts);

            _statusSlot = new VisualElement();
            _statusSlot.AddToClassList("sc-svc-head__status");
            _statusSlot.style.display = DisplayStyle.None;
            header.Add(_statusSlot);

            Add(header);

            Content = new ScrollView(ScrollViewMode.Vertical);
            Content.AddToClassList("sc-svc-content");
            Add(Content);

            SetSubtitle(meta.Description);

            Populate();
            schedule.Execute(WarnOnStrandedCalls).StartingIn(0);
        }

        // Shorthands kept as properties (not fields) so the 22 concrete views read unchanged while
        // the context stays the single source of truth. All of them may be null except Sdk.
        protected IMirraCloudSdk Sdk => Ctx.Sdk;
        protected RemoteImageLoader Images => Ctx.Images;
        protected Toasts Toasts => Ctx.Toasts;
        protected Popup Popup => Ctx.Popup;
        protected RequestLog Log => Ctx.Log;

        protected abstract void Populate();

        /// <summary>
        /// Replaces the one-liner under the title. Defaults to <c>ServiceMeta.Description</c>;
        /// pass null or an empty string to hide the line entirely.
        /// </summary>
        protected void SetSubtitle(string text)
        {
            _subtitle.text = text ?? string.Empty;
            _subtitle.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>
        /// Sets the chip at the right edge of the header — the screen-wide verdict ("Configured",
        /// "3 channels", "Not available on this platform"), not a per-row status. Passing null or
        /// an empty string clears it.
        /// </summary>
        protected void SetStatus(string text, ChipTone tone = ChipTone.Neutral)
        {
            _statusSlot.Clear();
            if (string.IsNullOrEmpty(text))
            {
                _statusSlot.style.display = DisplayStyle.None;
                return;
            }
            _statusSlot.style.display = DisplayStyle.Flex;
            _statusSlot.Add(new Chip(text, tone));
        }

        /// <summary>
        /// Creates (once) the toolbar band between the header and the content, and returns it for
        /// fluent configuration. A <c>&lt;/&gt; SDK call</c> button is appended automatically at the
        /// end of the line when the view has declared at least one <see cref="DeclareCall"/> and the
        /// context has a popup host to open the drawer in.
        /// </summary>
        protected Toolbar UseToolbar()
        {
            if (_toolbar != null && !_toolbarStale)
            {
                return _toolbar;
            }

            var fresh = new Toolbar();
            fresh.AddToClassList("sc-svc__toolbar");

            if (_toolbar != null)
            {
                // rebuild pass (see Refresh): swap in an empty toolbar in the same slot, so
                // Populate() re-adds its search/filters instead of stacking a second copy of each
                int at = IndexOf(_toolbar);
                _toolbar.RemoveFromHierarchy();
                Insert(at, fresh);
            }
            else
            {
                // above the tab strip even when the view asked for the tabs first
                var below = _tabs != null && _tabs.parent == this ? (VisualElement)_tabs : Content;
                Insert(IndexOf(below), fresh);
            }

            _toolbar = fresh;
            _toolbarStale = false;
            _toolbarSettled = false;
            _sdkButtonAdded = false;

            // Refresh() sets the busy look before Populate() swaps this toolbar in, so without
            // carrying the flag across the rebuild the click would give no feedback at all.
            if (_busy)
            {
                fresh.SetBusy(true);
            }

            // Deferred by one frame so the button lands after everything Populate() adds to the
            // toolbar, whichever order the view calls UseToolbar()/DeclareCall() in.
            schedule.Execute(SettleToolbar).StartingIn(0);
            return _toolbar;
        }

        /// <summary>
        /// Creates (once) the tab strip below the toolbar and returns it, so the view can
        /// <c>Add(title, () =&gt; BuildPane())</c> its panes.
        /// <para>
        /// The strip stays pinned while the panes are re-parented into <see cref="Content"/>: the
        /// screen keeps a single scroller, and a pane is a plain column that must not scroll on its
        /// own. Anything appended to <see cref="Content"/> before this call scrolls above the panes,
        /// anything after scrolls below them — so call it once the fixed sections are in place.
        /// </para>
        /// </summary>
        protected Tabs UseTabs()
        {
            if (_tabs != null)
            {
                return _tabs;
            }

            _tabs = new Tabs();
            _tabs.AddToClassList("sc-svc__tabs");
            Insert(IndexOf(Content), _tabs);

            // Tabs keeps a direct reference to its pane host and only toggles `display` on the
            // children, so moving that host elsewhere in the tree leaves Select/Invalidate/Clear2
            // fully working — it just decides where the panes are painted.
            var panes = _tabs.Q<VisualElement>(className: "sc-tabs__content");
            if (panes != null)
            {
                panes.RemoveFromHierarchy();
                panes.AddToClassList("sc-svc__panes");
                Content.Add(panes);
            }
            else
            {
                Debug.LogWarning("[Showcase] " + Meta.Id + ": tab pane host not found, falling back to a scrolling strip");
                _tabs.RemoveFromHierarchy();
                Content.Add(_tabs);
            }
            return _tabs;
        }

        /// <summary>
        /// Registers the C# behind this screen for the toolbar's <c>&lt;/&gt;</c> drawer. Calling it
        /// again with the same title and snippet is a no-op, so declaring from
        /// <see cref="Populate"/> survives a <see cref="Refresh"/> without duplicating entries.
        /// </summary>
        protected void DeclareCall(SdkCall call)
        {
            if (call == null)
            {
                return;
            }

            foreach (var known in _calls)
            {
                if (known.Title == call.Title && known.Snippet == call.Snippet)
                {
                    return;
                }
            }
            _calls.Add(call);

            // Before the toolbar has settled the button is added by SettleToolbar, which keeps it
            // last on the line; afterwards (a call declared from an async callback) add it here.
            if (_toolbarSettled)
            {
                EnsureSdkCallButton();
            }
        }

        /// <summary>
        /// Rebuilds the screen: empties <see cref="Content"/> (dropping the tab panes with it) and
        /// runs <see cref="Populate"/> again. Wire it to <c>Toolbar.WithRefresh</c>. Override when a
        /// view can reload in place — a full rebuild resets the scroll position and the tab
        /// selection, which is jarring on screens with a lot of state.
        /// </summary>
        protected virtual void Refresh()
        {
            if (_rebuilding)
            {
                Debug.LogWarning("[Showcase] " + Meta.Id + ": Refresh() re-entered from Populate(), ignored");
                return;
            }

            _rebuilding = true;
            SetBusy(true);
            int token = _busyToken;
            try
            {
                ResetChrome();
                Populate();
            }
            finally
            {
                _rebuilding = false;
            }

            // Only release the state this refresh set: a view that took the toolbar busy for its own
            // long operation during Populate() keeps it.
            schedule.Execute(() =>
            {
                if (_busyToken == token)
                {
                    SetBusy(false);
                }
            }).StartingIn(BusyReleaseMs);
        }

        /// <summary>Puts the toolbar (if the view made one) into its in-flight look.</summary>
        protected void SetBusy(bool busy)
        {
            _busyToken++;
            _busy = busy;
            if (_toolbar != null)
            {
                _toolbar.SetBusy(busy);
            }
        }

        /// <summary>Appends an empty bound slot (used as a target for <see cref="ViewBind"/>).</summary>
        protected VisualElement AddSlot(float marginBottom = 14f)
        {
            var slot = new VisualElement();
            slot.style.marginBottom = marginBottom;
            Content.Add(slot);
            return slot;
        }

        /// <summary>Swap a slot's contents (mirrors ViewBind's private replace, for manual fan-out loads).</summary>
        protected static void Replace(VisualElement slot, VisualElement content)
        {
            if (slot == null)
            {
                return;
            }
            slot.Clear();
            if (content != null)
            {
                slot.Add(content);
            }
        }

        /// <summary>Tears down everything a <see cref="Populate"/> pass built, leaving the header.</summary>
        private void ResetChrome()
        {
            if (_tabs != null)
            {
                // the pane host is a child of Content, so Content.Clear() below disposes of it
                _tabs.RemoveFromHierarchy();
                _tabs = null;
            }
            Content.Clear();
            _toolbarStale = _toolbar != null;

            // Populate() re-declares whatever it needs. Keeping the old entries would pile up a new
            // copy on every refresh for any snippet that embeds a runtime value (a board id, a
            // selected key), since DeclareCall only dedupes on an exact title+snippet match.
            _calls.Clear();
            _sdkButtonAdded = false;
        }

        private void SettleToolbar()
        {
            _toolbarSettled = true;
            EnsureSdkCallButton();
        }

        // The drawer is reachable only through the toolbar button, so snippets declared by a view
        // that never asked for a toolbar are unreachable. Silent in that case would be worse than
        // noisy: the whole point of DeclareCall is that the client can read the call.
        private void WarnOnStrandedCalls()
        {
            if (_calls.Count > 0 && _toolbar == null)
            {
                Debug.LogWarning("[Showcase] " + Meta.Id + ": DeclareCall used without UseToolbar() — "
                    + "the declared snippets have nowhere to open from");
            }
        }

        private void EnsureSdkCallButton()
        {
            // No popup host (a view hosted outside the app shell) means nowhere to open the drawer,
            // so the button would be a dead control — skip it rather than fail on click.
            if (_sdkButtonAdded || _toolbar == null || _calls.Count == 0 || Ctx.Popup == null)
            {
                return;
            }
            _sdkButtonAdded = true;
            _toolbar.WithSdkCall(OpenSdkCalls);
        }

        private void OpenSdkCalls()
        {
            var popup = Ctx.Popup;
            if (popup == null)
            {
                return;
            }
            // The dialog itself does not scroll (.sc-dialog caps width only), and the drawer grows
            // with every declared call plus the whole request journal — without this it runs off
            // the bottom of the screen with no way to reach the rest.
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 520f;
            scroll.Add(SdkCallDrawer.Build(_calls, Ctx.Log));
            popup.Open(scroll, Meta.Title + " · SDK calls");
        }

        /// <summary>Mirrors the badges on the module card, so opening a card does not silently
        /// change what the service claims to do. Returns null when there is nothing to say.</summary>
        private static VisualElement BuildCaps(ServiceCaps caps)
        {
            bool read = (caps & ServiceCaps.Read) != 0;
            bool write = (caps & ServiceCaps.Write) != 0;
            bool realtime = (caps & ServiceCaps.Realtime) != 0;
            if (!write && !realtime && !read)
            {
                return null;
            }

            var row = new VisualElement();
            row.AddToClassList("sc-svc-head__caps");
            if (read && !write)
            {
                row.Add(new Badge("Read-only", ChipTone.Neutral));
            }
            if (write)
            {
                row.Add(new Badge("Write", ChipTone.Accent));
            }
            if (realtime)
            {
                row.Add(new Badge("Realtime", ChipTone.Info));
            }
            return row;
        }
    }
}
