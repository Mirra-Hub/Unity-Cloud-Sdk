using System;
using System.Collections.Generic;
using MirraCloud.Core.WebView.Protocol;

namespace MirraCloud.Core.WebView.Utils
{
    internal static class CallbackUrlParser
    {
        public static string NormalizeForPrefixCompare(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            var questionIndex = url.IndexOf('?');
            var basePart = questionIndex >= 0 ? url.Substring(0, questionIndex) : url;
            return basePart.TrimEnd('/').ToLowerInvariant();
        }

        public static Dictionary<string, string> ParseQuery(string url)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(url))
            {
                return result;
            }

            var questionIndex = url.IndexOf('?');
            if (questionIndex < 0 || questionIndex == url.Length - 1)
            {
                return result;
            }

            var query = url.Substring(questionIndex + 1);
            var hashIndex = query.IndexOf('#');
            if (hashIndex >= 0)
            {
                query = query.Substring(0, hashIndex);
            }

            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                if (string.IsNullOrEmpty(pair))
                {
                    continue;
                }

                var equalIndex = pair.IndexOf('=');
                if (equalIndex <= 0)
                {
                    continue;
                }

                var name = Uri.UnescapeDataString(pair.Substring(0, equalIndex));
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var value = Uri.UnescapeDataString(pair.Substring(equalIndex + 1));
                result[name] = value;
            }

            return result;
        }

        public static WebViewCallbackEnvelope BuildEnvelope(string url, WebViewCallbackSource source)
        {
            return new WebViewCallbackEnvelope(
                url,
                NormalizeForPrefixCompare(url),
                ParseQuery(url),
                source);
        }
    }
}
