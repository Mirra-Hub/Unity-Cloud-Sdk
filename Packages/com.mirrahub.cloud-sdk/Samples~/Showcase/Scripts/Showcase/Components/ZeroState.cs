using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Zero-data layouts. A service with nothing to show keeps its real shape — the actual table
    /// header, a card grid — filled with pale ghosts, plus one line telling the reader how data
    /// gets there. Also keeps "nothing yet", "not set up" and "no access" visually distinct, since
    /// they mean completely different things to someone evaluating the SDK.
    /// </summary>
    public static class ZeroState
    {
        // Deterministic widths so the ghosts look like text of varying length instead of a grid.
        private static readonly float[] BarWidths = { 72f, 54f, 88f, 46f, 64f, 80f, 58f };

        /// <summary>
        /// The table the service *would* render: its real header, then <paramref name="ghostRows"/>
        /// fading placeholder rows, then the message and an optional call to action.
        /// </summary>
        public static VisualElement Table(DataColumn[] columns, string message, int ghostRows = 3,
            string ctaText = null, Action onCta = null)
        {
            var cols = columns;
            if (cols == null || cols.Length == 0)
            {
                Debug.LogWarning("[Showcase] ZeroState.Table got no columns — falling back to a single one.");
                cols = new[] { new DataColumn { Header = "DATA", Grow = 1f } };
            }

            var root = new VisualElement();
            root.AddToClassList("sc-zero");

            // A real DataTable draws the header with the exact geometry the populated table would
            // have, and already knows how to render dimmed rows plus the caption underneath — this
            // layer only adds the call to action, which the table has no opinion about.
            var table = new DataTable(cols);
            table.BindEmpty(message, Mathf.Clamp(ghostRows, 0, 12));
            root.Add(table);

            AppendCta(root, ctaText, onCta);
            return root;
        }

        /// <summary>Grid of pale placeholder cards — the card-based counterpart of <see cref="Table"/>.</summary>
        public static VisualElement Cards(string glyph, string message, int ghosts = 3,
            string ctaText = null, Action onCta = null)
        {
            var root = new VisualElement();
            root.AddToClassList("sc-zero");

            var grid = new VisualElement();
            grid.AddToClassList("sc-zero__cards");
            int count = Mathf.Clamp(ghosts, 0, 12);
            for (int i = 0; i < count; i++)
            {
                grid.Add(GhostCard(glyph, i));
            }
            root.Add(grid);

            root.Add(Caption(message));
            AppendCta(root, ctaText, onCta);
            return root;
        }

        /// <summary>
        /// Explanatory panel for screens with no table or grid to imitate: glyph, headline, a line
        /// about how data shows up here, an optional action and an optional footnote.
        /// </summary>
        public static VisualElement Panel(string glyph, string title, string message,
            string ctaText = null, Action onCta = null, string hint = null)
        {
            return BuildPanel(glyph, title, message, ctaText, onCta, hint, null);
        }

        /// <summary>
        /// "The project has no such configuration" — deliberately different from "no data yet",
        /// because the fix is in the Mirra Hub console rather than in the game.
        /// </summary>
        public static VisualElement NotConfigured(string serviceName, string hint = null)
        {
            string name = string.IsNullOrEmpty(serviceName) ? "This service" : serviceName;
            return BuildPanel(
                LucideIcon.SlidersHorizontal,
                name + " is not set up yet",
                "Nothing has been configured for " + name + " in this project. Create it in the Mirra Hub "
                + "console and it will show up here the next time you open this screen.",
                null,
                null,
                hint ?? ("Open the Mirra Hub console, pick this project, then open " + name + "."),
                "sc-zero__panel--warn");
        }

        /// <summary>Access denied (403) — the request was understood, this player just may not see it.</summary>
        public static VisualElement Forbidden(string message = null)
        {
            return BuildPanel(
                LucideIcon.Lock,
                "You do not have access",
                string.IsNullOrEmpty(message)
                    ? "This player is not allowed to see this data. Ask a project admin to grant access, "
                      + "then open this screen again."
                    : message,
                null,
                null,
                null,
                "sc-zero__panel--bad");
        }

        private static VisualElement BuildPanel(string glyph, string title, string message,
            string ctaText, Action onCta, string hint, string toneClass)
        {
            var panel = new VisualElement();
            panel.AddToClassList("sc-zero");
            panel.AddToClassList("sc-zero__panel");
            if (!string.IsNullOrEmpty(toneClass))
            {
                panel.AddToClassList(toneClass);
            }

            if (!string.IsNullOrEmpty(glyph))
            {
                var g = new Label(glyph);
                g.AddToClassList("sc-zero__glyph");
                g.AddToClassList("sc-icon");
                panel.Add(g);
            }

            if (!string.IsNullOrEmpty(title))
            {
                var t = new Label(title);
                t.enableRichText = false;
                t.AddToClassList("sc-zero__title");
                panel.Add(t);
            }

            if (!string.IsNullOrEmpty(message))
            {
                panel.Add(Caption(message));
            }

            AppendCta(panel, ctaText, onCta);

            if (!string.IsNullOrEmpty(hint))
            {
                panel.Add(Hint(hint));
            }

            return panel;
        }

        private static VisualElement GhostCard(string glyph, int index)
        {
            var card = new VisualElement();
            card.AddToClassList("sc-zero__card");
            card.pickingMode = PickingMode.Ignore;
            card.style.opacity = Mathf.Max(0.2f, 0.7f - index * 0.16f);

            if (!string.IsNullOrEmpty(glyph))
            {
                var g = new Label(glyph);
                g.AddToClassList("sc-zero__card-glyph");
                g.AddToClassList("sc-icon");
                card.Add(g);
            }

            var wide = new VisualElement();
            wide.AddToClassList("sc-zero__bar");
            wide.style.width = Length.Percent(BarWidths[index % BarWidths.Length]);
            card.Add(wide);

            var narrow = new VisualElement();
            narrow.AddToClassList("sc-zero__bar");
            narrow.AddToClassList("sc-zero__bar--thin");
            narrow.style.width = Length.Percent(BarWidths[(index + 3) % BarWidths.Length] * 0.6f);
            card.Add(narrow);

            return card;
        }

        private static Label Caption(string message)
        {
            var l = new Label(message ?? string.Empty);
            l.enableRichText = false;
            l.AddToClassList("sc-zero__msg");
            return l;
        }

        private static VisualElement Hint(string text)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-zero__hint");

            var g = new Label(LucideIcon.Info);
            g.AddToClassList("sc-zero__hint-glyph");
            g.AddToClassList("sc-icon");
            row.Add(g);

            var l = new Label(text);
            l.enableRichText = false;
            l.AddToClassList("sc-zero__hint-text");
            row.Add(l);

            return row;
        }

        private static void AppendCta(VisualElement root, string ctaText, Action onCta)
        {
            if (string.IsNullOrEmpty(ctaText))
            {
                return;
            }
            if (onCta == null)
            {
                // A button that does nothing is worse than no button at all.
                Debug.LogWarning("[Showcase] ZeroState CTA \"" + ctaText + "\" has no action — skipping it.");
                return;
            }

            var b = new Button(() => onCta()) { text = ctaText };
            b.AddToClassList("sc-btn");
            b.AddToClassList("sc-btn--primary");
            b.AddToClassList("sc-zero__cta");
            root.Add(b);
        }
    }
}
