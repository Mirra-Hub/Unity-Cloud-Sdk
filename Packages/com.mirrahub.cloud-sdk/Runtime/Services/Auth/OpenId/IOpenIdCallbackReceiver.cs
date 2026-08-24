using System;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Auth.OpenId
{
    internal interface IOpenIdCallbackReceiver : IDisposable
    {
        string SuccessUrl { get; }
        bool LaunchAuthUrl(string authUrl);
        AsyncOperation<OpenIdCallbackResult> WaitForCallbackAsync();
    }
}
