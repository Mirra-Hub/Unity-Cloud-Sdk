using System;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Modal dialog system (UITK has none): a full-rect scrim + centered dialog card mounted into
    /// an overlay layer. Click on the scrim or the ✕ closes it. One dialog at a time.
    /// </summary>
    public sealed class Popup
    {
        private readonly VisualElement _host;
        private VisualElement _scrim;

        public Popup(VisualElement host)
        {
            _host = host;
        }

        public bool IsOpen => _scrim != null;

        public void Open(VisualElement content, string title, Action onClose = null)
        {
            Close();

            _scrim = new VisualElement();
            _scrim.AddToClassList("sc-scrim");
            _scrim.RegisterCallback<ClickEvent>(e =>
            {
                if (ReferenceEquals(e.target, _scrim))
                {
                    Close();
                    onClose?.Invoke();
                }
            });

            var dialog = new VisualElement();
            dialog.AddToClassList("sc-dialog");

            var head = new VisualElement();
            head.AddToClassList("sc-dialog__head");
            var t = new Label(title);
            t.AddToClassList("sc-dialog__title");
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            var close = new Button(() => { Close(); onClose?.Invoke(); }) { text = LucideIcon.X };
            close.AddToClassList("sc-dialog__close");
            close.AddToClassList("sc-icon");
            head.Add(t);
            head.Add(spacer);
            head.Add(close);

            dialog.Add(head);
            var body = new VisualElement();
            body.AddToClassList("sc-dialog__body");
            body.Add(content);
            dialog.Add(body);

            _scrim.Add(dialog);
            _host.Add(_scrim);
        }

        public void Close()
        {
            if (_scrim != null)
            {
                _scrim.RemoveFromHierarchy();
                _scrim = null;
            }
        }
    }
}
