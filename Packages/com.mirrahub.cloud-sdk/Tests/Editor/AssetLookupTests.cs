using System.Collections.Generic;
using System.Linq;
using MirraCloud.Core.AssetsStorage;
using NUnit.Framework;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// Lookups over the loaded catalog. Paths are compared exactly, as the server compares them — a lookup
    /// that forgave the case here would promise an asset the server then answers 404 for.
    /// </summary>
    [TestFixture]
    public class AssetLookupTests
    {
        private static readonly IReadOnlyList<Asset> Catalog = new List<Asset>
        {
            At("/readme.txt"),
            At("/icons/coin.png"),
            At("/icons/gem.png"),
            At("/icons/ui/close.png"),
            // Starts like "/icons" but is another folder.
            At("/iconsets/pack.png"),
        };

        [Test]
        public void Finds_an_asset_by_its_stored_path()
        {
            Assert.That(AssetLookup.FindByPath(Catalog, "/icons/coin.png").StableId, Is.EqualTo("/icons/coin.png"));
        }

        [TestCase("/Icons/Coin.png")]
        [TestCase("/icons/none.png")]
        [TestCase("/icons")]
        public void Finds_nothing_for_another_case_a_missing_file_or_a_folder(string storedPath)
        {
            Assert.That(AssetLookup.FindByPath(Catalog, storedPath), Is.Null);
        }

        [Test]
        public void A_folder_lists_what_is_directly_in_it()
        {
            Assert.That(Paths(AssetLookup.InFolder(Catalog, "/icons", false)),
                Is.EqualTo(new[] { "/icons/coin.png", "/icons/gem.png" }));
        }

        [Test]
        public void A_recursive_folder_lists_its_subfolders_too_but_not_a_sibling_with_the_same_prefix()
        {
            Assert.That(Paths(AssetLookup.InFolder(Catalog, "/icons", true)),
                Is.EqualTo(new[] { "/icons/coin.png", "/icons/gem.png", "/icons/ui/close.png" }));
        }

        [Test]
        public void The_root_lists_the_top_level_or_everything()
        {
            Assert.That(Paths(AssetLookup.InFolder(Catalog, string.Empty, false)), Is.EqualTo(new[] { "/readme.txt" }));
            Assert.That(AssetLookup.InFolder(Catalog, string.Empty, true), Has.Count.EqualTo(Catalog.Count));
        }

        [Test]
        public void A_missing_folder_is_empty()
        {
            Assert.That(AssetLookup.InFolder(Catalog, "/missing", true), Is.Empty);
        }

        // The stable id doubles as the path so an assertion can name the asset it expected.
        private static Asset At(string path)
        {
            return new Asset(new AssetDto { stableId = path, path = path });
        }

        private static string[] Paths(IEnumerable<Asset> assets)
        {
            return assets.Select(asset => asset.Path).ToArray();
        }
    }
}
