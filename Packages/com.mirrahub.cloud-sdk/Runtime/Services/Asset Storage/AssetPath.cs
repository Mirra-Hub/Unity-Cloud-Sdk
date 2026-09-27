using System;
using System.Text;

namespace MirraCloud.Core.AssetsStorage
{
    /// <summary>
    /// Turns the path a game passes into the two forms the SDK needs: the stored one, which
    /// <see cref="BaseItemStorage.Path"/> is compared with, and the one that goes into the URL.
    /// </summary>
    /// <remarks>
    /// The server compares paths exactly, case included, with the stored form: a leading slash and
    /// names as they were uploaded (<c>/icons/coin.png</c>). A game may leave the leading slash out,
    /// double a slash or write a Windows separator; no name can contain a slash of either kind, so
    /// those are folded away here instead of turning into a 404. Names can hold spaces, Cyrillic,
    /// '#', '?', '%' or '+', which is why the URL form escapes each segment on its own: escaping the
    /// whole path would escape the slashes between folders too.
    /// </remarks>
    internal static class AssetPath
    {
        /// <param name="storedForm"><c>/icons/coin 1.png</c></param>
        /// <param name="routeForm"><c>icons/coin%201.png</c></param>
        /// <returns>False for an empty path and for <c>..</c>, which the server refuses as traversal.</returns>
        public static bool TryParse(string path, out string storedForm, out string routeForm)
        {
            storedForm = null;
            routeForm = null;

            if (string.IsNullOrWhiteSpace(path) || path.Contains(".."))
            {
                return false;
            }

            var stored = new StringBuilder(path.Length + 1);
            var route = new StringBuilder(path.Length + 16);

            foreach (string segment in path.Replace('\\', '/').Split('/'))
            {
                if (segment.Length == 0)
                {
                    continue;
                }

                stored.Append('/').Append(segment);

                if (route.Length > 0)
                {
                    route.Append('/');
                }

                route.Append(Uri.EscapeDataString(segment));
            }

            if (stored.Length == 0)
            {
                return false;
            }

            storedForm = stored.ToString();
            routeForm = route.ToString();
            return true;
        }

        /// <summary>Null, empty or nothing but separators: the branch root.</summary>
        public static bool IsRoot(string path)
        {
            return string.IsNullOrEmpty(path) || path.Trim('/', '\\').Length == 0;
        }
    }
}
