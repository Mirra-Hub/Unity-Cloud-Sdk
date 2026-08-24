using MirraCloud.Core;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core
{
    public interface ISessionRefresher
    {
        bool CanRefresh { get; }
        AsyncOperation<RestApiResult> RefreshSessionAsync();
    }
}

