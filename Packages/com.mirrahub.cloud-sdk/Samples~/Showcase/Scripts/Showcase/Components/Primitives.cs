using MirraCloud.Core;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>Shimmer placeholder shown into a slot while a load is in flight.</summary>
    public static class Skeleton
    {
        public static void Into(VisualElement slot, int rows = 3)
        {
            slot.Clear();
            var box = new VisualElement();
            box.AddToClassList("sc-skeleton");
            for (int i = 0; i < rows; i++)
            {
                var r = new VisualElement();
                r.AddToClassList("sc-skeleton__row");
                box.Add(r);
            }
            slot.Add(box);
        }
    }

    /// <summary>Friendly empty state (glyph + message).</summary>
    public static class EmptyState
    {
        public static VisualElement Default() => Build(LucideIcon.Inbox, "Nothing here yet");

        public static VisualElement Build(string glyph, string message)
        {
            var v = new VisualElement();
            v.AddToClassList("sc-state");
            v.AddToClassList("sc-state--empty");
            var g = new Label(glyph);
            g.AddToClassList("sc-state__glyph");
            g.AddToClassList("sc-icon");
            var m = new Label(message);
            m.AddToClassList("sc-state__msg");
            v.Add(g);
            v.Add(m);
            return v;
        }
    }

    /// <summary>Error state built from a <see cref="RestApiError"/> (or a raw message).</summary>
    public static class ErrorState
    {
        public static VisualElement Build(RestApiError error)
        {
            string msg = "Request failed";
            if (error != null)
            {
                msg = string.IsNullOrEmpty(error.Message) ? error.Type.ToString() : error.Message;
            }
            return Message(msg);
        }

        public static VisualElement Message(string msg)
        {
            var v = new VisualElement();
            v.AddToClassList("sc-state");
            v.AddToClassList("sc-state--error");
            var g = new Label(LucideIcon.CircleAlert);
            g.AddToClassList("sc-state__glyph");
            g.AddToClassList("sc-icon");
            var m = new Label(msg);
            m.enableRichText = false;
            m.AddToClassList("sc-state__msg");
            v.Add(g);
            v.Add(m);
            return v;
        }
    }

    /// <summary>Section title with an optional count chip.</summary>
    public sealed class SectionHeader : VisualElement
    {
        private readonly Label _count;

        public SectionHeader(string title, string countText = null)
        {
            AddToClassList("sc-section-header");
            var t = new Label(title);
            t.AddToClassList("sc-section-header__title");
            Add(t);
            _count = new Label(countText ?? string.Empty);
            _count.AddToClassList("sc-section-header__count");
            _count.style.display = string.IsNullOrEmpty(countText) ? DisplayStyle.None : DisplayStyle.Flex;
            Add(_count);
        }

        public SectionHeader SetCount(string text)
        {
            _count.text = text ?? string.Empty;
            _count.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            return this;
        }
    }

    /// <summary>Horizontal fill bar with an optional inline label.</summary>
    public sealed class ProgressBar : VisualElement
    {
        private readonly VisualElement _fill;
        private readonly Label _label;

        public ProgressBar()
        {
            AddToClassList("sc-pbar");
            var track = new VisualElement();
            track.AddToClassList("sc-pbar__track");
            _fill = new VisualElement();
            _fill.AddToClassList("sc-pbar__fill");
            track.Add(_fill);
            Add(track);
            _label = new Label();
            _label.AddToClassList("sc-pbar__label");
            _label.style.display = DisplayStyle.None;
            Add(_label);
        }

        public ProgressBar Set(float current, float max)
        {
            float p = max <= 0f ? 0f : UnityEngine.Mathf.Clamp01(current / max);
            _fill.style.width = Length.Percent(p * 100f);
            return this;
        }

        public ProgressBar SetLabel(string text)
        {
            _label.text = text ?? string.Empty;
            _label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            return this;
        }

        public ProgressBar SetAccent(UnityEngine.Color color)
        {
            _fill.style.backgroundColor = color;
            return this;
        }
    }
}
