using System.Collections.Generic;
using MirraCloud.Core.AssetsStorage;
using MirraCloud.Core.Auth;
using MirraCloud.Core.Chats;
using MirraCloud.Core.CloudSave;
using MirraCloud.Core.DailyRewards;
using MirraCloud.Core.CloudCode;
using MirraCloud.Core.Economy;
using MirraCloud.Core.Entities;
using MirraCloud.Core.Friends;
using MirraCloud.Core.Groups;
using MirraCloud.Core.Leaderboard;
using MirraCloud.Core.Localization;
using MirraCloud.Core.Logger;
using MirraCloud.Core.ProfanityFilter;
using MirraCloud.Core.PromoCodes;
using MirraCloud.Core.Purchases;
using MirraCloud.Core.RemoteConfig;
using MirraCloud.Core.Storage;
using MirraCloud.Core.Storage.Blob;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.LifeCycle;
using Plugins.MirraCloud.Core.Services.Analytics;
using Plugins.MirraCloud.Core.Services.Deployment;
using Plugins.MirraCloud.Core.Services.PlayerAccount;
using Plugins.MirraCloud.Core.Services.Segments;
using MirraCloud.Core.WebView;
using Plugins.MirraCloud.Core.Services.Challenges;
using Plugins.MirraCloud.Core.Services.Tournaments;

namespace MirraCloud.Core
{
    public class MirraCloudSDK : IMirraCloudSdk
    {
        private AnalyticsTracker _analyticsTracker;
        private IBlobStorage _blobStorage;

        public AuthenticationService Authentication { get; private set; }
        public PlayerAccountService PlayerAccount { get; private set; }
        public FriendsService Friends { get; private set; }
        public ChatsService Chats { get; private set; }
        public EconomyService Economy { get; private set; }
        public EntitiesService Entities { get; private set; }
        public CloudSaveService CloudSave { get; private set; }
        public LeaderboardService Leaderboard { get; private set; }
        public LocalizationService Localization { get; private set; }
        public TournamentsService Tournaments { get; private set; }
        public RemoteConfigService RemoteConfig { get; private set; }
        public AssetsStorageService AssetsStorage { get; private set; }
        public CloudCodeService CloudCode { get; private set; }
        public SegmentService Segments { get; private set; }
        public AnalyticsService Analytics { get; private set; }
        public DeploymentService Deployment { get; private set; }
        public GroupsService Groups { get; private set; }
        public DailyRewardsService DailyRewards { get; private set; }
        public ChallengesService Challenges { get; private set; }
        public PurchasesService Purchases { get; private set; }
        public ProfanityFilterService ProfanityFilter { get; private set; }
        public PromoCodesService PromoCodes { get; private set; }
        public WebViewService WebView { get; private set; }

        public bool IsInitialized { get; private set; }

        private readonly List<ICloudSdkDisposable> _disposables = new List<ICloudSdkDisposable>();
        private readonly List<ICloudSdkInitializable> _initializables = new List<ICloudSdkInitializable>();

        private T RegisterService<T>(T service) where T : ICloudSdkService
        {
            _initializables.Add(service);
            _disposables.Add(service);

            return service;
        }

        public static MirraCloudSDK Create()
        {
            MirraCloudSDK sdk = new MirraCloudSDK();

            return sdk;
        }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            CoroutineRunner coroutineRunner = CoroutineRunner.CreateInstance();

            Configuration configuration = Configuration.Load();
            ILogger logger = new Core.Logger.Logger();
            IJsonService jsonService = new JsonService();

            RestApiClientOptions restApiClientOptions = new RestApiClientOptions()
            {
                BaseUrl = configuration.Url,
            };

            RestApiClient restApiClient = new RestApiClient(restApiClientOptions, coroutineRunner, jsonService, logger);

            IStorage storage = new PrefsStorage();

#if UNITY_WEBGL && !UNITY_EDITOR
            _blobStorage = new IndexedDbBlobStorage();
            RegisterService(new BlobStorageWebSmokeTest(_blobStorage));
#else
            _blobStorage = new SqliteBlobStorage();
#endif

            WebView = RegisterService(new WebViewService());
            Authentication = RegisterService(new AuthenticationService(configuration, logger, storage, restApiClient, WebView));
            PlayerAccount = RegisterService(new PlayerAccountService(Authentication, restApiClient, configuration, logger));
            Friends = RegisterService(new FriendsService(configuration, logger, restApiClient));
            Groups = RegisterService(new GroupsService(configuration, logger, restApiClient));
            Chats = RegisterService(new ChatsService(configuration, logger, restApiClient, jsonService, coroutineRunner, Authentication));
            Economy = RegisterService(new EconomyService(configuration, logger, restApiClient));
            Entities = RegisterService(new EntitiesService(configuration, logger, restApiClient));
            CloudSave = RegisterService(new CloudSaveService(configuration, logger, jsonService, restApiClient));
            Leaderboard = RegisterService(new LeaderboardService(configuration, PlayerAccount, logger, jsonService, restApiClient));
            Localization = RegisterService(new LocalizationService(configuration, logger, restApiClient));
            Tournaments = RegisterService(new TournamentsService(configuration, restApiClient, PlayerAccount));
            RemoteConfig = RegisterService(new RemoteConfigService(restApiClient, configuration, logger));
            AssetsStorage = RegisterService(new AssetsStorageService(configuration, restApiClient, logger, _blobStorage));
            Analytics = RegisterService(new AnalyticsService(configuration, logger, restApiClient));
            Deployment = RegisterService(new DeploymentService(configuration, logger, restApiClient));
            CloudCode = RegisterService(new CloudCodeService(configuration, logger, restApiClient));
            Segments = RegisterService(new SegmentService(configuration, logger, restApiClient));
            DailyRewards = RegisterService(new DailyRewardsService(configuration, restApiClient));
            Challenges = RegisterService(new ChallengesService(configuration, PlayerAccount, restApiClient));
            Purchases = RegisterService(new PurchasesService(configuration, logger, restApiClient, WebView, coroutineRunner));
            ProfanityFilter = RegisterService(new ProfanityFilterService(restApiClient, configuration, logger));
            PromoCodes = RegisterService(new PromoCodesService(configuration, restApiClient));

            
            _analyticsTracker = AnalyticsTracker.CreateInstance();
            Analytics.SetTracker(_analyticsTracker);
            Authentication.OnLogin += _ =>
            {
                Analytics.SendSessionStartedAsync();
                _analyticsTracker.StartTracking(Analytics);
            };

            foreach (var cloudSdkInitializable in _initializables)
            {
                cloudSdkInitializable.CloudSdkInitialize();
            }

            IsInitialized = true;
        }

        public void Dispose()
        {
            foreach (var cloudSdkDisposable in _disposables)
            {
                cloudSdkDisposable.CloudSdkDispose();
            }

            if (_blobStorage is System.IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
