using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>Rounded surface container with an optional accent-tinted header band.</summary>
    public sealed class Card : VisualElement
    {
        public readonly VisualElement Header;
        public readonly VisualElement Body;

        public Card(Color? accent = null)
        {
            AddToClassList("sc-card");

            Header = new VisualElement();
            Header.AddToClassList("sc-card__header");
            if (accent.HasValue)
            {
                Header.style.backgroundColor = new Color(accent.Value.r, accent.Value.g, accent.Value.b, 0.14f);
            }
            Header.style.display = DisplayStyle.None;
            Add(Header);

            Body = new VisualElement();
            Body.AddToClassList("sc-card__body");
            Add(Body);
        }

        public Card WithHeader(VisualElement content)
        {
            Header.Clear();
            Header.Add(content);
            Header.style.display = DisplayStyle.Flex;
            return this;
        }

        public Card WithTitle(string title, Color? titleColor = null)
        {
            var l = new Label(title);
            l.AddToClassList("sc-card__title");
            if (titleColor.HasValue)
            {
                l.style.color = titleColor.Value;
            }
            return WithHeader(l);
        }
    }
}
