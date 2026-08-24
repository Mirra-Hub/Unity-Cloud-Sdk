using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Circular avatar: async remote image with an initials+color fallback (never a broken box),
    /// plus an optional presence dot.
    /// </summary>
    public sealed class Avatar : VisualElement
    {
        private readonly Image _img;
        private readonly Label _initials;
        private readonly VisualElement _presence;

        public Avatar(float size = 48f)
        {
            AddToClassList("sc-avatar");
            style.width = size;
            style.height = size;

            _img = new Image { scaleMode = ScaleMode.ScaleAndCrop };
            _img.AddToClassList("sc-avatar__img");
            _img.style.display = DisplayStyle.None;
            Add(_img);

            _initials = new Label("?");
            _initials.AddToClassList("sc-avatar__initials");
            Add(_initials);

            _presence = new VisualElement();
            _presence.AddToClassList("sc-avatar__presence");
            _presence.style.display = DisplayStyle.None;
            Add(_presence);
        }

        public Avatar SetInitialsFor(string name)
        {
            _initials.text = ToInitials(name);
            style.backgroundColor = ColorFor(name);
            _initials.style.display = DisplayStyle.Flex;
            _img.style.display = DisplayStyle.None;
            return this;
        }

        /// <summary>Shows the initials immediately, then swaps in the remote image if it loads.</summary>
        public async void BindUrl(RemoteImageLoader loader, string url, string fallbackName)
        {
            SetInitialsFor(fallbackName);
            if (loader == null || string.IsNullOrEmpty(url))
            {
                return;
            }
            var tex = await loader.Load(url);
            if (tex == null)
            {
                return;
            }
            _img.image = tex;
            _img.style.display = DisplayStyle.Flex;
            _initials.style.display = DisplayStyle.None;
        }

        public Avatar SetPresence(Color? color)
        {
            if (color.HasValue)
            {
                _presence.style.backgroundColor = color.Value;
                _presence.style.display = DisplayStyle.Flex;
            }
            else
            {
                _presence.style.display = DisplayStyle.None;
            }
            return this;
        }

        private static string ToInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "?";
            }
            var parts = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "?";
            }
            string s = parts[0].Substring(0, 1);
            if (parts.Length > 1)
            {
                s += parts[1].Substring(0, 1);
            }
            return s.ToUpperInvariant();
        }

        private static Color ColorFor(string name)
        {
            int h = 0;
            if (!string.IsNullOrEmpty(name))
            {
                foreach (char c in name)
                {
                    h = (h * 31 + c) & 0x7fffffff;
                }
            }
            float hue = (h % 360) / 360f;
            return Color.HSVToRGB(hue, 0.45f, 0.50f);
        }
    }
}
