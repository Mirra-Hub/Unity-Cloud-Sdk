using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using MirraCloud.Core.Errors;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;
using UnityEngine.Networking;
using ILogger = MirraCloud.Core.Logger.ILogger;

namespace MirraCloud.Core
{
    public class RestApiClient
    {
        public string BaseUrl { get; private set; }

        private readonly ICoroutineRunner _coroutineRunner;
        public readonly IJsonService JsonService;
        private readonly ILogger _logger;

        private readonly List<Func<RestRequestConfig, RestRequestConfig>> _requestInterceptors = new();
        private ISessionRefresher _sessionRefresher;
        private static readonly long[] DefaultRedirectHttpStatusCodes = { 301, 302, 303, 307, 308 };
        private static readonly char[] HexDigits = "0123456789ABCDEF".ToCharArray();

        /// <summary>Who is responsible for the download handler attached to an attempt's request.</summary>
        private enum DownloadHandlerOwnership
        {
            /// <summary>Default buffer created by the client — disposed together with the request.</summary>
            Own,
            /// <summary>Produced by <see cref="RestRequestConfig.DownloadHandlerFactory"/> for this attempt —
            /// disposed only when the attempt is abandoned (retry/redirect); the final one is left to the caller's
            /// extractor (e.g. a texture handler whose content the caller keeps).</summary>
            Factory,
            /// <summary>Supplied by the caller via <see cref="RestRequestConfig.DownloadHandler"/> — never disposed here.</summary>
            Caller
        }

        public RestApiClient(RestApiClientOptions options, ICoroutineRunner coroutineRunner, IJsonService jsonService, ILogger logger)
        {
            BaseUrl = options.BaseUrl?.TrimEnd('/');
            _coroutineRunner = coroutineRunner;
            JsonService = jsonService;
            _logger = logger;
        }

        #region Interceptors
        public int UseRequestInterceptor(Func<RestRequestConfig, RestRequestConfig> interceptor)
        {
            _requestInterceptors.Add(interceptor);
            return _requestInterceptors.Count - 1;
        }

        public void EjectRequestInterceptor(int id)
        {
            if (id >= 0 && id < _requestInterceptors.Count)
            {
                _requestInterceptors[id] = null;
            }
        }

        public Dictionary<string, string> GetCurrentHeaders()
        {
            var config = new RestRequestConfig();
            foreach (var interceptor in _requestInterceptors)
            {
                if (interceptor == null) continue;
                config = interceptor.Invoke(config) ?? config;
            }
            return config.Headers;
        }
        #endregion

        public void SetSessionRefresher(ISessionRefresher sessionRefresher)
        {
            _sessionRefresher = sessionRefresher;
        }

        #region Public API
        public AsyncOperation<RestApiResult> GetAsync(string route, RestRequestConfig config = null)
        {
            var finalConfig = BuildConfig(route, UnityWebRequest.kHttpVerbGET, null, config);
            return SendRequest(finalConfig);
        }

        public AsyncOperation<RestApiResult<T>> GetAsync<T>(string route, RestRequestConfig config = null)
        {
            var finalConfig = BuildConfig(route, UnityWebRequest.kHttpVerbGET, null, config);
            return SendRequest<T>(finalConfig, null);
        }

        public AsyncOperation<RestApiResult<T>> GetAsync<T>(string route, RestRequestConfig config, Func<UnityWebRequest, T> extractData)
        {
            var finalConfig = BuildConfig(route, UnityWebRequest.kHttpVerbGET, null, config);
            return SendRequest(finalConfig, extractData);
        }

        public AsyncOperation<RestApiResult<byte[]>> GetBytesAsync(string route, RestRequestConfig config = null)
        {
            var finalConfig = BuildConfig(route, UnityWebRequest.kHttpVerbGET, null, config);
            return SendRequest<byte[]>(finalConfig, request => request.downloadHandler.data);
        }

        public AsyncOperation<RestApiResult> PostAsync(string route, object body = null, RestRequestConfig config = null)
        {
            var finalConfig = BuildConfig(route, UnityWebRequest.kHttpVerbPOST, body, config);
            return SendRequest(finalConfig);
        }

        public AsyncOperation<RestApiResult<T>> PostAsync<T>(string route, object body = null, RestRequestConfig config = null)
        {
            var finalConfig = BuildConfig(route, UnityWebRequest.kHttpVerbPOST, body, config);
            return SendRequest<T>(finalConfig);
        }

