using MirraCloud.Core;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace MirraCloud.Example.Showcase
{
    /// <summary>Runtime options for the showcase (set in the scene installer).</summary>
    public sealed class ShowcaseOptions
    {
        public bool DevForceServices;
    }

    /// <summary>
    /// Scene LifetimeScope for MC_Showcase. Owns the SDK singleton, grabs the scene's UIDocument,
    /// registers shared services, and runs <see cref="ShowcaseApp"/>. Self-contained on purpose:
    /// the scene runs as-is in any project the Example folder is dropped into.
    /// </summary>
    public sealed class ShowcaseInstaller : LifetimeScope
    {
        [Tooltip("Dev only: skip the auth gate and show the services screen (for visual QA without a backend).")]
        [SerializeField] private bool _devForceServices;

        protected override void Configure(IContainerBuilder builder)
        {
            // One SDK per app run, disposed with the scope. In your own game you would more
            // likely register it in a project-wide root scope instead of a scene one.
            builder.Register<MirraCloudSDK>(Lifetime.Singleton).As<IMirraCloudSdk>();

            builder.RegisterComponentInHierarchy<UIDocument>();
            builder.Register<RemoteImageLoader>(Lifetime.Singleton);
            builder.RegisterInstance(new ShowcaseOptions { DevForceServices = _devForceServices });
            builder.RegisterEntryPoint<ShowcaseApp>();
        }
    }
}
