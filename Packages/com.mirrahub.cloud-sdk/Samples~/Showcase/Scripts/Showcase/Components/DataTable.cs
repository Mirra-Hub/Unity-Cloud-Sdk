using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>Column descriptor for <see cref="DataTable"/>. Cell builds a VisualElement from a row object.</summary>
    public sealed class DataColumn
    {
        public string Header;
        public Func<object, VisualElement> Cell;
        public float Grow = 1f;       // proportional width when FixedWidth == false
        public bool FixedWidth;
        public float Px;              // fixed pixel width when FixedWidth == true
        public string Align;          // null/"left" | "right" | "center"

        /// <summary>
        /// Sort key extracted from a row; leave null to keep the column unsortable (its header
        /// then stays inert and shows no arrow). Keys may be null — those rows sort first.
        /// </summary>
        public Func<object, IComparable> SortKey;
    }

    /// <summary>
    /// A ranked/columnar table (sticky header + scrollable rows). Plain ScrollView-based — robust
    /// for showcase-sized lists; swap to a virtualized ListView later if rows get large.
    /// Rows bound through <see cref="Bind"/> are kept, so sorting/zebra/row-click can re-render
    /// them without the view re-fetching anything.
    /// </summary>
    public sealed class DataTable : VisualElement
    {
        // Percent widths cycled through the ghost placeholders so a loading table looks like data.
        private static readonly float[] GhostWidths = { 72f, 46f, 88f, 58f, 36f, 78f };

        private readonly DataColumn[] _cols;
        private readonly ScrollView _body;
        private readonly Label[] _sortArrows;
        private readonly Label _note;
        private readonly List<object> _rows = new List<object>();

        private Func<object, bool> _highlight;
        private Action<object> _onRowClick;
        private bool _zebra;
        private int _sortColumn = -1;
        private bool _sortAscending = true;
        private bool _ghostMode;
        private string _noteMessage;
        private int _ghostRows = 3;

        public DataTable(DataColumn[] cols)
        {
            _cols = cols ?? Array.Empty<DataColumn>();
            _sortArrows = new Label[_cols.Length];
            AddToClassList("sc-table");

            var header = new VisualElement();
            header.AddToClassList("sc-table__header");
            for (int i = 0; i < _cols.Length; i++)
            {
                var c = _cols[i];
                // The header cell is a row container (text + sort arrow), not a bare Label — color,
                // font-size and font-style are inherited properties, so the existing hcell rule still applies.
                var h = new VisualElement();
                h.AddToClassList("sc-table__hcell");
                ApplyColumn(h, c);

                var caption = new Label(c.Header ?? string.Empty);
                caption.AddToClassList("sc-table__hlabel");
                h.Add(caption);

                if (c.SortKey != null)
                {
                    var arrow = new Label(string.Empty);
                    arrow.AddToClassList("sc-table__sort");
                    arrow.AddToClassList("sc-icon");
                    arrow.style.display = DisplayStyle.None;
                    h.Add(arrow);
                    _sortArrows[i] = arrow;

                    h.AddToClassList("sc-table__hcell--sortable");
                    int index = i;
                    h.RegisterCallback<ClickEvent>(_ => ToggleSort(index));
                }

                header.Add(h);
            }
            Add(header);

            _body = new ScrollView(ScrollViewMode.Vertical);
            _body.AddToClassList("sc-table__body");
            Add(_body);

            _note = new Label();
            _note.AddToClassList("sc-table__note");
            _note.enableRichText = false;
            _note.style.display = DisplayStyle.None;
            Add(_note);
        }

        /// <summary>Number of rows currently bound (0 while showing ghosts).</summary>
        public int RowCount => _rows.Count;

        public DataTable Bind(IEnumerable rows, Func<object, bool> highlight = null)
        {
            _ghostMode = false;
            _highlight = highlight;
            _rows.Clear();
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    _rows.Add(row);
                }
            }
            Render();
            return this;
        }

        /// <summary>
        /// Renders the header plus dimmed placeholder rows instead of data — keeps the table's
        /// shape (and the reader's context) while a load is in flight or a filter matched nothing.
        /// </summary>
        public DataTable BindEmpty(string message = null, int ghostRows = 3)
        {
            _ghostMode = true;
            _noteMessage = string.IsNullOrEmpty(message) ? "Nothing to show" : message;
            _ghostRows = ghostRows < 0 ? 0 : ghostRows;
            _rows.Clear();
            Render();
            return this;
        }

        /// <summary>Makes rows clickable (hover affordance included); the bound row object is passed back.</summary>
        public DataTable WithRowClick(Action<object> onClick)
        {
            _onRowClick = onClick;
            Render();
            return this;
        }

        /// <summary>Alternating row tint — helps the eye track wide rows.</summary>
        public DataTable WithZebra()
        {
            _zebra = true;
            Render();
            return this;
        }

        /// <summary>Caps the scrollable body height (the stylesheet default is 320px).</summary>
        public DataTable WithMaxHeight(float px)
        {
            _body.style.maxHeight = px < 0f ? 0f : px;
            return this;
        }

        /// <summary>Initial sort order; ignored (with a warning) for columns that declare no SortKey.</summary>
        public DataTable WithSort(int columnIndex, bool ascending)
        {
            if (columnIndex < 0 || columnIndex >= _cols.Length || _cols[columnIndex].SortKey == null)
            {
                Debug.LogWarning("[Showcase] DataTable.WithSort: column " + columnIndex + " is not sortable — ignored.");
                return this;
            }
            _sortColumn = columnIndex;
            _sortAscending = ascending;
            Render();
            return this;
        }

        private void ToggleSort(int columnIndex)
        {
            if (_sortColumn == columnIndex)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = columnIndex;
                _sortAscending = true;
            }
            Render();
        }

        private void Render()
        {
            _body.Clear();
            UpdateSortArrows();

            if (_ghostMode)
            {
                RenderGhosts();
                _note.text = _noteMessage;
                _note.style.display = DisplayStyle.Flex;
                return;
            }

            _note.style.display = DisplayStyle.None;
            var order = SortedOrder();
            for (int i = 0; i < order.Count; i++)
            {
                _body.Add(BuildRow(_rows[order[i]], i));
            }
        }

        private VisualElement BuildRow(object row, int visualIndex)
        {
            var tr = new VisualElement();
            tr.AddToClassList("sc-table__row");
            if (_zebra && (visualIndex & 1) == 1)
            {
                tr.AddToClassList("sc-table__row--odd");
            }
            if (_highlight != null && _highlight(row))
            {
                tr.AddToClassList("sc-table__row--hi");
            }
            if (_onRowClick != null)
            {
                tr.AddToClassList("sc-table__row--clickable");
                object captured = row;
                tr.RegisterCallback<ClickEvent>(_ => _onRowClick?.Invoke(captured));
            }

            foreach (var c in _cols)
            {
                var cell = new VisualElement();
                cell.AddToClassList("sc-table__cell");
                ApplyColumn(cell, c);
                var inner = c.Cell != null ? c.Cell(row) : new Label(string.Empty);
                if (inner != null)
                {
                    cell.Add(inner);
                }
                tr.Add(cell);
            }
            return tr;
        }

        private void RenderGhosts()
        {
            for (int r = 0; r < _ghostRows; r++)
            {
                var tr = new VisualElement();
                tr.AddToClassList("sc-table__row");
                tr.AddToClassList("sc-table__row--ghost");
                for (int c = 0; c < _cols.Length; c++)
                {
                    var cell = new VisualElement();
                    cell.AddToClassList("sc-table__cell");
                    ApplyColumn(cell, _cols[c]);
                    var bar = new VisualElement();
                    bar.AddToClassList("sc-table__ghost");
                    bar.style.width = Length.Percent(GhostWidths[(r * 3 + c) % GhostWidths.Length]);
                    cell.Add(bar);
                    tr.Add(cell);
                }
                _body.Add(tr);
            }
        }

        /// <summary>Row indices in display order. Ties keep the bound order, so sorting is stable.</summary>
        private List<int> SortedOrder()
        {
            var order = new List<int>(_rows.Count);
            for (int i = 0; i < _rows.Count; i++)
            {
                order.Add(i);
            }

            if (_sortColumn < 0 || _sortColumn >= _cols.Length)
            {
                return order;
            }
            var selector = _cols[_sortColumn].SortKey;
            if (selector == null || order.Count < 2)
            {
                return order;
            }

            var keys = new IComparable[_rows.Count];
            bool warned = false;
            for (int i = 0; i < _rows.Count; i++)
            {
                try
                {
                    keys[i] = selector(_rows[i]);
                }
                catch (Exception e)
                {
                    keys[i] = null;
                    if (!warned)
                    {
                        warned = true;
                        Debug.LogWarning("[Showcase] DataTable sort key failed on column " +
                                         _sortColumn + ": " + e.Message);
                    }
                }
            }

            int dir = _sortAscending ? 1 : -1;
            order.Sort((a, b) =>
            {
                int cmp = CompareKeys(keys[a], keys[b]) * dir;
                return cmp != 0 ? cmp : a.CompareTo(b);
            });
            return order;
        }

        private static int CompareKeys(IComparable a, IComparable b)
        {
            if (a == null)
            {
                return b == null ? 0 : -1;
            }
            if (b == null)
            {
                return 1;
            }
            try
            {
                // Normalized to -1/0/1: the caller flips the sign for descending, and some
                // IComparable implementations return arbitrary magnitudes.
                return Math.Sign(a.CompareTo(b));
            }
            catch (Exception)
            {
                // Mixed key types (e.g. int vs string) make CompareTo throw — degrade to text order
                // instead of aborting the whole render.
                return Math.Sign(string.CompareOrdinal(a.ToString() ?? string.Empty, b.ToString() ?? string.Empty));
            }
        }

        private void UpdateSortArrows()
        {
            for (int i = 0; i < _sortArrows.Length; i++)
            {
                var arrow = _sortArrows[i];
                if (arrow == null)
                {
                    continue;
                }
                bool active = i == _sortColumn;
                arrow.text = active
                    ? (_sortAscending ? LucideIcon.ChevronUp : LucideIcon.ChevronDown)
                    : string.Empty;
                arrow.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private static void ApplyColumn(VisualElement el, DataColumn c)
        {
            if (c.FixedWidth)
            {
                el.style.width = c.Px;
                el.style.flexGrow = 0f;
                el.style.flexShrink = 0f;
            }
            else
            {
                el.style.flexGrow = c.Grow <= 0f ? 1f : c.Grow;
                el.style.flexBasis = new Length(0f);
                el.style.flexShrink = 1f;
            }

            if (c.Align == "right")
            {
                el.AddToClassList("sc-cell-right");
            }
            else if (c.Align == "center")
            {
                el.AddToClassList("sc-cell-center");
            }
        }
    }
}
