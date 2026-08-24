using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>A metric tile: big value + caption + optional glyph. Optionally highlighted.</summary>
    public sealed class StatTile : VisualElement
    {
        private readonly Label _value;

        public StatTile(string caption, string glyph = null)
        {
            AddToClassList("sc-stat-tile");

            if (!string.IsNullOrEmpty(glyph))
            {
                var g = new Label(glyph);
                g.AddToClassList("sc-stat-tile__glyph");
                g.AddToClassList("sc-icon");
                Add(g);
            }

            _value = new Label("—");
            _value.AddToClassList("sc-stat-tile__value");
            Add(_value);

            var cap = new Label(caption);
            cap.AddToClassList("sc-stat-tile__caption");
            Add(cap);
        }

        public StatTile Set(string value)
        {
            _value.text = value;
            return this;
        }

        public StatTile Highlight(bool on)
        {
            EnableInClassList("sc-stat-tile--hi", on);
            return this;
        }
    }
}