        public AsyncOperation<RestApiResult<T>> PostAsync<T>(string route, object body, RestRequestConfig config, Func<UnityWebRequest, T> extractData)
        {
            var finalConfig = BuildConfig(route, UnityWebRequest.kHttpVerbPOST, body, config);
            return SendRequest(finalConfig, extractData);
        }

        public AsyncOperation<RestApiResult> PutAsync(string route, object body = null, RestRequestConfig config = null)
        {
            var finalConfig = BuildConfig(route, UnityWebRequest.kHttpVerbPUT, body, config);
            return SendRequest(finalConfig);
        }

        public AsyncOperation<RestApiResult<T>> PutAsync<T>(string route, object body = null, RestRequestConfig config = null)
        {
            var finalConfig = BuildConfig(route, UnityWebRequest.kHttpVerbPUT, body, config);
            return SendRequest<T>(finalConfig);
        }

        public AsyncOperation<RestApiResult<T>> PatchAsync<T>(string route, object body = null, RestRequestConfig config = null)
        {
            var finalConfig = BuildConfig(route, "PATCH", body, config);
            return SendRequest<T>(finalConfig);
        }

        public AsyncOperation<RestApiResult> PatchAsync(string route, object body = null, RestRequestConfig config = null)
        {
            var finalConfig = BuildConfig(route, "PATCH", body, config);
            return SendRequest(finalConfig);
        }

        public AsyncOperation<RestApiResult> PatchMultipartAsync(string route, List<IMultipartFormSection> multipartFormSections, RestRequestConfig config = null)
        {
            var finalConfig = BuildConfig(route, "PATCH", null, config);
            finalConfig.MultipartFormSections = multipartFormSections;
            return SendRequest(finalConfig);
        }

        public AsyncOperation<RestApiResult> DeleteAsync(string route, RestRequestConfig config = null)
        {
            var finalConfig = BuildConfig(route, UnityWebRequest.kHttpVerbDELETE, null, config);
            return SendRequest(finalConfig);
        }

        public AsyncOperation<RestApiResult> DeleteAsync(string route, object body, RestRequestConfig config = null)
        {
            var finalConfig = BuildConfig(route, UnityWebRequest.kHttpVerbDELETE, body, config);
            return SendRequest(finalConfig);
        }

        public AsyncOperation<RestApiResult<T>> DeleteAsync<T>(string route, RestRequestConfig config = null)
        {
            var finalConfig = BuildConfig(route, UnityWebRequest.kHttpVerbDELETE, null, config);
            return SendRequest<T>(finalConfig);
        }
        #endregion

        private AsyncOperation<RestApiResult> SendRequest(RestRequestConfig config)
        {
            var op = new AsyncOperation<RestApiResult>();
            _coroutineRunner.StartCoroutine(SendRequestInternal(config, op));
            return op;
        }

        private AsyncOperation<RestApiResult<T>> SendRequest<T>(RestRequestConfig config, Func<UnityWebRequest, T> extractData)
        {
            var op = new AsyncOperation<RestApiResult<T>>();
            _coroutineRunner.StartCoroutine(SendRequestInternal(config, op, extractData));
            return op;
        }

        private AsyncOperation<RestApiResult<T>> SendRequest<T>(RestRequestConfig config)
        {
            return SendRequest<T>(config, null);
        }

