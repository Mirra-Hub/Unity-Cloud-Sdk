using System;
using System.Globalization;
using MirraCloud.Json;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Display formatting shared by every view, so 22 services print sizes, counts, money and dates
    /// the same way. Everything goes through InvariantCulture on purpose: the showcase UI is English
    /// and must not drift with the player's machine locale. Unset input (default/MinValue dates,
    /// blank strings) renders as <see cref="Dash"/> rather than "0001-01-01" or an empty cell.
    /// </summary>
    public static class Fmt
    {
        /// <summary>The "no value" placeholder used across the UI — an em dash, never an empty cell.</summary>
        public const string Dash = "—";

        private static readonly string[] ByteUnits = { "B", "KB", "MB", "GB", "TB" };
        private static readonly string[] CompactUnits = { "k", "M", "B", "T" };

        public static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s ?? string.Empty;
            }
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        /// <summary>Stringify a dynamic JsonValue for table display (branch on Type; containers show a count).</summary>
        public static string Json(JsonValue v)
        {
            if (v == null)
            {
                return "null";
            }
            switch (v.Type)
            {
                case JsonValueType.Null: return "null";
                case JsonValueType.String: return (string)v;
                case JsonValueType.Boolean: return ((bool)v).ToString();
                case JsonValueType.Int: return ((int)v).ToString();
                case JsonValueType.Double: return ((double)v).ToString(CultureInfo.InvariantCulture);
                case JsonValueType.Object: return "{ " + v.Count + " keys }";
                case JsonValueType.Array: return "[ " + v.Count + " items ]";
                default: return string.Empty;
            }
        }

        /// <summary>Binary file size: "0 B" / "1.4 KB" / "3.2 MB". 1024-based, whole bytes, one decimal above.</summary>
        public static string Bytes(long bytes)
        {
            if (bytes <= 0L)
            {
                return "0 B";
            }
            double v = bytes;
            int i = 0;
            while (v >= 1024d && i < ByteUnits.Length - 1)
            {
                v /= 1024d;
                i++;
            }
            string n = i == 0
                ? v.ToString("0", CultureInfo.InvariantCulture)
                : v.ToString("0.#", CultureInfo.InvariantCulture);
            return n + " " + ByteUnits[i];
        }

        /// <summary>Human count: grouped below 10 000 ("0", "1 240"), compact above ("12.4k", "3.4M").
        /// NaN/Infinity (an empty average, a divide-by-zero rate) render as a dash instead of "NaN".</summary>
        public static string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return Dash;
            }
            double abs = Math.Abs(value);
            if (abs < 10000d)
            {
                return Grouped(value);
            }

            double scaled = abs;
            int unit = -1;
            while (scaled >= 1000d && unit < CompactUnits.Length - 1)
            {
                scaled /= 1000d;
                unit++;
            }
            // 999_990 divides to 999.99 and would print as "1000k" — promote to the next unit instead
            if (unit < CompactUnits.Length - 1 && Math.Round(scaled, 1) >= 1000d)
            {
                scaled /= 1000d;
                unit++;
            }
            string sign = value < 0d ? "-" : string.Empty;
            return sign + scaled.ToString("0.#", CultureInfo.InvariantCulture) + CompactUnits[unit];
        }

        /// <summary>Price or balance: "4.99 USD" / "1 240 gold". The currency is appended as given
        /// (it can be an ISO code or a soft-currency id), omitted when blank.</summary>
        public static string Money(decimal amount, string currency = null)
        {
            string n = Grouped(amount);
            return string.IsNullOrWhiteSpace(currency) ? n : n + " " + currency.Trim();
        }

        /// <summary>Local calendar day, "2026-07-26" (the format every view already prints).</summary>
        public static string Date(DateTime t)
        {
            return IsUnset(t) ? Dash : t.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>Nullable overload — a missing timestamp is a dash, not an exception.</summary>
        public static string Date(DateTime? t)
        {
            return t.HasValue ? Date(t.Value) : Dash;
        }

        /// <summary>Local day + minute, "2026-07-26 14:03". Named with a "2" because a member cannot
        /// shadow the <see cref="System.DateTime"/> type used in this class' own signatures.</summary>
        public static string DateTime2(DateTime t)
        {
            return IsUnset(t) ? Dash : t.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>Nullable overload of <see cref="DateTime2(System.DateTime)"/>.</summary>
        public static string DateTime2(DateTime? t)
        {
            return t.HasValue ? DateTime2(t.Value) : Dash;
        }

        /// <summary>Local wall clock only, "14:03" — for rows already grouped under a date.</summary>
        public static string Time(DateTime t)
        {
            return IsUnset(t) ? Dash : t.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>Nullable overload of <see cref="Time(System.DateTime)"/>.</summary>
        public static string Time(DateTime? t)
        {
            return t.HasValue ? Time(t.Value) : Dash;
        }

        /// <summary>Coarse span, two units at most: "3d 4h" / "2h 15m" / "12m 30s" / "8s" / "420ms".
        /// Matches the countdown chip's wording; negative spans keep a leading "-".</summary>
        public static string Duration(TimeSpan span)
        {
            long ticks = span.Ticks;
            string sign = ticks < 0L ? "-" : string.Empty;
            // -Ticks overflows for TimeSpan.MinValue, so clamp that one case instead of negating it
            TimeSpan s = ticks == long.MinValue ? TimeSpan.MaxValue : new TimeSpan(ticks < 0L ? -ticks : ticks);

            if (ticks == 0L)
            {
                return "0s";
            }
            if (s.TotalDays >= 1d)
            {
                return sign + (int)s.TotalDays + "d " + s.Hours + "h";
            }
            if (s.TotalHours >= 1d)
            {
                return sign + s.Hours + "h " + s.Minutes + "m";
            }
            if (s.TotalMinutes >= 1d)
            {
                return sign + s.Minutes + "m " + s.Seconds + "s";
            }
            if (s.TotalSeconds >= 1d)
            {
                return sign + s.Seconds + "s";
            }
            return sign + (int)s.TotalMilliseconds + "ms";
        }

        /// <summary>A 0..1 ratio as "42%". Values above 1 are kept (overflowing progress reads better
        /// as "120%" than as a silent clamp); NaN/Infinity render as a dash.</summary>
        public static string Percent(float ratio01)
        {
            if (float.IsNaN(ratio01) || float.IsInfinity(ratio01))
            {
                return Dash;
            }
            double p = Math.Round(ratio01 * 100d, MidpointRounding.AwayFromZero);
            return p.ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>Head of a long opaque id: "6a3ae8ec…". Ids shorter than <paramref name="keep"/>
        /// are shown whole; blank ones become a dash.</summary>
        public static string Id(string id, int keep = 8)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Dash;
            }
            int cut = keep < 0 ? 0 : keep;
            return id.Length <= cut ? id : id.Substring(0, cut) + "…";
        }

        /// <summary>The string, or a dash when it is null/blank — the guard every table cell needs.</summary>
        public static string OrDash(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? Dash : s;
        }

        /// <summary>InvariantCulture groups with ",", but the UI uses a space: it reads better in
        /// narrow numeric cells and never looks like a decimal point.</summary>
        private static string Grouped(double value)
        {
            return value.ToString("#,0.##", CultureInfo.InvariantCulture).Replace(",", " ");
        }

        private static string Grouped(decimal value)
        {
            return value.ToString("#,0.##", CultureInfo.InvariantCulture).Replace(",", " ");
        }

        /// <summary>default(DateTime) equals DateTime.MinValue and "==" ignores Kind, so a zero-tick
        /// UTC stamp is caught too; MaxValue means "never" and is not worth printing as 9999-12-31.</summary>
        private static bool IsUnset(DateTime t)
        {
            return t.Ticks == 0L || t == System.DateTime.MaxValue;
        }
    }
}
