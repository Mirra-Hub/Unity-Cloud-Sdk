using MirraCloud.Core.AssetsStorage;
using NUnit.Framework;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// Which route a load takes. <c>Player</c> never goes anonymous, even without a session: a game that asked
    /// for the player's route learns the player is missing (401) instead of quietly getting only what is public.
    /// </summary>
    [TestFixture]
    public class AssetAccessResolverTests
    {
        [TestCase(AssetAccess.Player, true, false)]
        [TestCase(AssetAccess.Player, false, false)]
        [TestCase(AssetAccess.Public, true, true)]
        [TestCase(AssetAccess.Public, false, true)]
        [TestCase(AssetAccess.Auto, true, false)]
        [TestCase(AssetAccess.Auto, false, true)]
        public void Picks_the_route(AssetAccess access, bool hasSession, bool anonymous)
        {
            Assert.That(AssetAccessResolver.IsAnonymous(access, hasSession), Is.EqualTo(anonymous));
        }

        /// <summary>Every <c>Load*</c> defaults its <c>access</c> to <c>Player</c>; an unset field of the enum
        /// type has to mean the same.</summary>
        [Test]
        public void The_unset_value_is_the_players_route()
        {
            Assert.That(default(AssetAccess), Is.EqualTo(AssetAccess.Player));
        }
    }
}
