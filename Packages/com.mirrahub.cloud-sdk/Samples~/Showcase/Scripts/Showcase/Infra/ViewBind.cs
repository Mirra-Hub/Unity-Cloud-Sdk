using System;
using MirraCloud.Core;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Everything a bound load needs beyond the call itself. Kept as a bag of fields (not ctor
    /// parameters) so a call site only spells out what it actually cares about; every field is
    /// optional and an absent one degrades to the plain behaviour.
    /// </summary>
    public sealed class BindOptions
    {
        /// <summary>Journal to record the finished call in. Null keeps the call out of the log.</summary>
        public RequestLog Log;

        /// <summary>Human label for the journal row ("Leaderboard page"); falls back to the route.</summary>
        public string Label;

        /// <summary>The C# behind the call — shown in the journal row and in the <c>&lt;/&gt;</c> drawer.</summary>
        public string Snippet;

        /// <summary>Service name used by the "not set up yet" state, e.g. "Tournament".
        /// Keep it singular — the state composes sentences around it.</summary>
        public string ServiceName;

        /// <summary>
        /// Marks this call as the one that fetches the service's configuration. Only then does a
        /// 404/501 mean "nothing is set up in this project": on a data call the same code usually
        /// means "this player has no entry yet", which is an empty state, not a misconfiguration.
        /// </summary>
        public bool ConfigurationRequest;

        /// <summary>Custom empty view; wins over <see cref="EmptyMessage"/>.</summary>
        public Func<VisualElement> EmptyView;

        /// <summary>One line explaining how data gets here, shown when the response is empty.</summary>
        public string EmptyMessage;

        /// <summary>Offer a Retry button on transport/server failures (re-runs the whole call).</summary>
        public bool AllowRetry;
    }

    /// <summary>
    /// Binds the SDK's uniform <c>AsyncOperation&lt;RestApiResult&lt;T&gt;&gt;</c> to a UI slot,
    /// driving it through Loading → {Data | Empty | Not configured | No access | Error}. The SDK
    /// never throws on HTTP (failures are values), so this branches on
    /// <see cref="RestApiResult.IsSuccess"/> and always renders from <c>r.Data</c> (never a stale
    /// cache). Telling "empty" from "not set up" from "forbidden" from "broken" matters here: they
    /// look identical on the wire but the reader's next move is completely different for each.
    /// </summary>
    public static class ViewBind
    {
        private const string SessionExpired =
            "Your session has expired. Sign in again to continue.";

        /// <summary>
        /// Original binding: loading → data / empty / error, with no journal, no retry and no
        /// status-code taxonomy. Kept verbatim because the service views lean on it.
        /// </summary>
        public static void Load<T>(
            AsyncOperation<RestApiResult<T>> op,
            VisualElement slot,
            Func<T, VisualElement> render,
            Func<T, bool> isEmpty = null,
            Func<VisualElement> emptyView = null)
        {
            // The op is already in flight, so there is nothing to re-run — a retry here would rebind
            // the very same (finished) operation, which is why this path never offers one.
            Run(() => op, slot, render, isEmpty, new BindOptions { EmptyView = emptyView }, false);
        }

        /// <summary>Rich binding without an emptiness test (any successful response renders).</summary>
        public static void Load<T>(
            Func<AsyncOperation<RestApiResult<T>>> start,
            VisualElement slot,
            Func<T, VisualElement> render,
            BindOptions options)
        {
            Load(start, slot, render, null, options);
        }

        /// <summary>
        /// Rich binding. Takes a *factory* rather than a started operation so the failure state can
        /// re-issue the call, and reads <see cref="RestApiResult.HttpStatusCode"/> to distinguish
        /// "nothing configured" (404/501) and "no access" (403) from a genuine breakage.
        /// </summary>
        public static void Load<T>(
            Func<AsyncOperation<RestApiResult<T>>> start,
            VisualElement slot,
            Func<T, VisualElement> render,
            Func<T, bool> isEmpty,
            BindOptions options)
        {
            Run(start, slot, render, isEmpty, options ?? new BindOptions(), true);
        }

        private static async void Run<T>(
            Func<AsyncOperation<RestApiResult<T>>> start,
            VisualElement slot,
            Func<T, VisualElement> render,
            Func<T, bool> isEmpty,
            BindOptions options,
            bool richStates)
        {
            if (slot == null)
            {
                return;
            }

            Skeleton.Into(slot);

            // Liveness tracking. A slot that reached a panel and left it again belongs to a screen
            // the user has walked away from, so the response must not be painted into that (now
            // detached) tree. A slot that has *never* been attached is the opposite case: views build
            // their subtree before it is added, and SDK calls that fail validation complete
            // synchronously — dropping those would leave the skeleton up forever.
            bool sawPanel = slot.panel != null;
            EventCallback<AttachToPanelEvent> watch = _ => sawPanel = true;
            slot.RegisterCallback(watch);

            AsyncOperation<RestApiResult<T>> op = null;
            try
            {
                op = start != null ? start() : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] ViewBind failed to start '" + (options.Label ?? "request") + "': " + e.Message);
            }

            RestApiResult<T> result = null;
            if (op != null)
            {
                await op.Task();
                result = op.Result;
            }

            slot.UnregisterCallback(watch);

            // The call happened whether or not anyone is still watching, so it belongs in the journal.
            if (result != null && options.Log != null)
            {
                options.Log.Record(options.Label, result, options.Snippet);
            }

            void Apply()
            {
                Replace(slot, Outcome(result, slot, start, render, isEmpty, options, richStates));
            }

            if (sawPanel && slot.panel == null)
            {
                // Navigation is element-preserving (Nav re-adds the same screen on Back), so hold the
                // outcome instead of discarding it — otherwise the screen returns wearing a skeleton.
                DeferUntilAttached(slot, Apply);
                return;
            }

            Apply();
        }

        private static VisualElement Outcome<T>(
            RestApiResult<T> result,
            VisualElement slot,
            Func<AsyncOperation<RestApiResult<T>>> start,
            Func<T, VisualElement> render,
            Func<T, bool> isEmpty,
            BindOptions options,
            bool richStates)
        {
            if (result == null || !result.IsSuccess)
            {
                return Failure(result, slot, start, render, isEmpty, options, richStates);
            }

            try
            {
                if (isEmpty != null && isEmpty(result.Data))
                {
                    return Empty(options);
                }
                return render(result.Data);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] ViewBind render failed for '" + (options.Label ?? "request") + "': " + e);
                return ErrorState.Message(e.Message);
            }
        }

        private static VisualElement Empty(BindOptions options)
        {
            if (options.EmptyView != null)
            {
                return options.EmptyView();
            }
            if (!string.IsNullOrEmpty(options.EmptyMessage))
            {
                // EmptyMessage is the explanatory line, not the headline — Panel's second parameter
                // is the title, so passing it there would render the sentence in heading type.
                return ZeroState.Panel(LucideIcon.Inbox, "Nothing here yet", options.EmptyMessage);
            }
            return EmptyState.Default();
        }

        private static VisualElement Failure<T>(
            RestApiResult<T> result,
            VisualElement slot,
            Func<AsyncOperation<RestApiResult<T>>> start,
            Func<T, VisualElement> render,
            Func<T, bool> isEmpty,
            BindOptions options,
            bool richStates)
        {
            var error = result?.Error;
            if (!richStates)
            {
                return ErrorState.Build(error);
            }

            // On transport failures the meta lives on the error rather than on the result.
            long? code = result?.HttpStatusCode ?? error?.HttpStatusCode;

            if ((code == 404 || code == 501) && options.ConfigurationRequest)
            {
                // Not "broken": the project simply has no such configuration, and the fix is in the
                // Mirra Hub console rather than in the game. Only opt-in, because on a data call a
                // 404 usually means "this player has no row yet", which is an empty state.
                return ZeroState.NotConfigured(options.ServiceName);
            }
            if (code == 403)
            {
                return ZeroState.Forbidden();
            }
            if (code == 401)
            {
                return ErrorState.Message(SessionExpired);
            }

            var state = ErrorState.Build(error);
            if (!options.AllowRetry)
            {
                return state;
            }

            var box = new VisualElement();
            box.AddToClassList("sc-bind-error");
            box.Add(state);

            var retry = new Button(() => Run(start, slot, render, isEmpty, options, true)) { text = "Retry" };
            retry.AddToClassList("sc-btn");
            retry.AddToClassList("sc-btn--primary");
            retry.AddToClassList("sc-bind-error__retry");
            box.Add(retry);

            return box;
        }

        /// <summary>Runs <paramref name="apply"/> the next time the slot joins a panel, once.</summary>
        private static void DeferUntilAttached(VisualElement slot, Action apply)
        {
            EventCallback<AttachToPanelEvent> once = null;
            once = _ =>
            {
                slot.UnregisterCallback(once);
                apply();
            };
            slot.RegisterCallback(once);
        }

        private static void Replace(VisualElement slot, VisualElement content)
        {
            slot.Clear();
            if (content != null)
            {
                slot.Add(content);
            }
        }
    }
}
