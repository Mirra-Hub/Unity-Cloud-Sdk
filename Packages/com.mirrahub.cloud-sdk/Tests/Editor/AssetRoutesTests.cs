using MirraCloud.Core.AssetsStorage;
using NUnit.Framework;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// The literal routes, pinned. They compile whatever they say: a whole-word rename of <c>id</c> to
    /// <c>stableId</c> once turned <c>by-stable-id</c> into <c>by-stable-stableId</c>, and every download
    /// answered 404 while the build stayed green.
    /// </summary>
    [TestFixture]
    public class AssetRoutesTests
    {
        private static readonly AssetAddress ById = AssetAddress.ById("64b7f0c2e1a2b3c4d5e6f7a8");
        private static readonly AssetAddress ByPath = AssetAddress.ByPath("/icons/coin 1.png", "icons/coin%201.png");

        [Test]
        public void The_players_route_by_stable_id()
        {
            Assert.That(AssetRoutes.Download("p1", "main", false, ById),
                Is.EqualTo("/assets/v1/projects/p1/branches/main/assets/by-stable-id/64b7f0c2e1a2b3c4d5e6f7a8"));
        }

        [Test]
        public void The_players_route_by_path_carries_the_escaped_form()
        {
            Assert.That(AssetRoutes.Download("p1", "main", false, ByPath),
                Is.EqualTo("/assets/v1/projects/p1/branches/main/path/icons/coin%201.png"));
        }

        /// <summary>The prefix the sdk-gateway lets through without jwt-auth.</summary>
        [Test]
        public void The_anonymous_route_by_stable_id()
        {
            Assert.That(AssetRoutes.Download("p1", "main", true, ById),
                Is.EqualTo("/public/assets/v1/projects/p1/branches/main/assets/by-stable-id/64b7f0c2e1a2b3c4d5e6f7a8"));
        }

        [Test]
        public void The_anonymous_route_by_path()
        {
            Assert.That(AssetRoutes.Download("p1", "main", true, ByPath),
                Is.EqualTo("/public/assets/v1/projects/p1/branches/main/path/icons/coin%201.png"));
        }

        [Test]
        public void The_catalog_route()
        {
            Assert.That(AssetRoutes.Config("p1", "main"), Is.EqualTo("/assets/v1/projects/p1/branches/main/config"));
        }
    }
}
