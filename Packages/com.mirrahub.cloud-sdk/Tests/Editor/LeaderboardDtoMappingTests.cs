using System;
using System.Collections.Generic;
using MirraCloud.Core.Economy;
using MirraCloud.Core.Economy.Dto;
using MirraCloud.Core.Enums;
using MirraCloud.Core.Leaderboard.Dto;
using MirraCloud.Core.Leaderboard.Entities;
using MirraCloud.Core.Leaderboard.Enums;
using MirraCloud.Json;
using NUnit.Framework;

namespace MirraCloud.Core.Tests
{
    /// <summary>
    /// Leaderboard and reward payloads, read the way the runtime reads them from the game backend: camelCase
    /// members, enums as names, dates in UTC with a zone mark.
    ///
    /// <para>
    /// The DTOs had drifted from the server: entries were keyed by a <c>leaderboardId</c> nobody sent (the server
    /// sends <c>leaderboardKey</c>), rewards had a <c>rewardType</c> the server never wrote next to the
    /// <c>economyResourceKind</c> it does, and the icon and country of an entry were not read at all.
    /// </para>
    /// </summary>
    [TestFixture]
    public class LeaderboardDtoMappingTests
    {
        private const string Config =
            "[{\"id\":\"6aba1692426c05314f0eb25a\",\"key\":\"qa_lb_rewards\",\"name\":\"QA rewards\","
            + "\"rewardsForPlaces\":[{\"valueMin\":1,\"valueMax\":3,\"rewards\":["
            + "{\"rewardId\":\"6a9f00000000000000000001\",\"rewardKey\":\"gold\",\"economyResourceKind\":\"Currency\",\"count\":100},"
            + "{\"rewardId\":\"6a9f00000000000000000002\",\"rewardKey\":null,\"economyResourceKind\":\"Item\",\"count\":1}]}],"
            + "\"orderType\":\"Highest\",\"type\":\"Score\",\"updateStrategy\":\"Total\",\"rewardDistributionType\":\"ByPlace\","
            + "\"isReset\":true,\"resetIntervalType\":\"Weekly\",\"resetIntervalValue\":1,"
            + "\"resetTimeHour\":4,\"resetTimeMinute\":30,\"cohortsEnabled\":true,\"cohortSize\":10,"
            + "\"createdDate\":\"2026-09-28T07:12:00Z\",\"updatedDate\":\"2026-09-28T07:40:00Z\","
            + "\"nextResetDate\":\"2026-10-05T04:30:00Z\",\"lastResetDate\":\"2026-09-28T00:00:00Z\"}]";

        private const string Entries =
            "{\"leaderboardKey\":\"qa_lb_rewards\",\"entries\":["
            + "{\"playerId\":\"6a8df5bf0000000000000001\",\"playerName\":\"Ada\","
            + "\"iconKey\":{\"source\":\"Internal\",\"key\":\"avatars/7.png\"},\"countryCode\":\"Germany\","
            + "\"position\":1,\"value\":1250.5},"
            + "{\"playerId\":\"6a8df5bf0000000000000002\",\"playerName\":\"Bo\",\"iconKey\":null,"
            + "\"countryCode\":\"RussianFederation\",\"position\":2,\"value\":1250.5}]}";

        [Test]
        public void A_config_carries_the_reset_schedule_and_the_reward_keys()
        {
            var dto = JsonMapper.FromJson<LeaderboardConfigDto[]>(Config)[0];
            var config = new LeaderboardConfig(dto);

            Assert.That(config.Key, Is.EqualTo("qa_lb_rewards"));
            Assert.That(config.UpdateStrategy, Is.EqualTo(UpdateStrategy.Total));
            Assert.That(config.ResetIntervalType, Is.EqualTo(ResetIntervalType.Weekly));
            Assert.That(config.ResetTimeHour, Is.EqualTo(4));
            Assert.That(config.ResetTimeMinute, Is.EqualTo(30));
            Assert.That(config.CohortsEnabled, Is.True);
            Assert.That(config.CohortSize, Is.EqualTo(10));

            Assert.That(config.NextResetDate, Is.EqualTo(new DateTime(2026, 10, 5, 4, 30, 0, DateTimeKind.Utc)));
            Assert.That(config.NextResetDate.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(config.LastResetDate, Is.EqualTo(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc)));

            var range = config.RewardsForPlaces[0];
            Assert.That(range.valueMin, Is.EqualTo(1d));
            Assert.That(range.valueMax, Is.EqualTo(3d));
            Assert.That(range.rewards[0].rewardKey, Is.EqualTo("gold"));
            Assert.That(range.rewards[0].economyResourceKind, Is.EqualTo(EconomyResourceKind.Currency));
            Assert.That(range.rewards[0].count, Is.EqualTo(100));
            Assert.That(range.rewards[1].rewardKey, Is.Null); // the branch no longer has that resource
            Assert.That(range.rewards[1].economyResourceKind, Is.EqualTo(EconomyResourceKind.Item));
        }

