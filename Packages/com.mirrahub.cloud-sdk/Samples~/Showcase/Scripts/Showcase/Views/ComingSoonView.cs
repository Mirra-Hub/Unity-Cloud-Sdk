using System;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>Placeholder module detail (back + title + friendly message) until its real
    /// IServiceView is built. Used because the raw/JSON fallback was removed.</summary>
    public sealed class ComingSoonView : VisualElement
    {
        public ComingSoonView(ServiceMeta module, Action onBack)
        {
            AddToClassList("sc-detail");

            var header = new VisualElement();
            header.AddToClassList("sc-detail__header");
            var back = new Button(() => onBack?.Invoke()) { text = LucideIcon.ArrowLeft };
            back.AddToClassList("sc-back-btn");
            back.AddToClassList("sc-icon");
            var title = new Label(module.Title);
            title.AddToClassList("sc-detail__title");
            title.style.color = module.Accent;
            header.Add(back);
            header.Add(title);
            Add(header);

            var body = new VisualElement();
            body.AddToClassList("sc-detail__body");
            var glyph = new Label(module.Glyph);
            glyph.AddToClassList("sc-coming__glyph");
            glyph.AddToClassList("sc-icon");
            glyph.style.color = module.Accent;
            var msg = new Label("A polished " + module.Title + " view is coming soon.");
            msg.AddToClassList("sc-coming__msg");
            body.Add(glyph);
            body.Add(msg);
            Add(body);
        }
    }
}
