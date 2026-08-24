using UnityEngine;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// The showcase palette in C#. USS styles everything it can, but code-painted pixels
    /// (Painter2D charts, inline tints, accent-driven chips and cards) need the very same values —
    /// this is the single source for them, so no view hand-rolls another hex literal.
    /// </summary>
    public static class ShowcaseTheme
    {
        public static readonly Color Bg = new Color32(0x0E, 0x11, 0x16, 0xFF);           // #0E1116 screen background
        public static readonly Color Surface = new Color32(0x16, 0x1A, 0x22, 0xFF);      // #161A22 card
        public static readonly Color Surface2 = new Color32(0x1B, 0x20, 0x29, 0xFF);     // #1B2029 row / nested surface
        public static readonly Color Border = new Color32(0x23, 0x2A, 0x36, 0xFF);       // #232A36 hairline
        public static readonly Color BorderStrong = new Color32(0x2E, 0x37, 0x46, 0xFF); // #2E3746 emphasized edge
        public static readonly Color Text = new Color32(0xE6, 0xEA, 0xF2, 0xFF);         // #E6EAF2 primary text
        public static readonly Color TextMuted = new Color32(0x9A, 0xA4, 0xB8, 0xFF);    // #9AA4B8 secondary text
        public static readonly Color TextDim = new Color32(0x6B, 0x74, 0x88, 0xFF);      // #6B7488 captions / axis labels
        public static readonly Color Accent = new Color32(0x25, 0x63, 0xEB, 0xFF);       // #2563EB primary action
        public static readonly Color AccentHover = new Color32(0x1D, 0x4E, 0xD8, 0xFF);  // #1D4ED8 primary action, hovered
        public static readonly Color AccentSoft = new Color32(0x4D, 0x8D, 0xFF, 0xFF);   // #4D8DFF accent text on dark
        public static readonly Color Focus = new Color32(0x3B, 0x82, 0xF6, 0xFF);        // #3B82F6 focus ring
        public static readonly Color Ok = new Color32(0x22, 0xC5, 0x5E, 0xFF);           // #22C55E success
        public static readonly Color Warn = new Color32(0xF5, 0x9E, 0x0B, 0xFF);         // #F59E0B warning
        public static readonly Color Bad = new Color32(0xEF, 0x44, 0x44, 0xFF);          // #EF4444 error
        public static readonly Color Info = new Color32(0x38, 0xBD, 0xF8, 0xFF);         // #38BDF8 informational
        public static readonly Color Violet = new Color32(0xA7, 0x8B, 0xFA, 0xFF);       // #A78BFA sixth chart series

        /// <summary>Categorical chart palette, ordered so neighbours (including the wrap from the last
        /// back to the first) stay distinguishable. Six entries: past that, series repeat.</summary>
        // Declared last on purpose: static field initializers run top-down, so the colors above must
        // already be assigned when this array is built.
        // Two rules behind this order: Bad (#EF4444) is absent because red reads as "error"
        // everywhere else in the showcase, and the two blues sit apart — side by side in a donut
        // legend, Accent and AccentSoft are hard to tell apart.
        public static readonly Color[] Series = { Accent, Ok, Warn, Violet, Info, AccentSoft };

        /// <summary>Grid lines and empty-state rings: visible, but never mistaken for data.</summary>
        public static readonly Color ChartGrid = new Color(0.60f, 0.64f, 0.75f, 0.18f);

        /// <summary>The zero baseline, drawn only when a series actually crosses it.</summary>
        public static readonly Color ChartAxis = new Color(0.60f, 0.64f, 0.75f, 0.38f);

        /// <summary>Foreground color of a <see cref="ChipTone"/>, for code that tints outside a chip
        /// (bars, dots, borders). Accent maps to AccentSoft because pure #2563EB is unreadable as text
        /// on the dark surface — that is also what the .sc-chip--accent label uses.</summary>
        public static Color Tone(ChipTone tone)
        {
            switch (tone)
            {
                case ChipTone.Accent: return AccentSoft;
                case ChipTone.Ok: return Ok;
                case ChipTone.Warn: return Warn;
                case ChipTone.Bad: return Bad;
                case ChipTone.Info: return Info;
                default: return TextMuted;
            }
        }

        /// <summary>Series color by index, wrapping — chart code never has to bounds-check.</summary>
        public static Color SeriesAt(int index)
        {
            int n = Series.Length;
            int i = index % n;
            if (i < 0)
            {
                // C# keeps the sign of the dividend, so a negative index needs one wrap back up
                i += n;
            }
            return Series[i];
        }

        /// <summary>The same color at another opacity — soft fills use 0.12–0.18, matching chips and cards.</summary>
        public static Color Alpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
        }
    }
}
