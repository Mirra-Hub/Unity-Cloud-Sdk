using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MirraCloud.Core.WebView.Protocol;
using MirraCloud.Core.WebView.Utils;

namespace MirraCloud.Core.WebView.Dispatching
{
    internal sealed class WebViewCallbackDispatcher
    {
        private readonly List<Registration> _registrations = new List<Registration>();

        public void Register(string urlKey, IWebViewCallbackHandler handler)
        {
            if (string.IsNullOrEmpty(urlKey) || handler == null)
            {
                return;
            }

            var normalizedKey = CallbackUrlParser.NormalizeForPrefixCompare(urlKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                return;
            }

            _registrations.Add(new Registration(urlKey, normalizedKey, handler));
        }

        public void Unregister(string urlKey)
        {
            if (string.IsNullOrEmpty(urlKey))
            {
                return;
            }

            var normalizedKey = CallbackUrlParser.NormalizeForPrefixCompare(urlKey);
            _registrations.RemoveAll(r => string.Equals(r.NormalizedKey, normalizedKey, StringComparison.Ordinal));
        }

        public void Clear()
        {
            _registrations.Clear();
        }

        public bool Dispatch(WebViewCallbackEnvelope envelope)
        {
            if (envelope == null || string.IsNullOrEmpty(envelope.NormalizedBase))
            {
                return false;
            }

            for (var i = 0; i < _registrations.Count; i++)
            {
                var registration = _registrations[i];
                if (envelope.NormalizedBase.StartsWith(registration.NormalizedKey, StringComparison.Ordinal))
                {
                    registration.Handler.Handle(envelope);
                    return true;
                }
            }

            return false;
        }

        public string BuildHookRegex()
        {
            if (_registrations.Count == 0)
            {
                return null;
            }

            // Native WebView bridges (Android, iOS) typically run the hook regex with full-match
            // semantics (Pattern.matches / NSRegularExpression on the whole string), so a bare
            // "^(https://callback)" pattern would NOT match the real callback URL that arrives with
            // a query string ("https://callback?mirra_openid_key=...").
            // Anchor on both sides and explicitly accept any trailing path/query/fragment so the
            // hook fires reliably across platforms.
            var builder = new StringBuilder();
            builder.Append("^(?:");
            for (var i = 0; i < _registrations.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('|');
                }
                builder.Append(Regex.Escape(_registrations[i].RawKey));
            }
            builder.Append(")(?:[/?#].*)?$");
            return builder.ToString();
        }

        private readonly struct Registration
        {
            public string RawKey { get; }
            public string NormalizedKey { get; }
            public IWebViewCallbackHandler Handler { get; }

            public Registration(string rawKey, string normalizedKey, IWebViewCallbackHandler handler)
            {
                RawKey = rawKey;
                NormalizedKey = normalizedKey;
                Handler = handler;
            }
        }
    }
}
