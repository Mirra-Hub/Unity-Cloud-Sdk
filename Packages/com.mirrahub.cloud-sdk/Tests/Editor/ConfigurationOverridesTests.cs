using NUnit.Framework;
using UnityEngine;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// <see cref="MirraCloudOptions"/> over <c>Configuration.asset</c>: a set field wins and is trimmed, an unset one
    /// — null, empty or blank — leaves the asset's value, and the asset itself never changes.
    /// </summary>
    [TestFixture]
    public class ConfigurationOverridesTests
    {
        private Configuration _configuration;

        [SetUp]
        public void SetUp()
        {
            _configuration = ScriptableObject.CreateInstance<Configuration>();
            _configuration.ProjectId = "asset_project";
            _configuration.Branch = "main";
            _configuration.Token = "asset_token";
            _configuration.PlatformKey = "asset_platform";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_configuration);
        }

        [Test]
        public void No_options_keep_the_asset()
        {
            _configuration.ApplyOverrides(null);
            _configuration.ApplyOverrides(new MirraCloudOptions());

            AssertConfiguration("asset_project", "main", "asset_token", "asset_platform");
        }

        [Test]
        public void Each_field_is_set_on_its_own()
        {
            _configuration.ApplyOverrides(new MirraCloudOptions { ProjectId = "code_project" });
            AssertConfiguration("code_project", "main", "asset_token", "asset_platform");

            _configuration.ApplyOverrides(new MirraCloudOptions { Branch = "qa" });
            AssertConfiguration("code_project", "qa", "asset_token", "asset_platform");

            _configuration.ApplyOverrides(new MirraCloudOptions { Token = "code_token" });
            AssertConfiguration("code_project", "qa", "code_token", "asset_platform");

            _configuration.ApplyOverrides(new MirraCloudOptions { PlatformKey = "yandex_games" });
            AssertConfiguration("code_project", "qa", "code_token", "yandex_games");
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t\n")]
        public void A_blank_field_keeps_the_asset(string blank)
        {
            _configuration.ApplyOverrides(new MirraCloudOptions { ProjectId = blank, Branch = blank, Token = blank, PlatformKey = blank });

            AssertConfiguration("asset_project", "main", "asset_token", "asset_platform");
        }

        [Test]
        public void A_set_field_is_trimmed()
        {
            _configuration.ApplyOverrides(new MirraCloudOptions { ProjectId = " p ", Branch = " qa ", Token = " t ", PlatformKey = " yandex_games " });

            AssertConfiguration("p", "qa", "t", "yandex_games");
        }

        [Test]
        public void Load_returns_a_copy_and_leaves_the_asset_alone()
        {
            Configuration asset = Resources.Load<Configuration>("Configuration");
            string assetProject = asset != null ? asset.ProjectId : null;
            string assetBranch = asset != null ? asset.Branch : null;

            // ProjectId and Branch from code: without an asset in this project, that is enough to run on.
            Configuration loaded = Configuration.Load(new MirraCloudOptions { ProjectId = "code_project", Branch = "qa" });

            try
            {
                Assert.That(loaded, Is.Not.SameAs(asset));
                Assert.That(loaded.ProjectId, Is.EqualTo("code_project"));
                Assert.That(loaded.Branch, Is.EqualTo("qa"));
                Assert.That(loaded.Url, Is.Not.Empty, "the environment is resolved on the copy");

                if (asset != null)
                {
                    Assert.That(asset.ProjectId, Is.EqualTo(assetProject));
                    Assert.That(asset.Branch, Is.EqualTo(assetBranch));
                }
            }
            finally
            {
                Object.DestroyImmediate(loaded);
            }
        }

        private void AssertConfiguration(string projectId, string branch, string token, string platformKey)
        {
            Assert.That(_configuration.ProjectId, Is.EqualTo(projectId));
            Assert.That(_configuration.Branch, Is.EqualTo(branch));
            Assert.That(_configuration.Token, Is.EqualTo(token));
            Assert.That(_configuration.PlatformKey, Is.EqualTo(platformKey));
        }
    }
}
