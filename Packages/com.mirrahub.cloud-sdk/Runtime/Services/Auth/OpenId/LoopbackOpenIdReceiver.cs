#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MirraCloud.Core.WebView.Utils;
using Plugins.MirraCloud.Core.General.AsyncOperations;

namespace MirraCloud.Core.Auth.OpenId
{
    internal sealed class LoopbackOpenIdReceiver : IOpenIdCallbackReceiver
    {
        private const string KeyParamName = "mirra_openid_key";
        private const string ErrorParamName = "mirra_openid_error";

        private readonly SynchronizationContext _syncContext;
        private readonly HttpListener _listener;
        private readonly string _successUrl;
        private readonly AsyncOperation<OpenIdCallbackResult> _resultOp;
        private bool _disposed;

        public string SuccessUrl => _successUrl;

        private LoopbackOpenIdReceiver(HttpListener listener, string successUrl, SynchronizationContext syncContext)
        {
            _listener = listener;
            _successUrl = successUrl;
            _syncContext = syncContext;
            _resultOp = new AsyncOperation<OpenIdCallbackResult>();
        }

        public static bool TryCreate(int port, out IOpenIdCallbackReceiver receiver, out string error)
        {
            receiver = null;
            error = null;

            try
            {
                var actualPort = port > 0 ? port : GetFreeTcpPort();
                var prefix = $"http://127.0.0.1:{actualPort}/mirra-openid/";

                var listener = new HttpListener();
                listener.Prefixes.Add(prefix);
                listener.Start();

                var syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
                var impl = new LoopbackOpenIdReceiver(listener, prefix, syncContext);
                impl.BeginWaitForCallback();

                receiver = impl;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to start loopback receiver: {ex.Message}";
                return false;
            }
        }

        public bool LaunchAuthUrl(string authUrl)
        {
            if (string.IsNullOrEmpty(authUrl))
            {
                return false;
            }

            UnityEngine.Application.OpenURL(authUrl);
            return true;
        }

        public AsyncOperation<OpenIdCallbackResult> WaitForCallbackAsync()
        {
            return _resultOp;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                if (_listener.IsListening)
                {
                    _listener.Stop();
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                _listener.Close();
            }
            catch
            {
                // ignored
            }
        }

        private void BeginWaitForCallback()
        {
            Task.Run(async () =>
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    var callbackUrl = context.Request.Url?.ToString();

                    var query = CallbackUrlParser.ParseQuery(callbackUrl);
                    var result = ResolveResult(query);

                    await WriteHtmlAsync(context.Response, result);

                    _syncContext.Post(_ => _resultOp.Complete(result), null);
                }
                catch (Exception ex)
                {
                    _syncContext.Post(_ => _resultOp.Complete(OpenIdCallbackResult.Failure(ex.Message)), null);
                }
                finally
                {
                    Dispose();
                }
            });
        }

        private static OpenIdCallbackResult ResolveResult(System.Collections.Generic.IDictionary<string, string> query)
        {
            if (query.TryGetValue(ErrorParamName, out var error) && !string.IsNullOrWhiteSpace(error))
            {
                return OpenIdCallbackResult.Failure(error);
            }
            if (query.TryGetValue(KeyParamName, out var key) && !string.IsNullOrWhiteSpace(key))
            {
                return OpenIdCallbackResult.Success(key);
            }
            return OpenIdCallbackResult.Empty;
        }

        private static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task WriteHtmlAsync(HttpListenerResponse response, OpenIdCallbackResult result)
        {
            const string okHtml = "<html><body><h3>MirraCloud login complete</h3><p>You can close this window.</p></body></html>";
            const string failHtml = "<html><body><h3>MirraCloud login failed</h3><p>{0}</p></body></html>";

            var html = result.IsSuccess
                ? okHtml
                : string.Format(failHtml, System.Net.WebUtility.HtmlEncode(result.ErrorMessage ?? "Invalid callback."));

            var bytes = Encoding.UTF8.GetBytes(html);

            response.StatusCode = 200;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;

            try
            {
                await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            }
            catch
            {
                // ignored
            }
            finally
            {
                try { response.OutputStream.Close(); } catch { /* ignored */ }
                try { response.Close(); } catch { /* ignored */ }
            }
        }
    }
}
#endif
