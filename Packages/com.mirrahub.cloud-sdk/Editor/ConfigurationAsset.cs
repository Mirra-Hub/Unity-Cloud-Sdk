using UnityEditor;
using UnityEngine;

namespace MirraCloud.Editor
{
    /// <summary>
    /// Finds the project's <see cref="Configuration"/> asset, creating it on first use.
    ///
    /// The asset belongs to the project, not to the SDK: a package installed from a git URL lives in
    /// the immutable package cache, so the Manager window could not write the selected project,
    /// branch and token into an asset shipped inside it.
    /// </summary>
    internal static class ConfigurationAsset
    {
        private const string ROOT_FOLDER = "Assets/MirraCloud";
        private const string RESOURCES_FOLDER = ROOT_FOLDER + "/Resources";
        private const string ASSET_PATH = RESOURCES_FOLDER + "/Configuration.asset";

        public static Configuration LoadOrCreate()
        {
            Configuration configuration = FindExisting();

            if (configuration == null)
            {
                EnsureResourcesFolder();

                configuration = ScriptableObject.CreateInstance<Configuration>();
                AssetDatabase.CreateAsset(configuration, ASSET_PATH);
                AssetDatabase.SaveAssets();

                Debug.Log($"Mirra Cloud: created {ASSET_PATH}. Keep it out of version control if it holds a token.");
            }

            // Url and EditorApiUrl are runtime-only and start out null on a freshly loaded asset.
            // Without this the editor talks to an empty base url and every call 404s, and the
            // DeveloperSettings environment is never applied.
            configuration.ResolveEnvironment();

            return configuration;
        }

        /// <summary>
        /// Picks up a configuration the project already has — including one left at the old
        /// <c>Assets/Plugins/MirraCloud/Resources</c> path — so upgrading does not lose the setup.
        /// </summary>
        private static Configuration FindExisting()
        {
            Configuration outsideResources = null;

            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(Configuration)}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Configuration asset = AssetDatabase.LoadAssetAtPath<Configuration>(path);

                if (asset == null)
                {
                    continue;
                }

                if (path.Contains("/Resources/"))
                {
                    return asset;
                }

                outsideResources = asset;
            }

            if (outsideResources != null)
            {
                Debug.LogWarning(
                    $"Mirra Cloud: {AssetDatabase.GetAssetPath(outsideResources)} is not inside a Resources " +
                    "folder, so the SDK cannot load it at runtime. Move it under one — for example " +
                    $"{RESOURCES_FOLDER}.");
            }

            return outsideResources;
        }

        private static void EnsureResourcesFolder()
        {
            if (AssetDatabase.IsValidFolder(ROOT_FOLDER) == false)
            {
                AssetDatabase.CreateFolder("Assets", "MirraCloud");
            }

            if (AssetDatabase.IsValidFolder(RESOURCES_FOLDER) == false)
            {
                AssetDatabase.CreateFolder(ROOT_FOLDER, "Resources");
            }
        }
    }
}
