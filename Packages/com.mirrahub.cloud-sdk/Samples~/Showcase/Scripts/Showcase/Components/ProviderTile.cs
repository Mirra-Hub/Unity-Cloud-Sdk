using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>A clickable provider button: accent-tinted glyph badge + label.</summary>
    public sealed class ProviderTile : VisualElement
    {
        public ProviderTile(string label, string glyph, Color accent, Action onClick)
        {
            AddToClassList("sc-provider");
            RegisterCallback<ClickEvent>(_ => onClick?.Invoke());

            var badge = new VisualElement();
            badge.AddToClassList("sc-provider__badge");
            badge.style.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.18f);
            var g = new Label(glyph);
            g.AddToClassList("sc-provider__glyph");
            g.style.color = accent;
            badge.Add(g);

            var l = new Label(label);
            l.AddToClassList("sc-provider__label");

            Add(badge);
            Add(l);
        }
    }
}
