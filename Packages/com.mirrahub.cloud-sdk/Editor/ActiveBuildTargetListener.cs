using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MirraCloud.Editor
{
    /// <summary>
    /// Redraws an open Manager window when the build target is switched, so the build target check under the Platform
    /// dropdown follows at once. A switch usually reloads the scripts anyway (the platform defines change); this covers
    /// a switch that does not.
    /// </summary>
    internal sealed class ActiveBuildTargetListener : IActiveBuildTargetChanged
    {
        public int callbackOrder => 0;

        public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<MirraCloudEditorWindow>())
            {
                window.Repaint();
            }
        }
    }
}
