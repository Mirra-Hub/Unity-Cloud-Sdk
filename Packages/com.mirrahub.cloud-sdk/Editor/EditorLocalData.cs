using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MirraCloud.Core.Storage;
using MirraCloud.Core.Storage.Blob;
using UnityEditor;
using UnityEngine;

namespace MirraCloud.Editor
{
    /// <summary>
    /// The SDK's data in <c>persistentDataPath</c> as the editor sees it: where a container lies, how much it takes,
    /// what the asset cache holds, and deleting a container. Shared by the Data menu and the cache window.
    /// </summary>
    internal static class EditorLocalData
    {
        /// <summary>Raised on the main thread with the id of a container that was just deleted.</summary>
        public static event Action<string> ContainerDeleted;

        /// <summary>While the game runs, the SDK holds its containers open, so nothing may delete them.</summary>
        public static bool IsPlaying => EditorApplication.isPlayingOrWillChangePlaymode;

        public static string RevealLabel =>
            Application.platform == RuntimePlatform.OSXEditor ? "Reveal in Finder" : "Show in Explorer";

        public static string DatabasePath(string containerId)
        {
            using (SqliteBlobStorage storage = new SqliteBlobStorage())
            {
                return storage.GetDatabasePath(containerId);
            }
        }

        /// <summary>The database with its WAL files; 0 when there is nothing on disk.</summary>
        public static long SizeOnDisk(string containerId)
        {
            string path = DatabasePath(containerId);

            return FileSize(path) + FileSize(path + "-wal") + FileSize(path + "-shm");
        }

        /// <summary>Shows why nothing happens when Play Mode started between opening the menu and the click.</summary>
        public static bool RefuseWhilePlaying()
        {
            if (IsPlaying == false)
            {
                return false;
            }

            EditorUtility.DisplayDialog("MirraCloud",
                "Stop Play Mode first: while the game runs, the SDK holds its local data open.", "OK");
            return true;
        }

        /// <summary>
        /// Every entry of the asset cache with its size, without reading the assets themselves. Empty when the game
        /// has cached nothing yet — the database is not created just to be looked at.
        /// </summary>
        public static async Task<IReadOnlyList<KeyValuePair<string, long>>> ReadAssetCacheAsync()
        {
            if (File.Exists(DatabasePath(LocalDataContainers.AssetCache)) == false)
            {
                return Array.Empty<KeyValuePair<string, long>>();
            }

            SqliteBlobStorage storage = new SqliteBlobStorage();

            try
            {
                // The SDK of a running game holds a connection of its own; the database is in WAL mode, so reading
                // alongside it is fine.
                IBlobContainer container = await storage.OpenContainerAsync(LocalDataContainers.AssetCache);

                using (container)
                {
                    return await ((SqliteBlobContainer)container).ListEntriesAsync();
                }
            }
            finally
            {
                storage.Dispose();
            }
        }

        public static async Task ClearAssetCacheAsync()
        {
            long size = SizeOnDisk(LocalDataContainers.AssetCache);

            if (await DeleteContainerAsync(LocalDataContainers.AssetCache, "the asset cache") == false)
            {
                return;
            }

            Debug.Log(size > 0
                ? $"Mirra Cloud: the asset cache is cleared ({EditorUtility.FormatBytes(size)}). Assets download again the next time they load."
                : "Mirra Cloud: the asset cache is already empty.");
        }

        public static async Task ClearSignInAsync()
        {
            if (await DeleteContainerAsync(LocalDataContainers.PrefsInEditor, "the saved sign-in"))
            {
                Debug.Log("Mirra Cloud: the editor's saved sign-in is cleared. The next guest sign-in creates a new player.");
            }
        }

        /// <summary>
        /// Opens the folder of a container with its database selected, or the bare folder when there is no database
        /// yet.
        /// </summary>
        public static void Reveal(string containerId)
        {
            string path = DatabasePath(containerId);

            if (File.Exists(path))
            {
                EditorUtility.RevealInFinder(path);
                return;
            }

            string folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            EditorUtility.OpenWithDefaultApp(folder);
        }

        private static async Task<bool> DeleteContainerAsync(string containerId, string what)
        {
            // An SDK left over from the last play session may still hold the database open, and Windows will not
            // delete an open file. Its connection closes when it is collected.
            GC.Collect();
            GC.WaitForPendingFinalizers();

            SqliteBlobStorage storage = new SqliteBlobStorage();

            try
            {
                await storage.DeleteContainerAsync(containerId);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Mirra Cloud: {what} could not be cleared — try again after restarting the editor. {exception.Message}");
                return false;
            }
            finally
            {
                storage.Dispose();
            }

            ContainerDeleted?.Invoke(containerId);
            return true;
        }

        private static long FileSize(string path)
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
    }
}
