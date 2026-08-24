using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// WebView screen: opens a page inside the game and reports back what happens on it.
    /// <para>
    /// Nothing here is REST — this is a native bridge, so the screen is a control panel plus a live
    /// event log. The bridge does not exist in the Editor or on WebGL, so the screen says that
    /// plainly and keeps its controls disabled rather than failing on click.
    /// </para>
    /// </summary>
    public sealed class WebViewView : ServiceView
    {
        private const string OpenSnippet =
@"// Nothing to await: the bridge is fire-and-forget and answers through events.
sdk.WebView.OnPageStarted += url => Debug.Log(""started "" + url);
sdk.WebView.OnPageLoaded  += url => Debug.Log(""loaded "" + url);
sdk.WebView.OnUrlHooked   += url => HandleReturn(url);   // your redirect came back
sdk.WebView.OnMessageReceived += payload => HandleMessage(payload);
sdk.WebView.OnError       += message => Debug.LogError(message);

sdk.WebView.LoadUrl(""https://example.com/login"");
sdk.WebView.SetVisibility(true);";

        private const string HookSnippet =
@"// The hook pattern is what makes a login or payment flow usable: the page redirects to a
// URL you own, the bridge intercepts it instead of navigating, and OnUrlHooked hands you
// the whole URL — query string included — so you can read the code out of it.
sdk.WebView.SetUrlPattern(
    allowPattern: ""https://.*"",
    denyPattern: null,
    hookPattern: ""myapp://auth/callback.*"");

sdk.WebView.OnUrlHooked += url => {
    // parse the token or code out of url, then close the view
    sdk.WebView.SetVisibility(false);
};";

        private const string ControlSnippet =
@"sdk.WebView.LoadHtml(""<h1>Hello</h1>"", baseUrl: null);
sdk.WebView.EvaluateJS(""document.title"");
sdk.WebView.SetMargins(left: 0, top: 120, right: 0, bottom: 0);
sdk.WebView.SetVisibility(false);

if (sdk.WebView.CanGoBack())
{
    sdk.WebView.GoBack();
}

// Capability flags, all safe to read before the bridge is up:
bool ready = sdk.WebView.IsReady;
bool canHook = sdk.WebView.SupportsUrlHooking;
bool fallback = sdk.WebView.IsBrowserFallback;   // opens the system browser instead";

        private const int MaxEvents = 60;

        private readonly List<string> _events = new List<string>();

        private TextField _url;
        private VisualElement _eventSlot;

        private Action<string> _onStarted;
        private Action<string> _onLoaded;
        private Action<string> _onHooked;
        private Action<string> _onMessage;
        private Action<string> _onError;
        private Action<string> _onHttpError;

        public WebViewView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
        }

        protected override void Populate()
        {
            _events.Clear();

            DeclareCall(new SdkCall("Open a page and listen", OpenSnippet));
            DeclareCall(new SdkCall("Hook a redirect back into the game", HookSnippet,
                "This is how an OpenID sign-in or a payment return is caught."));
            DeclareCall(new SdkCall("Drive the view", ControlSnippet));

            UseToolbar().WithSpacer().WithRefresh(Refresh);

            Subscribe();
            SyncStatus();

            Content.Add(BuildAvailability());
            Content.Add(BuildOpenCard());

            Content.Add(new SectionHeader("Bridge events"));
            _eventSlot = AddSlot();
            RenderEvents();
        }

        // ----- availability ---------------------------------------------------------------------

        private bool Available
        {
            get { return Sdk.WebView != null && Sdk.WebView.IsReady; }
        }

        private void SyncStatus()
        {
            if (Sdk.WebView == null)
            {
                SetStatus("Not available", ChipTone.Bad);
                return;
            }
            if (Sdk.WebView.IsReady)
            {
                SetStatus(Sdk.WebView.IsBrowserFallback ? "System browser" : "Ready", ChipTone.Ok);
                return;
            }
            SetStatus("Not available here", ChipTone.Warn);
        }

        private VisualElement BuildAvailability()
        {
            var card = new Card(Meta.Accent);
            card.WithTitle("Where this works", Meta.Accent);

            var text = new Label("The WebView is a native bridge: it exists in device builds, not in the "
                + "Editor and not on WebGL. The controls below stay disabled where it is unavailable, so "
                + "the whole surface is still readable without anything failing on click.");
            text.AddToClassList("sc-fs-hint");
            card.Body.Add(text);

            var chips = new VisualElement();
            chips.AddToClassList("sc-chip-row");
            chips.Add(new Chip(Available ? "bridge ready" : "bridge unavailable",
                Available ? ChipTone.Ok : ChipTone.Warn));
            if (Sdk.WebView != null)
            {
                chips.Add(new Chip(Sdk.WebView.SupportsUrlHooking ? "url hooking" : "no url hooking",
                    Sdk.WebView.SupportsUrlHooking ? ChipTone.Info : ChipTone.Neutral));
                chips.Add(new Chip(Sdk.WebView.IsBrowserFallback ? "opens system browser" : "in-app view",
                    Sdk.WebView.IsBrowserFallback ? ChipTone.Warn : ChipTone.Neutral));
            }
            chips.Add(new Chip(Application.platform.ToString(), ChipTone.Neutral));
            card.Body.Add(chips);

            if (!Available)
            {
                card.Body.Add(ZeroState.Panel(LucideIcon.Globe, "Nothing to drive from here",
                    "Run a device build to try it. This example's own sign-in already uses the bridge: "
                    + "an external provider opens in the WebView and its redirect is caught with a hook "
                    + "pattern — the code drawer shows the shape of it.",
                    hint: "Editor and WebGL fall back to the system browser, or to nothing at all."));
            }
            return card;
        }

        // ----- controls -------------------------------------------------------------------------

        private VisualElement BuildOpenCard()
        {
            var card = new Card(Meta.Accent);
            card.WithTitle("Open a page", Meta.Accent);

            _url = new TextField { label = "URL", value = "https://example.com" };
            _url.AddToClassList("sc-field");
            card.Body.Add(_url);

            var controls = new VisualElement();
            controls.AddToClassList("sc-chip-row");

            controls.Add(Control("Open", () =>
            {
                string url = _url != null ? _url.value : null;
                if (string.IsNullOrWhiteSpace(url))
                {
                    if (Toasts != null)
                    {
                        Toasts.Info("Type a URL first");
                    }
                    return;
                }
                Sdk.WebView.LoadUrl(url.Trim());
                Sdk.WebView.SetVisibility(true);
                Log("load · " + Fmt.Truncate(url.Trim(), 60));
            }, true));

            controls.Add(Control("Show", () =>
            {
                Sdk.WebView.SetVisibility(true);
                Log("visibility · shown");
            }, false));

            controls.Add(Control("Hide", () =>
            {
                Sdk.WebView.SetVisibility(false);
                Log("visibility · hidden");
            }, false));

            controls.Add(Control("Back", () =>
            {
                if (Sdk.WebView.CanGoBack())
                {
                    Sdk.WebView.GoBack();
                    Log("navigate · back");
                    return;
                }
                Log("navigate · nothing to go back to");
            }, false));

            controls.Add(Control("Forward", () =>
            {
                if (Sdk.WebView.CanGoForward())
                {
                    Sdk.WebView.GoForward();
                    Log("navigate · forward");
                    return;
                }
                Log("navigate · nothing to go forward to");
            }, false));

            controls.Add(Control("Run JS", () =>
            {
                Sdk.WebView.EvaluateJS("window.location.href");
                Log("evaluate · window.location.href");
            }, false));

            card.Body.Add(controls);

            var hookHint = new Label("A real sign-in calls SetUrlPattern first, so the provider's "
                + "redirect is caught by the game instead of being followed by the page.");
            hookHint.AddToClassList("sc-fs-hint");
            card.Body.Add(hookHint);
            return card;
        }

        private Button Control(string text, Action run, bool primary)
        {
            var button = new Button(run) { text = text };
            button.AddToClassList("sc-btn");
            if (primary)
            {
                button.AddToClassList("sc-btn--primary");
            }

            // Disabled rather than absent: the reader should see the whole surface even where the
            // bridge cannot run.
            button.SetEnabled(Available);
            if (!Available)
            {
                button.tooltip = "The WebView bridge is not available on this platform";
            }
            return button;
        }

        // ----- events ---------------------------------------------------------------------------

        private void Subscribe()
        {
            var webView = Sdk.WebView;
            if (webView == null || _onStarted != null)
            {
                return;
            }

            _onStarted = url => Log("page started · " + Fmt.Truncate(url, 60));
            _onLoaded = url => Log("page loaded · " + Fmt.Truncate(url, 60));
            _onHooked = url => Log("url hooked · " + Fmt.Truncate(url, 60));
            _onMessage = payload => Log("message · " + Fmt.Truncate(payload, 60));
            _onError = message => Log("error · " + Fmt.Truncate(message, 60));
            _onHttpError = message => Log("http error · " + Fmt.Truncate(message, 60));

            webView.OnPageStarted += _onStarted;
            webView.OnPageLoaded += _onLoaded;
            webView.OnUrlHooked += _onHooked;
            webView.OnMessageReceived += _onMessage;
            webView.OnError += _onError;
            webView.OnHttpError += _onHttpError;
        }

        private void Unsubscribe()
        {
            var webView = Sdk.WebView;
            if (webView == null || _onStarted == null)
            {
                return;
            }

            webView.OnPageStarted -= _onStarted;
            webView.OnPageLoaded -= _onLoaded;
            webView.OnUrlHooked -= _onHooked;
            webView.OnMessageReceived -= _onMessage;
            webView.OnError -= _onError;
            webView.OnHttpError -= _onHttpError;
            _onStarted = null;
        }

        /// <summary>
        /// Bridge callbacks arrive from native code, so the UI work is handed to the scheduler rather
        /// than done on whatever thread delivered the event.
        /// </summary>
        private void Log(string text)
        {
            schedule.Execute(() =>
            {
                _events.Insert(0, Fmt.Time(DateTime.Now) + "  " + text);
                if (_events.Count > MaxEvents)
                {
                    _events.RemoveAt(_events.Count - 1);
                }
                RenderEvents();
            }).StartingIn(0);
        }

        private void RenderEvents()
        {
            if (_eventSlot == null)
            {
                return;
            }
            _eventSlot.Clear();

            if (_events.Count == 0)
            {
                _eventSlot.Add(ZeroState.Panel(LucideIcon.ScrollText, "No events yet",
                    "Page loads, intercepted redirects, messages from the page and errors all land here "
                    + "as they happen — this log is how a game debugs a sign-in flow that opens a page."));
                return;
            }

            var box = new VisualElement();
            box.AddToClassList("sc-chat-events__body");
            box.style.display = DisplayStyle.Flex;
            foreach (var line in _events)
            {
                var label = new Label(line);
                label.enableRichText = false;
                label.AddToClassList("sc-chat-events__line");
                box.Add(label);
            }
            _eventSlot.Add(box);
        }
    }
}
