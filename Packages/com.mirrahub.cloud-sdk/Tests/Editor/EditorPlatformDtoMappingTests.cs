using MirraCloud.Editor.Dto;
using MirraCloud.Json;
using NUnit.Framework;

namespace MirraCloud.Editor.Tests
{
    /// <summary>
    /// The console's platforms list, read the way the Manager window reads it.
    ///
    /// <para>
    /// The window keeps only what it draws — key, name, whether the platform is on, its types — so everything else in
    /// the answer (sign-in methods, integrations, paging) has to be skipped rather than fail the list. The types come
    /// as strings: one this version does not know must reach the label as it came.
    /// </para>
    /// </summary>
    [TestFixture]
    public class EditorPlatformDtoMappingTests
    {
        /// <summary>A real answer of <c>GET …/platforms?page=1&amp;pageSize=500</c>, trimmed to two platforms.</summary>
        private const string Payload =
            "{\"items\":["
            + "{\"key\":\"qa_analytics\",\"projectId\":\"6a8c529ee26b42ae1343523d\",\"name\":\"QA Analytics / PC\","
            + "\"platformTypes\":[\"Pc\"],\"isEnabled\":true,"
            + "\"authProviders\":[{\"kind\":\"guest\",\"integrationKey\":null,\"isEnabled\":true,\"password\":null}],"
            + "\"paymentProviders\":[],\"integrations\":[],"
            + "\"createdAt\":\"2026-08-28T01:32:59.186Z\",\"updatedAt\":\"2026-08-28T01:32:59.186Z\"},"
            + "{\"key\":\"web-mobile\",\"projectId\":\"6a8c529ee26b42ae1343523d\",\"name\":\"Web and phones\","
            + "\"platformTypes\":[\"Web\",\"Mobile\"],\"isEnabled\":false,"
            + "\"authProviders\":[],\"paymentProviders\":[{\"integrationKey\":\"stripe\",\"isEnabled\":true}],"
            + "\"integrations\":[],\"createdAt\":\"2026-09-12T17:32:48.804Z\",\"updatedAt\":\"2026-09-12T17:32:48.804Z\"}],"
            + "\"totalCount\":2,\"page\":1,\"pageSize\":500,\"totalPages\":1,"
            + "\"hasPreviousPage\":false,\"hasNextPage\":false}";

        [Test]
        public void Reads_the_platforms_with_their_types()
        {
            var page = JsonMapper.FromJson<EditorPlatformsPageDto>(Payload);

            Assert.That(page.totalCount, Is.EqualTo(2));
            Assert.That(page.items, Has.Count.EqualTo(2));

            Assert.That(page.items[0].key, Is.EqualTo("qa_analytics"));
            Assert.That(page.items[0].name, Is.EqualTo("QA Analytics / PC"));
            Assert.That(page.items[0].isEnabled, Is.True);
            Assert.That(page.items[0].platformTypes, Is.EqualTo(new[] { "Pc" }));

            Assert.That(page.items[1].key, Is.EqualTo("web-mobile"));
            Assert.That(page.items[1].isEnabled, Is.False);
            Assert.That(page.items[1].platformTypes, Is.EqualTo(new[] { "Web", "Mobile" }));
        }

        [Test]
        public void A_type_this_version_does_not_know_is_kept_as_it_came()
        {
            var page = JsonMapper.FromJson<EditorPlatformsPageDto>(
                "{\"items\":[{\"key\":\"quest\",\"name\":\"Quest\","
                + "\"platformTypes\":[\"Vr\",\"Mobile\"],\"isEnabled\":true}]}");

            Assert.That(page.items[0].platformTypes, Is.EqualTo(new[] { "Vr", "Mobile" }));
        }

        [Test]
        public void A_platform_without_types_reads_without_a_list()
        {
            var page = JsonMapper.FromJson<EditorPlatformsPageDto>(
                "{\"items\":[{\"key\":\"old\",\"name\":\"Old\",\"isEnabled\":true}]}");

            Assert.That(page.items[0].platformTypes, Is.Null);
            Assert.That(BuildTargetPlatformType.IsMismatch(page.items[0], BuildTargetPlatformType.Mobile), Is.False);
        }
    }
}
