using System;
using System.Threading.Tasks;
using MirraCloud.Core.Storage;
using MirraCloud.Core.Storage.Blob;
using UnityEditor;
using UnityEngine;

namespace MirraCloud.Editor
{
    /// <summary>
    /// Forgets the sign-in the editor keeps between play sessions — the session and the guest id — so the next guest
    /// sign-in makes a new player. Edit → Clear All PlayerPrefs did this while the SDK kept them in PlayerPrefs; it no
    /// longer does. Builds keep a container of their own, which this leaves alone.
    /// </summary>
    internal static class ClearSavedSignInMenu
    {
        [MenuItem("Tools/Mirra Cloud/Clear Saved Sign-In")]
        private static void ClearSavedSignIn()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Mirra Cloud",
                    "Stop Play Mode first: while the game runs, the SDK holds the saved sign-in open.", "OK");
                return;
            }

            _ = ClearAsync();
        }

        private static async Task ClearAsync()
        {
            // An SDK left over from the last play session may still hold the database open, and Windows will not
            // delete an open file. Its connection closes when it is collected.
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var storage = new SqliteBlobStorage();

            try
            {
                await storage.DeleteContainerAsync(LocalDataContainers.PrefsInEditor);
                Debug.Log("Mirra Cloud: the editor's saved sign-in is cleared. The next guest sign-in creates a new player.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"Mirra Cloud: the saved sign-in could not be cleared — try again after restarting the editor. {exception.Message}");
            }
            finally
            {
                storage.Dispose();
            }
        }
    }
}