        private IEnumerator SendRequestInternal(RestRequestConfig config, AsyncOperation<RestApiResult> operation)
        {
            // The whole retry/refresh/redirect flow mutates the single pooled config in-place
            // instead of allocating defensive copies per attempt.
            var cfg = config;
            var redirectDepth = 0;

            try
            {
                while (true)
                {
                    PrepareConfig(cfg);

                    foreach (var interceptor in _requestInterceptors)
                    {
                        if (interceptor == null) continue;
                        var updated = interceptor.Invoke(cfg);
                        if (updated != null)
                        {
                            cfg = updated;
                        }
                    }

                    var startTimestamp = Stopwatch.GetTimestamp();
                    var request = BuildUnityWebRequest(cfg, out var downloadOwnership, out var ownsUploadHandler);
                    yield return request.SendWebRequest();
                    var durationMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000 / Stopwatch.Frequency;

                    #if UNITY_EDITOR
                    var requestBodyForTrace = GetRequestBodyForTrace(cfg);
                    #endif

                    var responseBody = request.downloadHandler?.text;
                    var httpCode = request.responseCode;
                    var networkResult = request.result;
                    var networkError = request.error;
                    var isHttpSuccess = IsHttpSuccess(httpCode, cfg.AllowedHttpStatusCodes);

                    if (cfg.FollowRedirect && IsRedirectStatus(httpCode, cfg.RedirectHttpStatusCodes))
                    {
                        var redirectLocation = ExtractRedirectLocation(request);
                        DisposeRequest(request, downloadOwnership, ownsUploadHandler, finalAttempt: false);
                        var redirectUrl = ResolveRedirectUrl(cfg.Url, redirectLocation);

                        if (string.IsNullOrWhiteSpace(redirectUrl))
                        {
                            var redirectResult = RestApiResult.Fail(RestApiError.Validation("Redirect location is empty."));
                            FillResultMeta(redirectResult, cfg, httpCode, durationMs, responseBody);

                            #if UNITY_EDITOR
                            Debugging.RestApiTraceBus.Record(cfg, redirectResult, requestBodyForTrace);
                            #endif

                            operation.Complete(redirectResult);
                            yield break;
                        }

                        if (redirectDepth >= cfg.MaxRedirects)
                        {
                            var redirectResult = RestApiResult.Fail(RestApiError.Validation("Redirect limit exceeded."));
                            FillResultMeta(redirectResult, cfg, httpCode, durationMs, responseBody);

                            #if UNITY_EDITOR
                            Debugging.RestApiTraceBus.Record(cfg, redirectResult, requestBodyForTrace);
                            #endif

                            operation.Complete(redirectResult);
                            yield break;
                        }

                        ApplyRedirect(cfg, redirectUrl, httpCode);
                        redirectDepth++;
                        continue;
                    }

                    if ((httpCode == 401 || httpCode == 403) && cfg.NoAuth == false && cfg.AuthRetryAttempted == false &&
                        _sessionRefresher != null && _sessionRefresher.CanRefresh)
                    {
                        cfg.AuthRetryAttempted = true;
                        var refreshOp = _sessionRefresher.RefreshSessionAsync();
                        yield return refreshOp;
                        if (refreshOp.Result.IsSuccess)
                        {
                            cfg.RetryCount++;
                            DisposeRequest(request, downloadOwnership, ownsUploadHandler, finalAttempt: false);
                            continue;
                        }
                    }

                    if (networkResult != UnityWebRequest.Result.Success && isHttpSuccess == false && cfg.DisableRetry == false &&
                        cfg.RetryCount < cfg.MaxRetries)
                    {
                        cfg.RetryCount++;
                        DisposeRequest(request, downloadOwnership, ownsUploadHandler, finalAttempt: false);
                        continue;
                    }

                    RestApiResult result;

                    if (isHttpSuccess)
                    {
                        result = RestApiResult.Success();
                    }
                    else if (httpCode > 0)
                    {
                        result = RestApiResult.Fail(new RestApiError
                        {
                            Type = RestApiErrorType.Http,
                            Message = networkError,
                            Method = cfg.Method,
                            Route = cfg.Route,
                            Url = cfg.Url,
                            HttpStatusCode = httpCode,
                            NetworkResult = networkResult,
                            ResponseBody = responseBody,
                            Errors = TryParseCloudErrors(responseBody)
                        });
                    }
                    else
                    {
                        result = RestApiResult.Fail(new RestApiError
                        {
                            Type = RestApiErrorType.Network,
                            Message = networkError,
                            Method = cfg.Method,
                            Route = cfg.Route,
                            Url = cfg.Url,
                            NetworkResult = networkResult,
                            ResponseBody = responseBody
                        });
                    }

                    FillResultMeta(result, cfg, httpCode, durationMs, responseBody);

                    #if UNITY_EDITOR
                    Debugging.RestApiTraceBus.Record(cfg, result, requestBodyForTrace);
                    #endif

                    DisposeRequest(request, downloadOwnership, ownsUploadHandler, finalAttempt: true);
                    operation.Complete(result);
                    yield break;
                }
            }
            finally
            {
                // A custom interceptor may have swapped the working instance; in that case the
                // pooled original could still be referenced through the replacement, so only a
                // config that stayed ours goes back to the pool.
                if (ReferenceEquals(cfg, config))
                {
                    RestRequestConfigPool.Release(config);
                }
            }
        }

