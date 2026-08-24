using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Drop this on a GameObject with a UIDocument (source = Showcase.uxml) to render the M0
    /// component gallery. No SDK wiring — pure design-system proof.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ShowcaseGalleryBehaviour : MonoBehaviour
    {
        private void Start()
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null)
            {
                return;
            }
            var root = doc.rootVisualElement;
            var holder = root.Q<VisualElement>("sc-screen") ?? root;
            holder.Clear();
            holder.Add(ComponentGallery.Build(new RemoteImageLoader()));
        }
    }
}
