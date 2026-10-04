using MirraCloud.Core.Auth;
using MirraCloud.Core.Storage;
using NUnit.Framework;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// Where a player's sign-in is kept on the device. These names are data on every player's device: a renamed key
    /// or container signs everyone out and turns every guest into a new player — and like the route typo that once
    /// broke every asset download, it would still compile.
    /// </summary>
    [TestFixture]
    public class LocalStorageNamesTests
    {
        [Test]
        public void The_sign_in_keys_are_pinned()
        {
            Assert.That(AuthStorageKeys.GuestId, Is.EqualTo("auth/guest_id"));
            Assert.That(AuthStorageKeys.RefreshToken, Is.EqualTo("auth/refresh_token"));
        }

        [Test]
        public void The_containers_are_pinned_and_the_editor_keeps_its_own()
        {
            Assert.That(LocalDataContainers.PrefsInBuilds, Is.EqualTo("mirracloud_prefs"));
            Assert.That(LocalDataContainers.PrefsInEditor, Is.EqualTo("mirracloud_prefs_editor"));

            // These tests compile for the editor, where the SDK has to use the editor's container.
            Assert.That(LocalDataContainers.Prefs, Is.EqualTo(LocalDataContainers.PrefsInEditor));
        }

        [Test]
        public void The_asset_cache_container_is_pinned()
        {
            // Renamed, it would leave every cache already downloaded orphaned on disk.
            Assert.That(LocalDataContainers.AssetCache, Is.EqualTo("asset_cache"));
        }
    }
}
