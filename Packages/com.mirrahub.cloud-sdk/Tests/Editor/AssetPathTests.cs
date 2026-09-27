using MirraCloud.Core.AssetsStorage;
using NUnit.Framework;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// How the path a game passes becomes the stored form — compared with the catalog here and by the server,
    /// exactly — and the form that goes into the URL.
    ///
    /// <para>
    /// Every spelling of one path has to arrive identical, or the server answers 404 for an asset that exists.
    /// And a name holding a space, '#', '?', '%' or '+' has to reach the server as that name: unescaped, '#'
    /// would cut the URL short and '?' would start a query.
    /// </para>
    /// </summary>
    [TestFixture]
    public class AssetPathTests
    {
        [TestCase("icons/coin.png")]
        [TestCase("/icons/coin.png")]
        [TestCase("icons//coin.png")]
        [TestCase("icons/coin.png/")]
        [TestCase("icons\\coin.png")]
        public void Every_spelling_of_a_path_folds_into_the_stored_form(string input)
        {
            Assert.That(AssetPath.TryParse(input, out string stored, out string route), Is.True);
            Assert.That(stored, Is.EqualTo("/icons/coin.png"));
            Assert.That(route, Is.EqualTo("icons/coin.png"));
        }

        [TestCase("Иконки/монета 1.png", "/Иконки/монета 1.png",
            "%D0%98%D0%BA%D0%BE%D0%BD%D0%BA%D0%B8/%D0%BC%D0%BE%D0%BD%D0%B5%D1%82%D0%B0%201.png")]
        [TestCase("#1?.txt", "/#1?.txt", "%231%3F.txt")]
        [TestCase("50%.txt", "/50%.txt", "50%25.txt")]
        [TestCase("a+b;c.txt", "/a+b;c.txt", "a%2Bb%3Bc.txt")]
        public void Each_segment_is_escaped_on_its_own_and_the_slashes_between_them_are_kept(
            string input, string expectedStored, string expectedRoute)
        {
            Assert.That(AssetPath.TryParse(input, out string stored, out string route), Is.True);
            Assert.That(stored, Is.EqualTo(expectedStored));
            Assert.That(route, Is.EqualTo(expectedRoute));
        }

        [Test]
        public void The_case_is_kept_because_the_server_compares_it()
        {
            Assert.That(AssetPath.TryParse("Icons/Coin.png", out string stored, out _), Is.True);
            Assert.That(stored, Is.EqualTo("/Icons/Coin.png"));
        }

        /// <summary>The server refuses these too; refusing them here saves a request the player's route bills.</summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("/")]
        [TestCase("//")]
        [TestCase("../secret.txt")]
        [TestCase("icons/../../etc/passwd")]
        public void An_empty_path_or_traversal_is_refused(string input)
        {
            Assert.That(AssetPath.TryParse(input, out string stored, out string route), Is.False);
            Assert.That(stored, Is.Null);
            Assert.That(route, Is.Null);
        }

        [TestCase(null, true)]
        [TestCase("", true)]
        [TestCase("/", true)]
        [TestCase("\\", true)]
        [TestCase("icons", false)]
        [TestCase("/icons/", false)]
        public void The_root_is_an_empty_path(string input, bool isRoot)
        {
            Assert.That(AssetPath.IsRoot(input), Is.EqualTo(isRoot));
        }
    }
}
