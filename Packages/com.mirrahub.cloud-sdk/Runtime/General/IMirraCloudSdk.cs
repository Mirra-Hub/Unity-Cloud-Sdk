using MirraCloud.Core.AssetsStorage;
using MirraCloud.Core.Auth;
using MirraCloud.Core.Chats;
using MirraCloud.Core.CloudCode;
using MirraCloud.Core.CloudSave;
using MirraCloud.Core.DailyRewards;
using MirraCloud.Core.Economy;
using MirraCloud.Core.Entities;
using MirraCloud.Core.Friends;
using MirraCloud.Core.Groups;
using MirraCloud.Core.Leaderboard;
using MirraCloud.Core.Localization;
using MirraCloud.Core.ProfanityFilter;
using MirraCloud.Core.PromoCodes;
using MirraCloud.Core.Purchases;
using MirraCloud.Core.RemoteConfig;
using Plugins.MirraCloud.Core.Services.Analytics;
using Plugins.MirraCloud.Core.Services.Challenges;
using Plugins.MirraCloud.Core.Services.Deployment;
using Plugins.MirraCloud.Core.Services.PlayerAccount;
using Plugins.MirraCloud.Core.Services.Segments;
using MirraCloud.Core.WebView;
using Plugins.MirraCloud.Core.Services.Tournaments;

namespace MirraCloud.Core
{
    public interface IMirraCloudSdk
    {
        AuthenticationService Authentication { get; }
        PlayerAccountService PlayerAccount { get; }
        FriendsService Friends { get; }
        ChatsService Chats { get; }
        GroupsService Groups { get; }
        EconomyService Economy { get; }
        EntitiesService Entities { get; }
        CloudSaveService CloudSave { get; }
        LeaderboardService Leaderboard { get; }
        LocalizationService Localization { get; }
        TournamentsService Tournaments { get; }
        RemoteConfigService RemoteConfig { get; }
        AssetsStorageService AssetsStorage { get; }
        CloudCodeService CloudCode { get; }
        SegmentService Segments { get; }
        AnalyticsService Analytics { get; }
        DeploymentService Deployment { get; }
        DailyRewardsService DailyRewards { get; }
        ChallengesService Challenges { get; }
        PurchasesService Purchases { get; }
        ProfanityFilterService ProfanityFilter { get; }
        PromoCodesService PromoCodes { get; }
        WebViewService WebView { get; }
        bool IsInitialized { get; }

        void Initialize();
        void Dispose();
    }
}
