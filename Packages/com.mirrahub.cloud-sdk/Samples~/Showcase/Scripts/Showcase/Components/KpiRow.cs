using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// A row of <see cref="StatTile"/>s with an optional trend line under each value. Exists so a
    /// screen can always draw the same headline strip: on an empty project the tiles are added via
    /// <see cref="AddZero"/> and render muted zeros instead of disappearing.
    /// </summary>
    public sealed class KpiRow : VisualElement
    {
        public KpiRow()
        {
            // sc-stat-grid carries the shared wrap/spacing rules; sc-kpi-row only widens the tiles.
            AddToClassList("sc-stat-grid");
            AddToClassList("sc-kpi-row");
        }

        /// <summary>
        /// Appends a tile. <paramref name="trend"/> is shown verbatim under the value and colored by
        /// its first character: '+' reads as up, '-' as down, anything else (e.g. "—") as flat.
        /// </summary>
        public KpiRow Add(string caption, string glyph, string value, string trend = null, bool hi = false)
        {
            var tile = new StatTile(caption, glyph);
            tile.Set(string.IsNullOrEmpty(value) ? Fmt.Dash : value);
            tile.Highlight(hi);
            AttachTrend(tile, trend);
            Add(tile);
            return this;
        }

        /// <summary>
        /// Appends the muted "there is nothing to count yet" variant. It still carries a flat trend
        /// line so a mixed row keeps every tile the same height.
        /// </summary>
        public KpiRow AddZero(string caption, string glyph, string zeroText = "0")
        {
            var tile = new StatTile(caption, glyph);
            tile.Set(string.IsNullOrEmpty(zeroText) ? "0" : zeroText);
            tile.AddToClassList("sc-kpi--zero");
            AttachTrend(tile, Fmt.Dash);
            Add(tile);
            return this;
        }

        /// <summary>Removes every tile. Named Clear2 because VisualElement.Clear is not virtual.</summary>
        public KpiRow Clear2()
        {
            Clear();
            return this;
        }

        private static void AttachTrend(StatTile tile, string trend)
        {
            if (string.IsNullOrEmpty(trend))
            {
                return;
            }
            string text = trend.Trim();
            if (text.Length == 0)
            {
                return;
            }

            var line = new VisualElement();
            line.AddToClassList("sc-kpi__trend");
            line.pickingMode = PickingMode.Ignore;

            char sign = text[0];
            string glyph = null;
            if (sign == '+')
            {
                line.AddToClassList("sc-kpi__trend--up");
                glyph = LucideIcon.TrendingUp;
            }
            else if (sign == '-' || sign == '\u2212')
            {
                line.AddToClassList("sc-kpi__trend--down");
                glyph = LucideIcon.TrendingDown;
            }
            else
            {
                // Flat: the dash carries the meaning on its own, an extra arrow would only add noise.
                line.AddToClassList("sc-kpi__trend--flat");
            }

            if (glyph != null)
            {
                var g = new Label(glyph);
                g.AddToClassList("sc-kpi__trend-glyph");
                g.AddToClassList("sc-icon");
                line.Add(g);
            }

            var l = new Label(text);
            l.enableRichText = false;
            l.AddToClassList("sc-kpi__trend-text");
            line.Add(l);

            // StatTile lays out glyph / value / caption — the trend belongs right under the value.
            var valueLabel = tile.Q<Label>(className: "sc-stat-tile__value");
            int at = valueLabel != null ? tile.IndexOf(valueLabel) : -1;
            if (at >= 0)
            {
                tile.Insert(at + 1, line);
            }
            else
            {
                tile.Add(line);
            }
        }
    }
}
