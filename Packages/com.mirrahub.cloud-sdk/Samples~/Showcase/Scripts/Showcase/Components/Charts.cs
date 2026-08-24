using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// One labelled datum for <see cref="BarChart"/> / <see cref="DonutChart"/>.
    /// <c>Color</c> is optional: when it is null the chart falls back to its accent
    /// (bars) or to the cyclic <see cref="ChartPalette"/> (donut slices).
    /// </summary>
    public sealed class ChartPoint
    {
        public string Label;
        public float Value;

        // Fully qualified on purpose: the field is called Color as well, and qualifying the
        // type keeps the declaration unambiguous for readers (and for the compiler).
        public UnityEngine.Color? Color;

        public ChartPoint()
        {
        }

        public ChartPoint(string label, float value, UnityEngine.Color? color = null)
        {
            Label = label;
            Value = value;
            Color = color;
        }
    }

    /// <summary>
    /// Chart-facing view over <see cref="ShowcaseTheme"/>. The colors live in the theme (single
    /// source for the whole showcase); this only gives chart code shorter names for them.
    /// </summary>
    public static class ChartPalette
    {
        /// <summary>Ink for grid lines and the empty-state ring: visible, but never mistaken for data.</summary>
        public static Color Grid
        {
            get { return ShowcaseTheme.ChartGrid; }
        }

        /// <summary>Ink for the zero baseline, drawn only when a series crosses it.</summary>
        public static Color Axis
        {
            get { return ShowcaseTheme.ChartAxis; }
        }

        public static Color Accent
        {
            get { return ShowcaseTheme.Series[0]; }
        }

        public static int Count
        {
            get { return ShowcaseTheme.Series.Length; }
        }

        /// <summary>Cyclic lookup, safe for any index including negative ones.</summary>
        public static Color At(int index)
        {
            return ShowcaseTheme.SeriesAt(index);
        }
    }

    /// <summary>Compact, culture-invariant number text for chart labels ("12.3k", "45%").</summary>
    public static class ChartFormat
    {
        public static string Number(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v))
            {
                return Fmt.Dash;
            }
            float a = Mathf.Abs(v);
            if (a >= 1000000f)
            {
                return (v / 1000000f).ToString("0.#", CultureInfo.InvariantCulture) + "M";
            }
            if (a >= 1000f)
            {
                return (v / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + "k";
            }
            if (a >= 100f || Mathf.Approximately(v, Mathf.Round(v)))
            {
                return Mathf.Round(v).ToString("0", CultureInfo.InvariantCulture);
            }
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Defensive copies of caller data: NaN/Infinity never reach the painter.</summary>
    internal static class ChartData
    {
        public static float[] Sanitize(IReadOnlyList<float> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<float>();
            }
            var arr = new float[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                arr[i] = Safe(values[i]);
            }
            return arr;
        }

        public static ChartPoint[] Sanitize(IReadOnlyList<ChartPoint> points)
        {
            if (points == null || points.Count == 0)
            {
                return Array.Empty<ChartPoint>();
            }
            var list = new List<ChartPoint>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (p == null)
                {
                    continue;
                }
                // Copy so a later mutation of the caller's object can't desync the painted mesh.
                list.Add(new ChartPoint(p.Label, Safe(p.Value), p.Color));
            }
            return list.ToArray();
        }

        public static float Safe(float v)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        }
    }

    /// <summary>Shared chrome: the centered caption overlay every chart shows when it has no data.</summary>
    internal static class ChartParts
    {
        public static Label Overlay(VisualElement host, string text)
        {
            var overlay = new VisualElement();
            overlay.AddToClassList("sc-chart__overlay");
            overlay.pickingMode = PickingMode.Ignore;

            var label = new Label(text);
            label.AddToClassList("sc-chart__empty");
            label.pickingMode = PickingMode.Ignore;
            overlay.Add(label);

            host.Add(overlay);
            return label;
        }
    }

    /// <summary>
    /// Painter2D primitives shared by the charts. Unity 2022.3 has no annulus helper, so rings are
    /// emitted as flat polygons (outer arc forward + inner arc backward) — cheap and free of
    /// tessellation surprises.
    /// </summary>
    internal static class ChartPaint
    {
        /// <summary>Guards the degenerate rects UI Toolkit hands out before the first layout pass.</summary>
        public static bool Usable(Rect r)
        {
            return r.width > 2f && r.height > 2f
                   && !float.IsNaN(r.width) && !float.IsNaN(r.height)
                   && !float.IsNaN(r.x) && !float.IsNaN(r.y);
        }

        public static void Grid(Painter2D p, Rect r, int rows)
        {
            if (rows < 1)
            {
                rows = 1;
            }
            p.lineWidth = 1f;
            p.lineCap = LineCap.Butt;
            p.strokeColor = ChartPalette.Grid;
            p.BeginPath();
            for (int i = 0; i <= rows; i++)
            {
                // Half-pixel snap keeps hairlines crisp instead of smeared over two rows of pixels.
                float y = Mathf.Round(Mathf.Lerp(r.yMin, r.yMax - 1f, i / (float)rows)) + 0.5f;
                p.MoveTo(new Vector2(r.xMin, y));
                p.LineTo(new Vector2(r.xMax, y));
            }
            p.Stroke();
        }

        public static void HLine(Painter2D p, Rect r, float y, Color color)
        {
            p.lineWidth = 1f;
            p.lineCap = LineCap.Butt;
            p.strokeColor = color;
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, Mathf.Round(y) + 0.5f));
            p.LineTo(new Vector2(r.xMax, Mathf.Round(y) + 0.5f));
            p.Stroke();
        }

        public static void Polyline(Painter2D p, IList<Vector2> pts, Color color, float width)
        {
            if (pts == null || pts.Count < 2)
            {
                return;
            }
            p.lineWidth = width;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            p.strokeColor = color;
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Count; i++)
            {
                p.LineTo(pts[i]);
            }
            p.Stroke();
        }

        public static void FillRect(Painter2D p, float x0, float y0, float x1, float y1, Color fill)
        {
            if (x1 - x0 <= 0f || y1 - y0 <= 0f)
            {
                return;
            }
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(new Vector2(x0, y0));
            p.LineTo(new Vector2(x1, y0));
            p.LineTo(new Vector2(x1, y1));
            p.LineTo(new Vector2(x0, y1));
            p.ClosePath();
            p.Fill(FillRule.NonZero);
        }

        public static void Disc(Painter2D p, Vector2 center, float radius, Color fill)
        {
            if (radius <= 0.5f)
            {
                return;
            }
            p.fillColor = fill;
            p.BeginPath();
            const int steps = 24;
            for (int i = 0; i < steps; i++)
            {
                var v = OnCircle(center, radius, i / (float)steps * 360f);
                if (i == 0)
                {
                    p.MoveTo(v);
                }
                else
                {
                    p.LineTo(v);
                }
            }
            p.ClosePath();
            p.Fill(FillRule.NonZero);
        }

        /// <summary>Full ring drawn as a stroked circle — avoids the seam a 360° polygon would leave.</summary>
        public static void RingStroke(Painter2D p, Vector2 center, float radius, float thickness, Color color)
        {
            if (radius <= 0.5f || thickness <= 0f)
            {
                return;
            }
            p.lineWidth = thickness;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Butt;
            p.strokeColor = color;
            p.BeginPath();
            const int steps = 64;
            for (int i = 0; i < steps; i++)
            {
                var v = OnCircle(center, radius, i / (float)steps * 360f);
                if (i == 0)
                {
                    p.MoveTo(v);
                }
                else
                {
                    p.LineTo(v);
                }
            }
            p.ClosePath();
            p.Stroke();
        }

        /// <summary>Annular sector (donut slice). Angles are degrees, 0° = 3 o'clock, growing clockwise.</summary>
        public static void RingSector(Painter2D p, Vector2 center, float inner, float outer, float fromDeg, float toDeg, Color fill)
        {
            float span = toDeg - fromDeg;
            if (outer <= inner || Mathf.Abs(span) < 0.05f)
            {
                return;
            }
            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(span) / 5f), 2, 180);
            p.fillColor = fill;
            p.BeginPath();
            for (int i = 0; i <= steps; i++)
            {
                var v = OnCircle(center, outer, Mathf.Lerp(fromDeg, toDeg, i / (float)steps));
                if (i == 0)
                {
                    p.MoveTo(v);
                }
                else
                {
                    p.LineTo(v);
                }
            }
            for (int i = steps; i >= 0; i--)
            {
                p.LineTo(OnCircle(center, inner, Mathf.Lerp(fromDeg, toDeg, i / (float)steps)));
            }
            p.ClosePath();
            p.Fill(FillRule.NonZero);
        }

        public static Vector2 OnCircle(Vector2 center, float radius, float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            // UI space has y growing downwards, so a growing angle reads as clockwise on screen.
            return new Vector2(center.x + Mathf.Cos(rad) * radius, center.y + Mathf.Sin(rad) * radius);
        }
    }

    /// <summary>
    /// Compact trend line (optionally area-filled) for "last N values" strips. With no data it still
    /// paints a pale grid plus a caption, so a row never collapses into blank space.
    /// </summary>
    public sealed class Sparkline : VisualElement
    {
        private readonly Label _empty;
        private float[] _values = Array.Empty<float>();
        private Color _accent = ChartPalette.Accent;
        private bool _area = true;

        public Sparkline(float height = 48f)
        {
            AddToClassList("sc-spark");
            style.height = height;

            _empty = ChartParts.Overlay(this, "No data yet");

            generateVisualContent += PaintChart;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        public Sparkline SetData(IReadOnlyList<float> values)
        {
            _values = ChartData.Sanitize(values);
            _empty.style.display = _values.Length == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            MarkDirtyRepaint();
            return this;
        }

        public Sparkline SetAccent(Color c)
        {
            _accent = c;
            MarkDirtyRepaint();
            return this;
        }

        public Sparkline SetArea(bool fill)
        {
            _area = fill;
            MarkDirtyRepaint();
            return this;
        }

        public Sparkline SetEmptyText(string text)
        {
            _empty.text = text ?? string.Empty;
            return this;
        }

        private void PaintChart(MeshGenerationContext ctx)
        {
            // contentRect is only meaningful here: in the constructor the layout has not run yet.
            var r = contentRect;
            if (!ChartPaint.Usable(r))
            {
                return;
            }

            var p = ctx.painter2D;
            ChartPaint.Grid(p, r, 3);
            if (_values.Length == 0)
            {
                return;
            }

            var pts = BuildPoints(r);
            if (_area && pts.Length >= 2)
            {
                p.fillColor = new Color(_accent.r, _accent.g, _accent.b, 0.18f);
                p.BeginPath();
                p.MoveTo(new Vector2(pts[0].x, r.yMax));
                for (int i = 0; i < pts.Length; i++)
                {
                    p.LineTo(pts[i]);
                }
                p.LineTo(new Vector2(pts[pts.Length - 1].x, r.yMax));
                p.ClosePath();
                p.Fill(FillRule.NonZero);
            }

            ChartPaint.Polyline(p, pts, _accent, 2f);
            ChartPaint.Disc(p, pts[pts.Length - 1], 3f, _accent);
        }

        private Vector2[] BuildPoints(Rect r)
        {
            int n = _values.Length;
            float min = _values[0];
            float max = _values[0];
            for (int i = 1; i < n; i++)
            {
                if (_values[i] < min)
                {
                    min = _values[i];
                }
                if (_values[i] > max)
                {
                    max = _values[i];
                }
            }

            const float pad = 4f;
            float top = r.yMin + pad;
            float bottom = r.yMax - pad;
            if (bottom - top < 2f)
            {
                top = r.yMin;
                bottom = r.yMax;
            }
            float h = bottom - top;
            float left = r.xMin + 1f;
            float right = r.xMax - 1f;

            // A single sample, or a perfectly flat series, becomes a centered horizontal rule
            // instead of a division by zero.
            float range = max - min;
            if (n == 1 || range <= 0.000001f)
            {
                float mid = top + h * 0.5f;
                return new[] { new Vector2(left, mid), new Vector2(right, mid) };
            }

            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float x = Mathf.Lerp(left, right, i / (float)(n - 1));
                float y = bottom - (_values[i] - min) / range * h;
                pts[i] = new Vector2(x, y);
            }
            return pts;
        }
    }

    /// <summary>
    /// Vertical bars with value and category labels. The labels are ordinary flex columns that share
    /// the width evenly, which is exactly how the painter slices the plot — so text and bars line up
    /// without hard-coded offsets. Negative values are supported (a zero baseline appears).
    /// </summary>
    public sealed class BarChart : VisualElement
    {
        // Past this many bars the per-bar value labels turn into unreadable soup, so they are dropped.
        private const int MaxValueLabels = 14;

        private readonly VisualElement _valuesRow;
        private readonly VisualElement _plot;
        private readonly VisualElement _labelsRow;
        private readonly Label _empty;

        private ChartPoint[] _points = Array.Empty<ChartPoint>();
        private Color _accent = ChartPalette.Accent;
        private Func<float, string> _fmt = ChartFormat.Number;

        public BarChart(float height = 160f)
        {
            AddToClassList("sc-bars");
            style.height = height;

            _valuesRow = new VisualElement();
            _valuesRow.AddToClassList("sc-bars__row");
            _valuesRow.AddToClassList("sc-bars__values");
            _valuesRow.style.display = DisplayStyle.None;
            Add(_valuesRow);

            _plot = new VisualElement();
            _plot.AddToClassList("sc-bars__plot");
            Add(_plot);

            _empty = ChartParts.Overlay(_plot, "No data yet");

            _labelsRow = new VisualElement();
            _labelsRow.AddToClassList("sc-bars__row");
            _labelsRow.AddToClassList("sc-bars__labels");
            _labelsRow.style.display = DisplayStyle.None;
            Add(_labelsRow);

            _plot.generateVisualContent += PaintChart;
            _plot.RegisterCallback<GeometryChangedEvent>(_ => _plot.MarkDirtyRepaint());
        }

        public BarChart SetData(IReadOnlyList<ChartPoint> points)
        {
            _points = ChartData.Sanitize(points);
            Rebuild();
            _plot.MarkDirtyRepaint();
            return this;
        }

        /// <summary>Color for bars that carry no <see cref="ChartPoint.Color"/> of their own.</summary>
        public BarChart SetAccent(Color c)
        {
            _accent = c;
            _plot.MarkDirtyRepaint();
            return this;
        }

        public BarChart SetValueFormatter(Func<float, string> fmt)
        {
            _fmt = fmt ?? ChartFormat.Number;
            Rebuild();
            return this;
        }

        public BarChart SetEmptyText(string text)
        {
            _empty.text = text ?? string.Empty;
            return this;
        }

        private void Rebuild()
        {
            _valuesRow.Clear();
            _labelsRow.Clear();

            bool has = _points.Length > 0;
            bool showValues = has && _points.Length <= MaxValueLabels;
            _empty.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;
            _labelsRow.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
            _valuesRow.style.display = showValues ? DisplayStyle.Flex : DisplayStyle.None;
            if (!has)
            {
                return;
            }

            for (int i = 0; i < _points.Length; i++)
            {
                var pt = _points[i];
                if (showValues)
                {
                    _valuesRow.Add(Cell(SafeFormat(pt.Value), "sc-bars__vlabel"));
                }
                _labelsRow.Add(Cell(Fmt.Truncate(pt.Label ?? string.Empty, 10), "sc-bars__xlabel"));
            }
        }

        private string SafeFormat(float v)
        {
            try
            {
                return _fmt(v) ?? string.Empty;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] BarChart value formatter threw: " + e.Message);
                return ChartFormat.Number(v);
            }
        }

        private static Label Cell(string text, string modifier)
        {
            var l = new Label(text);
            l.AddToClassList("sc-chart__col");
            l.AddToClassList(modifier);
            return l;
        }

        private void PaintChart(MeshGenerationContext ctx)
        {
            var r = _plot.contentRect;
            if (!ChartPaint.Usable(r))
            {
                return;
            }

            var p = ctx.painter2D;
            ChartPaint.Grid(p, r, 4);

            int n = _points.Length;
            if (n == 0)
            {
                return;
            }

            // The scale always contains zero, otherwise bar lengths would lie about ratios.
            float max = 0f;
            float min = 0f;
            for (int i = 0; i < n; i++)
            {
                float v = _points[i].Value;
                if (v > max)
                {
                    max = v;
                }
                if (v < min)
                {
                    min = v;
                }
            }
            float range = max - min;
            if (range <= 0.000001f)
            {
                range = 1f;
            }

            float baseline = r.yMax - (0f - min) / range * r.height;
            float slot = r.width / n;
            float barWidth = Mathf.Clamp(slot * 0.62f, 1f, 54f);

            for (int i = 0; i < n; i++)
            {
                var pt = _points[i];
                float cx = r.xMin + slot * (i + 0.5f);
                float valueY = r.yMax - (pt.Value - min) / range * r.height;

                float y0 = Mathf.Min(valueY, baseline);
                float y1 = Mathf.Max(valueY, baseline);
                if (y1 - y0 < 2f)
                {
                    // Zero-ish bars still get a 2px stub on the baseline, so the category never
                    // looks like it is missing from the chart.
                    float mid = Mathf.Clamp((y0 + y1) * 0.5f, r.yMin + 1f, r.yMax - 1f);
                    y0 = mid - 1f;
                    y1 = mid + 1f;
                }
                else
                {
                    y0 = Mathf.Max(y0, r.yMin);
                    y1 = Mathf.Min(y1, r.yMax);
                }

                ChartPaint.FillRect(p, cx - barWidth * 0.5f, y0, cx + barWidth * 0.5f, y1, pt.Color ?? _accent);
            }

            if (min < 0f && max > 0f)
            {
                ChartPaint.HLine(p, r, baseline, ChartPalette.Axis);
            }
        }
    }

    /// <summary>
    /// Ring chart with a center caption and a color-dot legend. Only positive values can be parts of
    /// a whole, so non-positive points are dropped; when nothing survives, a pale ring plus a caption
    /// is drawn instead of an empty box.
    /// </summary>
    public sealed class DonutChart : VisualElement
    {
        private readonly VisualElement _canvas;
        private readonly VisualElement _legend;
        private readonly Label _value;
        private readonly Label _caption;
        private readonly Label _empty;

        private ChartPoint[] _slices = Array.Empty<ChartPoint>();
        private float _total;
        private string _centerValue;
        private string _centerCaption;

        public DonutChart(float size = 160f)
        {
            AddToClassList("sc-donut");

            _canvas = new VisualElement();
            _canvas.AddToClassList("sc-donut__canvas");
            _canvas.style.width = size;
            _canvas.style.height = size;
            Add(_canvas);

            var center = new VisualElement();
            center.AddToClassList("sc-donut__center");
            center.pickingMode = PickingMode.Ignore;

            _value = new Label();
            _value.AddToClassList("sc-donut__value");
            _value.pickingMode = PickingMode.Ignore;
            _value.style.display = DisplayStyle.None;
            center.Add(_value);

            _caption = new Label();
            _caption.AddToClassList("sc-donut__caption");
            _caption.pickingMode = PickingMode.Ignore;
            _caption.style.display = DisplayStyle.None;
            center.Add(_caption);

            _empty = new Label("No data yet");
            _empty.AddToClassList("sc-chart__empty");
            _empty.pickingMode = PickingMode.Ignore;
            center.Add(_empty);

            _canvas.Add(center);

            _legend = new VisualElement();
            _legend.AddToClassList("sc-donut__legend");
            _legend.style.display = DisplayStyle.None;
            Add(_legend);

            _canvas.generateVisualContent += PaintChart;
            _canvas.RegisterCallback<GeometryChangedEvent>(_ => _canvas.MarkDirtyRepaint());
        }

        public DonutChart SetData(IReadOnlyList<ChartPoint> points)
        {
            var all = ChartData.Sanitize(points);
            var kept = new List<ChartPoint>(all.Length);
            _total = 0f;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Value <= 0f)
                {
                    continue;
                }
                kept.Add(all[i]);
                _total += all[i].Value;
            }
            _slices = kept.ToArray();

            RebuildLegend();
            ApplyCenter();
            _canvas.MarkDirtyRepaint();
            return this;
        }

        /// <summary>Big value + small caption inside the ring (hidden while the chart has no data).</summary>
        public DonutChart SetCenter(string value, string caption)
        {
            _centerValue = value;
            _centerCaption = caption;
            ApplyCenter();
            return this;
        }

        public DonutChart SetEmptyText(string text)
        {
            _empty.text = text ?? string.Empty;
            return this;
        }

        private void ApplyCenter()
        {
            bool has = _slices.Length > 0 && _total > 0f;
            _empty.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;
            _value.text = _centerValue ?? string.Empty;
            _caption.text = _centerCaption ?? string.Empty;
            _value.style.display = has && !string.IsNullOrEmpty(_centerValue) ? DisplayStyle.Flex : DisplayStyle.None;
            _caption.style.display = has && !string.IsNullOrEmpty(_centerCaption) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RebuildLegend()
        {
            _legend.Clear();
            _legend.style.display = _slices.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            for (int i = 0; i < _slices.Length; i++)
            {
                var s = _slices[i];

                var row = new VisualElement();
                row.AddToClassList("sc-donut__legend-row");

                var dot = new VisualElement();
                dot.AddToClassList("sc-donut__dot");
                dot.style.backgroundColor = ColorOf(i);
                row.Add(dot);

                string name = string.IsNullOrEmpty(s.Label) ? "Item " + (i + 1) : Fmt.Truncate(s.Label, 22);
                var nameLabel = new Label(name);
                nameLabel.AddToClassList("sc-donut__legend-name");
                row.Add(nameLabel);

                float share = _total <= 0f ? 0f : s.Value / _total;
                var valueLabel = new Label(ChartFormat.Number(s.Value) + "   " + Fmt.Percent(share));
                valueLabel.AddToClassList("sc-donut__legend-val");
                row.Add(valueLabel);

                _legend.Add(row);
            }
        }

        private Color ColorOf(int index)
        {
            return _slices[index].Color ?? ChartPalette.At(index);
        }

        private void PaintChart(MeshGenerationContext ctx)
        {
            var r = _canvas.contentRect;
            if (!ChartPaint.Usable(r))
            {
                return;
            }

            var p = ctx.painter2D;
            float d = Mathf.Min(r.width, r.height);
            var center = r.center;
            float outer = d * 0.5f - 2f;
            if (outer <= 3f)
            {
                return;
            }
            float thickness = Mathf.Clamp(d * 0.22f, 4f, outer - 1f);
            float inner = Mathf.Max(1f, outer - thickness);

            if (_slices.Length == 0 || _total <= 0f)
            {
                ChartPaint.RingStroke(p, center, (outer + inner) * 0.5f, thickness, ChartPalette.Grid);
                return;
            }

            if (_slices.Length == 1)
            {
                // A single 360° polygon would leave a visible seam — stroke the full ring instead.
                ChartPaint.RingStroke(p, center, (outer + inner) * 0.5f, thickness, ColorOf(0));
                return;
            }

            const float gap = 2f;
            float angle = -90f; // start at 12 o'clock
            for (int i = 0; i < _slices.Length; i++)
            {
                float sweep = _slices[i].Value / _total * 360f;
                float g = sweep > gap + 1f ? gap : 0f;
                ChartPaint.RingSector(p, center, inner, outer, angle + g * 0.5f, angle + sweep - g * 0.5f, ColorOf(i));
                angle += sweep;
            }
        }
    }
}
