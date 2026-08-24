using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace MirraCloud.Core
{
    public class RestRequestConfig
    {
        public string Route;
        public string Method;
        public string TraceRoute;
        public string TraceUrl;
        public object Body;
        public byte[] SerializedBody;
        public Dictionary<string, string> Headers;
        public List<IMultipartFormSection> MultipartFormSections;
        public DownloadHandler DownloadHandler;
        public Func<string, DownloadHandler> DownloadHandlerFactory;
        public UploadHandler UploadHandler;
        public int? TimeoutMs;
        public int? RedirectLimit;
        public bool FollowRedirect;
        public int MaxRedirects = 5;
        public long[] RedirectHttpStatusCodes;
        public bool NoAuthOnRedirect;
        public bool StripHeadersOnRedirect;
        public long[] AllowedHttpStatusCodes;
        public int MaxRetries = 1;
        public int RetryCount;
        public bool AuthRetryAttempted;
        public bool DisableRetry;
        public bool NoAuth;

        internal string Url;

        /// <summary>True while this instance is rented from <see cref="RestRequestConfigPool"/>.
        /// Caller-created configs never carry the flag, so the pool leaves them alone.</summary>
        internal bool Rented;

        internal void CopyFrom(RestRequestConfig source)
        {
            Route = source.Route;
            Method = source.Method;
            TraceRoute = source.TraceRoute;
            TraceUrl = source.TraceUrl;
            Body = source.Body;
            SerializedBody = source.SerializedBody;
            if (source.Headers != null)
            {
                Headers ??= new Dictionary<string, string>(source.Headers.Count);
                foreach (var header in source.Headers)
                {
                    Headers[header.Key] = header.Value;
                }
            }
            MultipartFormSections = source.MultipartFormSections;
            DownloadHandler = source.DownloadHandler;
            DownloadHandlerFactory = source.DownloadHandlerFactory;
            UploadHandler = source.UploadHandler;
            TimeoutMs = source.TimeoutMs;
            RedirectLimit = source.RedirectLimit;
            FollowRedirect = source.FollowRedirect;
            MaxRedirects = source.MaxRedirects;
            RedirectHttpStatusCodes = source.RedirectHttpStatusCodes;
            NoAuthOnRedirect = source.NoAuthOnRedirect;
            StripHeadersOnRedirect = source.StripHeadersOnRedirect;
            AllowedHttpStatusCodes = source.AllowedHttpStatusCodes;
            MaxRetries = source.MaxRetries;
            RetryCount = source.RetryCount;
            AuthRetryAttempted = source.AuthRetryAttempted;
            DisableRetry = source.DisableRetry;
            NoAuth = source.NoAuth;
            Url = source.Url;
        }

        internal void Reset()
        {
            Route = null;
            Method = null;
            TraceRoute = null;
            TraceUrl = null;
            Body = null;
            SerializedBody = null;
            Headers?.Clear();
            MultipartFormSections = null;
            DownloadHandler = null;
            DownloadHandlerFactory = null;
            UploadHandler = null;
            TimeoutMs = null;
            RedirectLimit = null;
            FollowRedirect = false;
            MaxRedirects = 5;
            RedirectHttpStatusCodes = null;
            NoAuthOnRedirect = false;
            StripHeadersOnRedirect = false;
            AllowedHttpStatusCodes = null;
            MaxRetries = 1;
            RetryCount = 0;
            AuthRetryAttempted = false;
            DisableRetry = false;
            NoAuth = false;
            Url = null;
        }
    }
}
