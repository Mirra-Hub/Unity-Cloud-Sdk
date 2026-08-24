using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>Transient corner notifications. Construct once with a host element (overlay layer).</summary>
    public sealed class Toasts
    {
        private readonly VisualElement _host;

        public Toasts(VisualElement host)
        {
            _host = host;
        }

        public void Ok(string msg) => Show(msg, "sc-toast--ok");
        public void Fail(string msg) => Show(msg, "sc-toast--bad");
        public void Info(string msg) => Show(msg, "sc-toast--info");

        private void Show(string msg, string toneClass)
        {
            if (_host == null)
            {
                return;
            }
            var t = new Label(msg);
            t.AddToClassList("sc-toast");
            t.AddToClassList(toneClass);
            t.enableRichText = false;
            _host.Add(t);
            t.schedule.Execute(() =>
            {
                if (t.parent != null)
                {
                    t.RemoveFromHierarchy();
                }
            }).StartingIn(2800);
        }
    }
}
