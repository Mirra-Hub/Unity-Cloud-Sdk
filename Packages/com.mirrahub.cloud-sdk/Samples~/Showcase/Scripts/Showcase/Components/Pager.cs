using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Page navigator for tables and lists: first/prev/next/last plus an "N–M of K" caption.
    /// It never owns the data — it raises <see cref="PageRequested"/> and the view re-queries the
    /// SDK (or re-slices a local list with <see cref="Slice{T}"/>). Hides itself while everything
    /// fits on a single page, so a view can add it unconditionally.
    /// </summary>
    public sealed class Pager : VisualElement
    {
        private readonly Button _first;
        private readonly Button _prev;
        private readonly Button _next;
        private readonly Button _last;
        private readonly Label _label;
        private int _total;

        public Pager(int pageSize = 25)
        {
            PageSize = pageSize <= 0 ? 25 : pageSize;
            Page = 1;
            TotalPages = 1;

            AddToClassList("sc-pager");

            _first = MakeButton(LucideIcon.ChevronsLeft, () => Go(1));
            _prev = MakeButton(LucideIcon.ChevronLeft, () => Go(Page - 1));

            _label = new Label("0 of 0");
            _label.AddToClassList("sc-pager__label");
            _label.enableRichText = false;

            _next = MakeButton(LucideIcon.ChevronRight, () => Go(Page + 1));
            _last = MakeButton(LucideIcon.ChevronsRight, () => Go(TotalPages));

            Add(_first);
            Add(_prev);
            Add(_label);
            Add(_next);
            Add(_last);

            Sync();
        }

        /// <summary>Rows per page (fixed at construction).</summary>
        public int PageSize { get; }

        /// <summary>Current page, 1-based.</summary>
        public int Page { get; private set; }

        /// <summary>Page count for the last total set; always at least 1.</summary>
        public int TotalPages { get; private set; }

        /// <summary>Raised when the user picks another page. Never raised by <see cref="SetTotal"/>.</summary>
        public event Action<int> PageRequested;

        /// <summary>
        /// Feeds the pager the item count (and optionally the page to display). The page is clamped
        /// into range, which keeps the UI sane when the collection shrinks under it.
        /// </summary>
        public Pager SetTotal(int totalCount, int page = 1)
        {
            _total = totalCount < 0 ? 0 : totalCount;
            TotalPages = _total <= 0 ? 1 : (int)(((long)_total + PageSize - 1) / PageSize);
            Page = Clamp(page, 1, TotalPages);
            Sync();
            return this;
        }

        /// <summary>
        /// Cuts one page out of an in-memory list. Out-of-range pages are clamped (a page past the
        /// end yields the last page, not an empty view), so callers need no bounds checks.
        /// </summary>
        public static List<T> Slice<T>(IReadOnlyList<T> source, int page, int pageSize)
        {
            var result = new List<T>();
            if (source == null || source.Count == 0 || pageSize <= 0)
            {
                return result;
            }

            int pages = (source.Count + pageSize - 1) / pageSize;
            int p = Clamp(page, 1, pages);
            int start = (p - 1) * pageSize;
            int end = start + pageSize;
            if (end > source.Count)
            {
                end = source.Count;
            }
            for (int i = start; i < end; i++)
            {
                result.Add(source[i]);
            }
            return result;
        }

        private void Go(int page)
        {
            int target = Clamp(page, 1, TotalPages);
            if (target == Page)
            {
                return;
            }
            Page = target;
            Sync();
            PageRequested?.Invoke(Page);
        }

        private void Sync()
        {
            int from = _total <= 0 ? 0 : (Page - 1) * PageSize + 1;
            int to = _total <= 0 ? 0 : (int)Math.Min((long)Page * PageSize, _total);
            _label.text = from + "–" + to + " of " + _total;

            bool hasPrev = Page > 1;
            bool hasNext = Page < TotalPages;
            _first.SetEnabled(hasPrev);
            _prev.SetEnabled(hasPrev);
            _next.SetEnabled(hasNext);
            _last.SetEnabled(hasNext);

            style.display = TotalPages > 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static Button MakeButton(string glyph, Action action)
        {
            var b = new Button(action) { text = glyph };
            b.AddToClassList("sc-pager__btn");
            b.AddToClassList("sc-icon");
            return b;
        }

        private static int Clamp(int value, int lo, int hi)
        {
            if (value < lo)
            {
                return lo;
            }
            return value > hi ? hi : value;
        }
    }
}
