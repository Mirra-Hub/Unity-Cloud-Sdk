using System;
using System.Collections.Generic;

namespace MirraCloud.Core.AssetsStorage
{
    /// <summary>
    /// Path lookups over the catalog <c>LoadConfigAsync</c> brought — no request. Compared by
    /// <see cref="StringComparison.Ordinal"/>, as the server compares them.
    /// </summary>
    internal static class AssetLookup
    {
        /// <param name="storedPath">A path in stored form, <c>/icons/coin.png</c>.</param>
        public static Asset FindByPath(IReadOnlyList<Asset> assets, string storedPath)
        {
            for (int i = 0; i < assets.Count; i++)
            {
                if (string.Equals(assets[i].Path, storedPath, StringComparison.Ordinal))
                {
                    return assets[i];
                }
            }

            return null;
        }

        /// <param name="storedFolderPath">A folder in stored form, <c>/icons</c>, or an empty string for the
        /// branch root.</param>
        /// <param name="recursive">False: only the assets directly in the folder. True: its subfolders too.</param>
        public static List<Asset> InFolder(IReadOnlyList<Asset> assets, string storedFolderPath, bool recursive)
        {
            // By path rather than by FolderId: the server re-paths every asset when a folder is renamed or an
            // asset moved, so the path alone answers both the direct and the recursive question.
            string prefix = storedFolderPath + "/";
            var found = new List<Asset>();

            for (int i = 0; i < assets.Count; i++)
            {
                string path = assets[i].Path;

                if (path == null || path.StartsWith(prefix, StringComparison.Ordinal) == false)
                {
                    continue;
                }

                if (recursive || path.IndexOf('/', prefix.Length) < 0)
                {
                    found.Add(assets[i]);
                }
            }

            return found;
        }
    }
}
