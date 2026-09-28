using System;
using MirraCloud.Core.Chats.Dto;
using MirraCloud.Core.Groups.Dto.Response;
using MirraCloud.Json;
using NUnit.Framework;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// "My channels", read the way the runtime reads it: camelCase members, the channel nested in each entry, the
    /// nulls the server writes out and the page fields it adds on top. The dates come without a zone mark — the
    /// chat tables hand them over unmarked, although they are UTC — and a marked one has to read the same.
    /// </summary>
    [TestFixture]
    public class ChatsDtoMappingTests
    {
        // As the backend answered on the local stack, plus a group channel with its owner.
        private const string Payload =
            "{\"items\":["
            + "{\"channel\":{\"channelId\":\"ch-1\",\"type\":\"room\",\"ownerRef\":null,\"name\":\"Raid\","
            + "\"topic\":null,\"templateKey\":\"guild-chat\",\"state\":\"active\",\"lastMessageNumber\":5,"
            + "\"lastMessageAt\":\"2026-09-28T10:05:00\",\"createdAt\":\"2026-09-28T10:00:00\","
            + "\"updatedAt\":\"2026-09-28T10:05:00\"},"
            + "\"joinedAt\":\"2026-09-28T10:01:00\",\"lastReadMessageNumber\":3,\"lastReadAt\":null,\"unreadCount\":2},"
            + "{\"channel\":{\"channelId\":\"ch-2\",\"type\":\"group\",\"ownerRef\":{\"type\":\"group\",\"id\":\"g-1\"},"
            + "\"name\":\"Guild\",\"topic\":\"Raids\",\"templateKey\":null,\"state\":\"archived\",\"lastMessageNumber\":9,"
            + "\"lastMessageAt\":null,\"createdAt\":\"2026-09-27T10:00:00Z\",\"updatedAt\":\"2026-09-27T10:00:00Z\"},"
            + "\"joinedAt\":\"2026-09-27T10:01:00Z\",\"lastReadMessageNumber\":9,"
            + "\"lastReadAt\":\"2026-09-27T11:00:00Z\",\"unreadCount\":0}],"
            + "\"totalCount\":2,\"page\":1,\"pageSize\":20,\"totalPages\":1,\"hasPreviousPage\":false,\"hasNextPage\":false}";

        [Test]
        public void Reads_the_page_and_every_entry()
        {
            var page = JsonMapper.FromJson<PaginatedResult<ChatPlayerChannelDto>>(Payload);

            Assert.That(page.TotalCount, Is.EqualTo(2));
            Assert.That(page.Page, Is.EqualTo(1));
            Assert.That(page.PageSize, Is.EqualTo(20));
            Assert.That(page.Items, Has.Length.EqualTo(2));

            ChatPlayerChannelDto room = page.Items[0];
            Assert.That(room.Channel.ChannelId, Is.EqualTo("ch-1"));
            Assert.That(room.Channel.Type, Is.EqualTo("room"));
            Assert.That(room.Channel.Name, Is.EqualTo("Raid"));
            Assert.That(room.Channel.OwnerRef, Is.Null);
            Assert.That(room.Channel.State, Is.EqualTo("active"));
            Assert.That(room.Channel.LastMessageNumber, Is.EqualTo(5));
            // Unmarked: the clock time the server stored, read as is.
            Assert.That(room.JoinedAt, Is.EqualTo(new DateTime(2026, 9, 28, 10, 1, 0)));
            Assert.That(room.Channel.LastMessageAt, Is.EqualTo(new DateTime(2026, 9, 28, 10, 5, 0)));
            Assert.That(room.LastReadMessageNumber, Is.EqualTo(3));
            Assert.That(room.LastReadAt, Is.Null);
            Assert.That(room.UnreadCount, Is.EqualTo(2));

            ChatPlayerChannelDto guild = page.Items[1];
            Assert.That(guild.Channel.OwnerRef.Type, Is.EqualTo("group"));
            Assert.That(guild.Channel.OwnerRef.Id, Is.EqualTo("g-1"));
            Assert.That(guild.Channel.State, Is.EqualTo("archived"));
            Assert.That(guild.Channel.LastMessageAt, Is.Null);
            // Marked UTC — the form the server would send once the chat tables mark their dates.
            Assert.That(guild.LastReadAt?.ToUniversalTime(), Is.EqualTo(new DateTime(2026, 9, 27, 11, 0, 0, DateTimeKind.Utc)));
            Assert.That(guild.UnreadCount, Is.EqualTo(0));
        }

        [Test]
        public void An_empty_list_reads_as_an_empty_page()
        {
            var page = JsonMapper.FromJson<PaginatedResult<ChatPlayerChannelDto>>(
                "{\"items\":[],\"totalCount\":0,\"page\":1,\"pageSize\":20,\"totalPages\":0,"
                + "\"hasPreviousPage\":false,\"hasNextPage\":false}");

            Assert.That(page.Items, Is.Empty);
            Assert.That(page.TotalCount, Is.EqualTo(0));
        }
    }
}