        private IEnumerator SendRequestInternal<T>(RestRequestConfig config, AsyncOperation<RestApiResult<T>> operation, Func<UnityWebRequest, T> extractData)
        {
            var cfg = config;
            var redirectDepth = 0;

            try
            {
                while (true)
                {
                    PrepareConfig(cfg);

                    foreach (var interceptor in _requestInterceptors)
                    {
                        if (interceptor == null) continue;
                        var updated = interceptor.Invoke(cfg);
                        if (updated != null)
                        {
                            cfg = updated;
                        }
                    }

                    var startTimestamp = Stopwatch.GetTimestamp();
                    var request = BuildUnityWebRequest(cfg, out var downloadOwnership, out var ownsUploadHandler);
                    yield return request.SendWebRequest();
                    var durationMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000 / Stopwatch.Frequency;

                    #if UNITY_EDITOR
                    var requestBodyForTrace = GetRequestBodyForTrace(cfg);
                    #endif

                    var responseBody = request.downloadHandler?.text;
                    var httpCode = request.responseCode;
                    var networkResult = request.result;
                    var networkError = request.error;
                    var isHttpSuccess = IsHttpSuccess(httpCode, cfg.AllowedHttpStatusCodes);

                    if (cfg.FollowRedirect && IsRedirectStatus(httpCode, cfg.RedirectHttpStatusCodes))
                    {
                        var redirectLocation = ExtractRedirectLocation(request);
                        DisposeRequest(request, downloadOwnership, ownsUploadHandler, finalAttempt: false);
                        var redirectUrl = ResolveRedirectUrl(cfg.Url, redirectLocation);

                        if (string.IsNullOrWhiteSpace(redirectUrl))
                        {
                            var redirectResult = RestApiResult<T>.Fail(RestApiError.Validation("Redirect location is empty."));
                            FillResultMeta(redirectResult, cfg, httpCode, durationMs, responseBody);

                            #if UNITY_EDITOR
                            Debugging.RestApiTraceBus.Record(cfg, redirectResult, requestBodyForTrace);
                            #endif

                            operation.Complete(redirectResult);
                            yield break;
                        }

                        if (redirectDepth >= cfg.MaxRedirects)
                        {
                            var redirectResult = RestApiResult<T>.Fail(RestApiError.Validation("Redirect limit exceeded."));
                            FillResultMeta(redirectResult, cfg, httpCode, durationMs, responseBody);

                            #if UNITY_EDITOR
                            Debugging.RestApiTraceBus.Record(cfg, redirectResult, requestBodyForTrace);
                            #endif

                            operation.Complete(redirectResult);
                            yield break;
                        }

                        ApplyRedirect(cfg, redirectUrl, httpCode);
                        redirectDepth++;
                        continue;
                    }

                    if ((httpCode == 401 || httpCode == 403) && cfg.NoAuth == false && cfg.AuthRetryAttempted == false &&
                        _sessionRefresher != null && _sessionRefresher.CanRefresh)
                    {
                        cfg.AuthRetryAttempted = true;
                        var refreshOp = _sessionRefresher.RefreshSessionAsync();
                        yield return refreshOp;
                        if (refreshOp.Result.IsSuccess)
                        {
                            cfg.RetryCount++;
                            DisposeRequest(request, downloadOwnership, ownsUploadHandler, finalAttempt: false);
                            continue;
                        }
                    }

                    if (networkResult != UnityWebRequest.Result.Success && isHttpSuccess == false && cfg.DisableRetry == false &&
                        cfg.RetryCount < cfg.MaxRetries)
                    {
                        cfg.RetryCount++;
                        DisposeRequest(request, downloadOwnership, ownsUploadHandler, finalAttempt: false);
                        continue;
                    }

                    RestApiResult<T> result;

                    if (isHttpSuccess)
                    {
                        if (extractData != null)
                        {
                            try
                            {
                                var data = extractData.Invoke(request);
                                result = RestApiResult<T>.Success(data);
                            }
                            catch (Exception ex)
                            {
                                result = RestApiResult<T>.Fail(new RestApiError
                                {
                                    Type = RestApiErrorType.Deserialize,
                                    Message = ex.Message,
                                    Method = cfg.Method,
                                    Route = cfg.Route,
                                    Url = cfg.Url,
                                    HttpStatusCode = httpCode,
                                    NetworkResult = networkResult,
                                    ResponseBody = responseBody
                                });
                            }
                        }
                        else if (string.IsNullOrEmpty(responseBody))
                        {
                            result = RestApiResult<T>.Success(default);
                        }
                        else
                        {
                            try
                            {
                                var data = JsonService.FromJson<T>(responseBody);
                                result = RestApiResult<T>.Success(data);
                            }
                            catch (Exception ex)
                            {
                                result = RestApiResult<T>.Fail(new RestApiError
                                {
                                    Type = RestApiErrorType.Deserialize,
                                    Message = ex.Message,
                                    Method = cfg.Method,
                                    Route = cfg.Route,
                                    Url = cfg.Url,
                                    HttpStatusCode = httpCode,
                                    NetworkResult = networkResult,
                                    ResponseBody = responseBody
                                });
                            }
                        }
                    }
                    else if (httpCode > 0)
                    {
                        result = RestApiResult<T>.Fail(new RestApiError
                        {
                            Type = RestApiErrorType.Http,
                            Message = networkError,
                            Method = cfg.Method,
                            Route = cfg.Route,
                            Url = cfg.Url,
                            HttpStatusCode = httpCode,
                            NetworkResult = networkResult,
                            ResponseBody = responseBody,
                            Errors = TryParseCloudErrors(responseBody)
                        });
                    }
                    else
                    {
                        result = RestApiResult<T>.Fail(new RestApiError
                        {
                            Type = RestApiErrorType.Network,
                            Message = networkError,
                            Method = cfg.Method,
                            Route = cfg.Route,
                            Url = cfg.Url,
                            NetworkResult = networkResult,
                            ResponseBody = responseBody
                        });
                    }

                    FillResultMeta(result, cfg, httpCode, durationMs, responseBody);

                    #if UNITY_EDITOR
                    Debugging.RestApiTraceBus.Record(cfg, result, requestBodyForTrace);
                    #endif

                    DisposeRequest(request, downloadOwnership, ownsUploadHandler, finalAttempt: true);
                    operation.Complete(result);
                    yield break;
                }
            }
            finally
            {
                if (ReferenceEquals(cfg, config))
                {
                    RestRequestConfigPool.Release(config);
                }
            }
        }

