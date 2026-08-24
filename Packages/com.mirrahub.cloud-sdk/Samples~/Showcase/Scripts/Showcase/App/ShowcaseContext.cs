using MirraCloud.Core;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// The ambient services every screen needs, bundled so a view constructor stays a two-argument
    /// affair no matter how many facilities we add. Without it the toast/dialog/navigation hosts
    /// stay trapped in <see cref="ShowcaseApp"/> and no view can perform a write operation.
    /// Only <see cref="Sdk"/> is guaranteed non-null: every other member is optional so a view can
    /// also be hosted outside the full app shell (gallery, tests) without crashing.
    /// </summary>
    public sealed class ShowcaseContext
    {
        public ShowcaseContext(IMirraCloudSdk sdk, RemoteImageLoader images, Toasts toasts,
                               Popup popup, Nav nav, RequestLog log)
        {
            Sdk = sdk;
            Images = images;
            Toasts = toasts;
            Popup = popup;
            Nav = nav;
            Log = log;
        }

        /// <summary>The SDK facade every view calls into.</summary>
        public IMirraCloudSdk Sdk { get; }

        /// <summary>Shared remote-texture cache; null means "render fallbacks only".</summary>
        public RemoteImageLoader Images { get; }

        /// <summary>Transient corner notifications for operation outcomes.</summary>
        public Toasts Toasts { get; }

        /// <summary>Modal host, shared by every dialog helper (FormDialog / ConfirmDialog).</summary>
        public Popup Popup { get; }

        /// <summary>Screen stack, so a view can push a nested screen instead of a dialog.</summary>
        public Nav Nav { get; }

        /// <summary>Journal of SDK traffic; views record every await into it.</summary>
        public RequestLog Log { get; }
    }
}