        [Test]
        public void A_board_without_a_session_has_no_reset_dates()
        {
            var dto = JsonMapper.FromJson<LeaderboardConfigDto>(
                "{\"key\":\"fresh\",\"isReset\":true,\"resetIntervalType\":\"Daily\",\"nextResetDate\":null,\"lastResetDate\":null}");

            Assert.That(dto.nextResetDate, Is.Null);
            Assert.That(dto.lastResetDate, Is.Null);
        }

        [Test]
        public void Entries_carry_the_board_key_and_the_profile_s_icon_and_country()
        {
            var dto = JsonMapper.FromJson<LeaderboardEntriesDto>(Entries);

            Assert.That(dto.leaderboardKey, Is.EqualTo("qa_lb_rewards"));
            Assert.That(dto.entries, Has.Length.EqualTo(2));

            var first = dto.entries[0];
            Assert.That(first.playerName, Is.EqualTo("Ada"));
            Assert.That(first.iconKey.Key, Is.EqualTo("avatars/7.png"));
            Assert.That(first.countryCode, Is.EqualTo(CountryCode.Germany));
            Assert.That(first.position, Is.EqualTo(1));
            Assert.That(first.value, Is.EqualTo(1250.5d));

            // Equal scores still get consecutive places.
            var second = dto.entries[1];
            Assert.That(second.iconKey, Is.Null);
            Assert.That(second.countryCode, Is.EqualTo(CountryCode.RussianFederation));
            Assert.That(second.position, Is.EqualTo(2));
        }

        [Test]
        public void An_entry_from_a_country_this_build_does_not_know_is_still_read()
        {
            var previous = JsonMapper.Warning;
            JsonMapper.Warning = null;
            try
            {
                var dto = JsonMapper.FromJson<LeaderboardEntryDto>(
                    "{\"playerId\":\"p\",\"playerName\":\"Cy\",\"countryCode\":\"NewCountry\",\"position\":3,\"value\":10}");

                Assert.That(dto.playerName, Is.EqualTo("Cy"));
                Assert.That(dto.position, Is.EqualTo(3));
            }
            finally
            {
                JsonMapper.Warning = previous;
            }
        }

        [Test]
        public void The_submit_body_carries_the_score_only()
        {
            var json = JsonMapper.ToJson(new SubmitScoreDto { Value = 95.25 });

            // No name any more: the board shows the profile's. The writer keeps member names as declared, and the
            // server binds them regardless of case.
            Assert.That(json, Is.EqualTo("{\"Value\":95.25}"));
        }

        // As SdkEconomyRewardsController answers: the resource travels as its key under the old wire name rewardId.
        private const string PendingRewards =
            "[{\"sourceType\":1,\"sourceId\":\"6aba1692426c05314f0eb25a\",\"rewards\":["
            + "{\"rewardId\":\"gold\",\"economyResourceKind\":\"Currency\",\"count\":100},"
            + "{\"rewardId\":\"sword\",\"economyResourceKind\":\"Item\",\"count\":1}]},"
            + "{\"sourceType\":2,\"sourceId\":\"6aba19ed64304ceaf1f317e1\",\"rewards\":["
            + "{\"rewardId\":\"stamina\",\"economyResourceKind\":\"Energy\",\"count\":5}]}]";

        [Test]
        public void Pending_rewards_name_their_source_and_the_resource_keys()
        {
            var containers = JsonMapper.FromJson<List<RewardContainerDto>>(PendingRewards);

            Assert.That(containers, Has.Count.EqualTo(2));
            Assert.That(containers[0].SourceType, Is.EqualTo(RewardSourceType.Leaderboard));
            Assert.That(containers[0].SourceId, Is.EqualTo("6aba1692426c05314f0eb25a"));
            Assert.That(containers[0].Rewards[0].RewardKey, Is.EqualTo("gold"));
            Assert.That(containers[0].Rewards[0].EconomyResourceKind, Is.EqualTo(EconomyResourceKind.Currency));
            Assert.That(containers[0].Rewards[0].Count, Is.EqualTo(100));
            Assert.That(containers[0].Rewards[1].EconomyResourceKind, Is.EqualTo(EconomyResourceKind.Item));
            Assert.That(containers[1].SourceType, Is.EqualTo(RewardSourceType.Tournament));
            Assert.That(containers[1].Rewards[0].EconomyResourceKind, Is.EqualTo(EconomyResourceKind.Energy));
        }

        [Test]
        public void The_inventory_carries_the_pending_rewards()
        {
            var inventory = JsonMapper.FromJson<PlayerInventoryDto>(
                "{\"wallet\":[{\"currencyId\":\"gold\",\"balance\":250}],\"items\":[],\"energies\":[],\"rewards\":"
                + PendingRewards + "}");

            Assert.That(inventory.Wallet[0].CurrencyId, Is.EqualTo("gold"));
            Assert.That(inventory.Rewards, Has.Count.EqualTo(2));
            Assert.That(inventory.Rewards[1].Rewards[0].RewardKey, Is.EqualTo("stamina"));
        }
    }
}