        private static void FillResultMeta(RestApiResult result, RestRequestConfig config, long httpCode, long durationMs, string responseBody)
        {
            result.Method = config.Method;
            result.Route = config.Route;
            result.Url = config.Url;
            result.HttpStatusCode = httpCode > 0 ? httpCode : null;
            result.RetryCount = config.RetryCount;
            result.DurationMs = durationMs;
            result.ResponseBody = responseBody;
        }

        /// <summary>
        /// Attempt to parse a non-2xx response body as the Cloud typed-error
        /// envelope (<c>{ "errors": [ ... ] }</c>). Returns null when the body
        /// is empty or does not match the contract — callers fall back to
        /// the raw <see cref="RestApiError.ResponseBody"/>.
        /// </summary>
        private List<CloudApiError> TryParseCloudErrors(string responseBody)
        {
            if (string.IsNullOrEmpty(responseBody))
            {
                return null;
            }

            try
            {
                var dto = JsonService.FromJson<ErrorResponseDto>(responseBody);
                return dto?.Errors;
            }
            catch
            {
                // Non-cloud endpoints, HTML error pages, or bodies that simply
                // don't match the envelope all land here. The raw text remains
                // available via ResponseBody for diagnostics.
                return null;
            }
        }

        private RestRequestConfig BuildConfig(string route, string method, object body, RestRequestConfig config)
        {
            // The client always works on a pooled copy; a caller-supplied config is copied in
            // and never mutated, so callers can safely keep and reuse their own instances.
            var cfg = RestRequestConfigPool.Get();
            if (config != null)
            {
                cfg.CopyFrom(config);
            }
            cfg.Route = route;
            cfg.Method = method;
            if (string.IsNullOrEmpty(cfg.TraceRoute))
            {
                cfg.TraceRoute = route;
            }
            if (string.IsNullOrEmpty(cfg.TraceUrl))
            {
                cfg.TraceUrl = GetUrl(route);
            }
            if (body != null)
            {
                cfg.Body = body;
            }
            if (cfg.MaxRetries <= 0)
            {
                cfg.MaxRetries = 1;
            }
            if (cfg.MaxRedirects <= 0)
            {
                cfg.MaxRedirects = 5;
            }
            return cfg;
        }

