using MirraCloud.Core.AssetsStorage;
using NUnit.Framework;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// The editor's cache window reads the keys the service writes back into project, branch, asset and version.
    /// </summary>
    [TestFixture]
    public class AssetCacheKeysTests
    {
        [Test]
        public void A_written_key_reads_back()
        {
            string key = AssetCacheKeys.Asset("6a8c529e/main/7f3c", 3);

            Assert.That(AssetCacheKeys.TryParse(key, out string project, out string branch, out string stableId, out int version), Is.True);
            Assert.That(project, Is.EqualTo("6a8c529e"));
            Assert.That(branch, Is.EqualTo("main"));
            Assert.That(stableId, Is.EqualTo("7f3c"));
            Assert.That(version, Is.EqualTo(3));
        }

        [Test]
        public void A_branch_name_with_slashes_stays_whole()
        {
            Assert.That(AssetCacheKeys.TryParse("p/feature/new-shop/a1/v12", out string project, out string branch, out string stableId, out int version), Is.True);
            Assert.That(project, Is.EqualTo("p"));
            Assert.That(branch, Is.EqualTo("feature/new-shop"));
            Assert.That(stableId, Is.EqualTo("a1"));
            Assert.That(version, Is.EqualTo(12));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("tex_a/v1")]
        [TestCase("p/b/a")]
        [TestCase("p/b/a/1")]
        [TestCase("p/b/a/v")]
        [TestCase("p/b/a/vx")]
        public void Any_other_shape_is_refused(string key)
        {
            Assert.That(AssetCacheKeys.TryParse(key, out _, out _, out _, out _), Is.False);
        }
    }
}
