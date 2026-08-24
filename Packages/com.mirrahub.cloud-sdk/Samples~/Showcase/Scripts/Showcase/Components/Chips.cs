using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    public enum ChipTone { Neutral, Accent, Ok, Warn, Bad, Info }

    /// <summary>Colored pill for statuses / enums / segments / counts.</summary>
    public sealed class Chip : VisualElement
    {
        private readonly Label _label;

        public Chip(string text, ChipTone tone = ChipTone.Neutral)
        {
            AddToClassList("sc-chip");
            AddToClassList(ToneClass(tone));
            _label = new Label(text);
            _label.AddToClassList("sc-chip__label");
            Add(_label);
        }

        public Chip(string text, Color accent)
        {
            AddToClassList("sc-chip");
            style.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.16f);
            _label = new Label(text);
            _label.AddToClassList("sc-chip__label");
            _label.style.color = accent;
            Add(_label);
        }

        public Chip SetText(string text)
        {
            _label.text = text;
            return this;
        }

        private static string ToneClass(ChipTone tone)
        {
            switch (tone)
            {
                case ChipTone.Accent: return "sc-chip--accent";
                case ChipTone.Ok: return "sc-chip--ok";
                case ChipTone.Warn: return "sc-chip--warn";
                case ChipTone.Bad: return "sc-chip--bad";
                case ChipTone.Info: return "sc-chip--info";
                default: return "sc-chip--neutral";
            }
        }
    }

    /// <summary>A reward pill: kind glyph + "xCount" (shared by Economy / Daily / Leaderboard / Promo).</summary>
    public sealed class RewardChip : VisualElement
    {
        public RewardChip(string glyph, string countText, Color? accent = null)
        {
            AddToClassList("sc-reward-chip");
            var g = new Label(glyph);
            g.AddToClassList("sc-reward-chip__glyph");
            g.AddToClassList("sc-icon");
            if (accent.HasValue)
            {
                g.style.color = accent.Value;
            }
            var c = new Label(countText);
            c.AddToClassList("sc-reward-chip__count");
            Add(g);
            Add(c);
        }
    }

    /// <summary>Live "in 2d 4h" countdown pill driven by a UTC target (ticks once/second while attached).</summary>
    public sealed class CountdownChip : VisualElement
    {
        private readonly Label _label;
        private readonly DateTime? _target;
        private IVisualElementScheduledItem _tick;

        public CountdownChip(DateTime? targetUtc)
        {
            AddToClassList("sc-chip");
            AddToClassList("sc-chip--neutral");
            _label = new Label();
            _label.AddToClassList("sc-chip__label");
            Add(_label);
            _target = targetUtc;

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Render();
                _tick = schedule.Execute(Render).Every(1000);
            });
            RegisterCallback<DetachFromPanelEvent>(_ => _tick?.Pause());
        }

        private void Render()
        {
            if (_target == null)
            {
                _label.text = "—";
                return;
            }
            var span = _target.Value - DateTime.UtcNow;
            _label.text = span.TotalSeconds <= 0 ? "ended" : "in " + Humanize(span);
        }

        private static string Humanize(TimeSpan s)
        {
            if (s.TotalDays >= 1) return (int)s.TotalDays + "d " + s.Hours + "h";
            if (s.TotalHours >= 1) return s.Hours + "h " + s.Minutes + "m";
            if (s.TotalMinutes >= 1) return s.Minutes + "m " + s.Seconds + "s";
            return s.Seconds + "s";
        }
    }
}