        private void PrepareConfig(RestRequestConfig cfg)
        {
            cfg.Url = GetUrl(cfg.Route);

            if (string.IsNullOrEmpty(cfg.TraceRoute))
            {
                cfg.TraceRoute = cfg.Route;
            }

            if (string.IsNullOrEmpty(cfg.TraceUrl))
            {
                cfg.TraceUrl = GetUrl(cfg.TraceRoute);
            }

            if (cfg.MaxRetries <= 0)
            {
                cfg.MaxRetries = 1;
            }

            if (cfg.MaxRedirects <= 0)
            {
                cfg.MaxRedirects = 5;
            }

            if (cfg.FollowRedirect)
            {
                cfg.RedirectLimit = 0;
            }

            if (cfg.SerializedBody == null && cfg.Body != null && cfg.MultipartFormSections == null && cfg.UploadHandler == null)
            {
                var bodyJson = JsonService.ToJson(cfg.Body);
                cfg.SerializedBody = Encoding.UTF8.GetBytes(bodyJson);
            }
        }

        private UnityWebRequest BuildUnityWebRequest(RestRequestConfig config, out DownloadHandlerOwnership downloadOwnership, out bool ownsUploadHandler)
        {
            _logger.Log($"Send {config.Method} request: {config.Url}");
            UnityWebRequest request;
            if (config.MultipartFormSections != null)
            {
                request = UnityWebRequest.Post(config.Url, config.MultipartFormSections);
                request.method = config.Method;
                ownsUploadHandler = true;
            }
            else
            {
                request = new UnityWebRequest(config.Url, config.Method);
                ownsUploadHandler = false;
            }

            if (config.SerializedBody != null && config.SerializedBody.Length > 0)
            {
                request.uploadHandler = new UploadHandlerRaw(config.SerializedBody);
                request.SetRequestHeader("Content-Type", "application/json");
                ownsUploadHandler = true;
            }

            if (config.UploadHandler != null)
            {
                var replaced = ownsUploadHandler ? request.uploadHandler : null;
                request.uploadHandler = config.UploadHandler;
                replaced?.Dispose();
                ownsUploadHandler = false;
            }

            // A fresh download handler per attempt: UnityWebRequest handlers are single-use,
            // so retries/redirects must not inherit an already-consumed one.
            if (config.DownloadHandlerFactory != null)
            {
                var replaced = request.downloadHandler;
                request.downloadHandler = config.DownloadHandlerFactory.Invoke(config.Url);
                replaced?.Dispose();
                downloadOwnership = DownloadHandlerOwnership.Factory;
            }
            else if (config.DownloadHandler != null)
            {
                var replaced = request.downloadHandler;
                request.downloadHandler = config.DownloadHandler;
                if (ReferenceEquals(replaced, config.DownloadHandler) == false)
                {
                    replaced?.Dispose();
                }
                downloadOwnership = DownloadHandlerOwnership.Caller;
            }
            else
            {
                if (request.downloadHandler == null)
                {
                    request.downloadHandler = new DownloadHandlerBuffer();
                }
                downloadOwnership = DownloadHandlerOwnership.Own;
            }

            if (config.TimeoutMs.HasValue)
            {
                request.timeout = Mathf.CeilToInt(config.TimeoutMs.Value / 1000f);
            }

            if (config.RedirectLimit.HasValue)
            {
                request.redirectLimit = config.RedirectLimit.Value;
            }

            if (config.Headers != null)
            {
                foreach (var header in config.Headers)
                {
                    // A null value makes Unity throw, and the value itself may carry free-form
                    // player text (nickname, segment keys) that UnityWebRequest refuses outright.
                    if (header.Value == null)
                    {
                        continue;
                    }

                    request.SetRequestHeader(header.Key, MakeHeaderValueTransportSafe(header.Value));
                }
            }

            return request;
        }

        /// <summary>
        /// UnityWebRequest only accepts header values built from printable ASCII (0x20..0x7E):
        /// a Cyrillic nickname or an emoji makes SetRequestHeader throw
        /// "Header value contains invalid characters" and takes the whole request down with it.
        /// Values that are already clean are returned untouched (byte-identical on the wire);
        /// anything else is percent-encoded from its UTF-8 bytes, so the server restores the
        /// original with Uri.UnescapeDataString.
        /// </summary>
        private static string MakeHeaderValueTransportSafe(string value)
        {
            if (IsHeaderValueTransportSafe(value))
            {
                return value;
            }

            var bytes = Encoding.UTF8.GetBytes(value);
            var builder = new StringBuilder(bytes.Length + 8);
            for (var i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i];
                if (b >= 0x20 && b <= 0x7E && b != (byte)'%')
                {
                    builder.Append((char)b);
                }
                else
                {
                    builder.Append('%');
                    builder.Append(HexDigits[b >> 4]);
                    builder.Append(HexDigits[b & 0x0F]);
                }
            }

            return builder.ToString();
        }

        private static bool IsHeaderValueTransportSafe(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c < 0x20 || c > 0x7E)
                {
                    return false;
                }
            }

            return true;
        }

        private static void DisposeRequest(UnityWebRequest request, DownloadHandlerOwnership downloadOwnership, bool ownsUploadHandler, bool finalAttempt)
        {
            if (request == null)
            {
                return;
            }

            request.disposeDownloadHandlerOnDispose =
                downloadOwnership == DownloadHandlerOwnership.Own ||
                (downloadOwnership == DownloadHandlerOwnership.Factory && finalAttempt == false);
            request.disposeUploadHandlerOnDispose = ownsUploadHandler;
            request.Dispose();
        }

        private static bool IsHttpSuccess(long httpCode, long[] allowedHttpStatusCodes)
        {
            if (httpCode >= 200 && httpCode <= 299)
            {
                return true;
            }

            if (allowedHttpStatusCodes == null || allowedHttpStatusCodes.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < allowedHttpStatusCodes.Length; i++)
            {
                if (allowedHttpStatusCodes[i] == httpCode)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRedirectStatus(long httpCode, long[] redirectHttpStatusCodes)
        {
            var codes = redirectHttpStatusCodes ?? DefaultRedirectHttpStatusCodes;
            if (codes == null || codes.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < codes.Length; i++)
            {
                if (codes[i] == httpCode)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ExtractRedirectLocation(UnityWebRequest request)
        {
            return request.GetResponseHeader("Location") ?? request.GetResponseHeader("location");
        }

        private static string ResolveRedirectUrl(string currentUrl, string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            if (Uri.TryCreate(location, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            if (Uri.TryCreate(currentUrl, UriKind.Absolute, out var baseUri) &&
                Uri.TryCreate(baseUri, location, out var resolved))
            {
                return resolved.ToString();
            }

            return location;
        }

        private static void ApplyRedirect(RestRequestConfig config, string redirectUrl, long httpCode)
        {
            if (config.StripHeadersOnRedirect)
            {
                config.Headers?.Clear();
            }
            else if (config.NoAuthOnRedirect && config.Headers != null)
            {
                config.Headers.Remove("Authorization");
                config.Headers.Remove("authorization");
            }

            if (httpCode == 303 && string.Equals(config.Method, UnityWebRequest.kHttpVerbGET, StringComparison.OrdinalIgnoreCase) == false)
            {
                config.Method = UnityWebRequest.kHttpVerbGET;
                config.Body = null;
                config.SerializedBody = null;
                config.MultipartFormSections = null;
                config.UploadHandler = null;
            }

            if (config.NoAuthOnRedirect)
            {
                config.NoAuth = true;
            }

            config.Route = redirectUrl;
            config.Url = redirectUrl;
            config.RedirectLimit = 0;
        }

        public string GetUrl(string route)
        {
            if (string.IsNullOrEmpty(route))
            {
                return BaseUrl;
            }
            if (route.StartsWith("http"))
            {
                return route;
            }
            return $"{BaseUrl}{route}";
        }

        #if UNITY_EDITOR
        private static string GetRequestBodyForTrace(RestRequestConfig preparedConfig)
        {
            if (preparedConfig == null)
            {
                return null;
            }

            if (preparedConfig.SerializedBody != null && preparedConfig.SerializedBody.Length > 0)
            {
                return Encoding.UTF8.GetString(preparedConfig.SerializedBody);
            }

            if (preparedConfig.MultipartFormSections != null)
            {
                return $"[multipart] sections={preparedConfig.MultipartFormSections.Count}";
            }

            if (preparedConfig.UploadHandler != null)
            {
                return $"[uploadHandler] {preparedConfig.UploadHandler.GetType().Name}";
            }

            return null;
        }
        #endif
    }
}
