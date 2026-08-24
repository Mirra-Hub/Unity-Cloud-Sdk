using System;
using System.Collections.Generic;
using MirraCloud.Core.Enums;
using MirraCloud.Core.Localization.Dto;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Localization screen: one collection's keys, the text they carry in the language picked in the
    /// toolbar, and how much of the collection that language actually covers.
    /// <para>
    /// The service is lookup-driven — there is no endpoint that lists collections — so the id is typed
    /// (or pasted from the console) and everything else follows from the single
    /// <c>GetAllLocalizationsAsync</c> response. That response holds every language of every key, which
    /// is what makes the coverage figures and the per-key dialog possible without further requests.
    /// </para>
    /// <para>
    /// The language set is therefore only known once data has arrived: the toolbar filter starts empty
    /// and is refilled from the response (see <see cref="SyncLanguageFilter"/>).
    /// </para>
    /// </summary>
    public sealed class LocalizationView : ServiceView
    {
        private const int PageSize = 25;

        /// <summary>Filler option for the language filter before any collection has been read.</summary>
        private const string NoLanguages = "—";

        private const string AllSnippet =
@"// A collection is the unit of loading: one call brings every key with every translation.
var op = sdk.Localization.GetAllLocalizationsAsync(collectionId);
await op.Task();
if (!op.Result.IsSuccess) { return; }

foreach (LocalizationResponseDto row in op.Result.Data)
{
    // row.KeyName, row.StableId, row.GroupId, row.CreatedDate, row.UpdatedDate
    foreach (LocalizationValueDto value in row.Values)
    {
        // value.LanguageCode (the LanguageCode enum), value.Value
        Debug.Log(row.KeyName + "" ["" + value.LanguageCode + ""] = "" + value.Value);
    }
}

// There is no ""list my collections"" endpoint — keep the id as a constant in the game, or
// read it from the Mirra Hub console.";

        private const string KeySnippet =
@"// What a game calls in play: one key, instead of the whole collection.
var all = sdk.Localization.GetValuesAsync(collectionId, key);       // every language of one key
var one = sdk.Localization.GetValueAsync(collectionId, key, LanguageCode.En);
await one.Task();

// An untranslated key answers with a failure rather than an empty string, so the caller keeps
// a fallback — another language, or the key itself.
string text = one.Result.IsSuccess ? one.Result.Data.Value : key;";

        private readonly List<LanguageCode> _languages = new List<LanguageCode>();

        private List<LocalizationResponseDto> _rows = new List<LocalizationResponseDto>();
        private string _collectionId;
        private string _query = string.Empty;
        private LanguageCode? _language;
        private Toolbar _bar;
        private VisualElement _host;

        public LocalizationView(ServiceMeta meta, Action onBack, ShowcaseContext ctx)
            : base(meta, onBack, ctx)
        {
        }

        protected override void Populate()
        {
            _query = string.Empty;
            // Refresh() detaches the previous pass' subtree; keeping the reference would let a later
            // search or language change paint into an orphan.
            _host = null;
            SetStatus(null);

            DeclareCall(new SdkCall("Read a whole collection", AllSnippet,
                "This screen's only request. It carries every language, which is what the coverage "
                + "figures below are counted from."));
            DeclareCall(new SdkCall("Read one key at runtime", KeySnippet,
                "The calls a game makes in play — shown here for reference; this screen loads the "
                + "collection in one go instead, so it can report what is missing."));

            _bar = UseToolbar()
                .WithSearch("Search keys", OnSearch)
                .WithFilter("Language", LanguageOptions(), OnLanguage, CurrentLanguageName())
                .WithSpacer()
                .WithRefresh(Refresh);

            Content.Add(BuildPicker());

            if (string.IsNullOrEmpty(_collectionId))
            {
                SetStatus("No collection", ChipTone.Neutral);
                Content.Add(ZeroState.Panel(LucideIcon.Languages, "No collection loaded",
                    "Localization is organised into collections: a collection holds keys, and every key "
                    + "holds one string per language. Both are authored in the Mirra Hub console, and the "
                    + "SDK has no call that lists collections — so paste an id above to read one.",
                    null, null,
                    "Console → your project → Localization → open a collection and copy its id."));
                return;
            }

            var slot = AddSlot(0f);
            ViewBind.Load(
                () => Sdk.Localization.GetAllLocalizationsAsync(_collectionId),
                slot,
                BuildStrings,
                data => data == null || data.Count == 0,
                new BindOptions
                {
                    Log = Ctx.Log,
                    Label = "Localization collection",
                    Snippet = AllSnippet,
                    ServiceName = "Localization",
                    // The collection is this service's configuration: a 404 means no such collection
                    // exists on this branch, which is a console matter rather than a broken call.
                    ConfigurationRequest = true,
                    AllowRetry = true,
                    EmptyView = NoStrings,
                });
        }

        // ----- collection picker ----------------------------------------------------------------

        private VisualElement BuildPicker()
        {
            var box = new VisualElement();
            box.AddToClassList("sc-loc-picker");

            var picker = new VisualElement();
            picker.AddToClassList("sc-chat-lookup");

            var field = new TextField { label = "Collection id", value = _collectionId ?? string.Empty };
            field.AddToClassList("sc-field");
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    Open(field.value);
                }
            });
            picker.Add(field);

            var load = new Button(() => Open(field.value)) { text = "Load" };
            load.AddToClassList("sc-btn");
            load.AddToClassList("sc-btn--primary");
            picker.Add(load);
            box.Add(picker);

            var hint = new Label("The id is the collection's identifier in the console. A game normally "
                + "keeps it as a constant, since there is no call that discovers it.");
            hint.AddToClassList("sc-fs-hint");
            box.Add(hint);
            return box;
        }

        private void Open(string collectionId)
        {
            string id = collectionId == null ? null : collectionId.Trim();
            if (string.IsNullOrEmpty(id))
            {
                if (Toasts != null)
                {
                    Toasts.Info("Type a collection id first");
                }
                return;
            }

            if (id != _collectionId)
            {
                // The language set belongs to the collection, not to the screen: a new id starts from
                // "nothing known" so the filter cannot keep offering a language that is not there.
                _languages.Clear();
                _language = null;
                _rows = new List<LocalizationResponseDto>();
            }
            _collectionId = id;
            Refresh();
        }

        // ----- toolbar --------------------------------------------------------------------------

        private void OnSearch(string text)
        {
            _query = text == null ? string.Empty : text.Trim();
            Render();
        }

        private void OnLanguage(string name)
        {
            if (string.IsNullOrEmpty(name) || name == NoLanguages)
            {
                return;
            }
            foreach (var language in _languages)
            {
                if (language.ToLanguageString() == name)
                {
                    if (_language.HasValue && _language.Value == language)
                    {
                        return;
                    }
                    _language = language;
                    Render();
                    return;
                }
            }
        }

        private string[] LanguageOptions()
        {
            if (_languages.Count == 0)
            {
                return new[] { NoLanguages };
            }
            var options = new string[_languages.Count];
            for (int i = 0; i < _languages.Count; i++)
            {
                options[i] = _languages[i].ToLanguageString();
            }
            return options;
        }

        private string CurrentLanguageName()
        {
            return _language.HasValue ? _language.Value.ToLanguageString() : NoLanguages;
        }

        /// <summary>
        /// Refills the language dropdown once the response says which languages exist. The toolbar is
        /// built before any data arrives and <see cref="Toolbar.WithFilter"/> takes its options once, so
        /// the field itself is updated in place — rebuilding the toolbar would drop the search text.
        /// </summary>
        private void SyncLanguageFilter()
        {
            if (_bar == null)
            {
                return;
            }
            var dropdown = _bar.Q<DropdownField>(className: "sc-toolbar__filter");
            if (dropdown == null)
            {
                Debug.LogWarning("[Showcase] Localization: language filter not found, "
                    + "the toolbar keeps its placeholder");
                return;
            }

            var choices = new List<string>(LanguageOptions());
            dropdown.choices = choices;
            string current = CurrentLanguageName();
            // SetValueWithoutNotify: this is a sync, not a user pick — notifying would re-render the
            // body that is being built right now.
            dropdown.SetValueWithoutNotify(choices.Contains(current) ? current : choices[0]);
        }

        // ----- data -----------------------------------------------------------------------------

        private VisualElement BuildStrings(List<LocalizationResponseDto> rows)
        {
            _rows = rows ?? new List<LocalizationResponseDto>();
            CollectLanguages();

            if (!_language.HasValue || !_languages.Contains(_language.Value))
            {
                _language = _languages.Count > 0 ? _languages[0] : (LanguageCode?)null;
            }
            SyncLanguageFilter();

            SetStatus(_rows.Count + (_rows.Count == 1 ? " key · " : " keys · ")
                + _languages.Count + (_languages.Count == 1 ? " language" : " languages"),
                ChipTone.Ok);

            _host = new VisualElement();
            Render();
            return _host;
        }

        private VisualElement NoStrings()
        {
            SetStatus("Empty collection", ChipTone.Warn);
            _languages.Clear();
            _language = null;
            SyncLanguageFilter();
            return ZeroState.Table(Columns(),
                "This collection has no keys yet. Keys and their translations are authored in the Mirra "
                + "Hub console — add one there and it appears here as a row, with a column per language "
                + "it has been translated into.",
                4);
        }

        /// <summary>Every language mentioned by any key, sorted so the filter and the chart keep the
        /// same order between loads.</summary>
        private void CollectLanguages()
        {
            _languages.Clear();
            var seen = new HashSet<LanguageCode>();
            foreach (var row in _rows)
            {
                if (row == null || row.Values == null)
                {
                    continue;
                }
                foreach (var value in row.Values)
                {
                    if (value != null && seen.Add(value.LanguageCode))
                    {
                        _languages.Add(value.LanguageCode);
                    }
                }
            }
            _languages.Sort((a, b) => string.CompareOrdinal(a.ToLanguageString(), b.ToLanguageString()));
        }

        /// <summary>
        /// The text of one key in one language, or null when it is missing. A present-but-blank string
        /// counts as missing on purpose: to a player it is the same thing, and the coverage numbers
        /// would otherwise claim work that is not done.
        /// </summary>
        private static string ValueFor(LocalizationResponseDto row, LanguageCode language)
        {
            if (row == null || row.Values == null)
            {
                return null;
            }
            foreach (var value in row.Values)
            {
                if (value != null && value.LanguageCode == language)
                {
                    return string.IsNullOrWhiteSpace(value.Value) ? null : value.Value;
                }
            }
            return null;
        }

        private static int TranslationCount(LocalizationResponseDto row)
        {
            if (row == null || row.Values == null)
            {
                return 0;
            }
            int count = 0;
            foreach (var value in row.Values)
            {
                if (value != null && !string.IsNullOrWhiteSpace(value.Value))
                {
                    count++;
                }
            }
            return count;
        }

        private int TranslatedIn(LanguageCode language)
        {
            int count = 0;
            foreach (var row in _rows)
            {
                if (ValueFor(row, language) != null)
                {
                    count++;
                }
            }
            return count;
        }

        // ----- rendering ------------------------------------------------------------------------

        private void Render()
        {
            if (_host == null)
            {
                return;
            }
            _host.Clear();
            _host.Add(BuildKpis());
            _host.Add(BuildCoverageChart());

            var hint = new Label("A dash in the value column is a key this language has no text for — "
                + "that column is the translation backlog. Pick another language in the toolbar to see "
                + "how far it has got.");
            hint.AddToClassList("sc-fs-hint");
            _host.Add(hint);

            var filtered = Filter();
            _host.Add(new SectionHeader("Strings", filtered.Count == _rows.Count
                ? _rows.Count.ToString()
                : filtered.Count + " of " + _rows.Count));

            if (filtered.Count == 0)
            {
                _host.Add(ZeroState.Table(Columns(),
                    "No key contains \"" + Fmt.Truncate(_query, 24) + "\". Clear the search box to see "
                    + "the whole collection again.",
                    3));
                return;
            }

            var tableSlot = new VisualElement();
            _host.Add(tableSlot);

            // The response is not paged, so the pager slices the list already in memory rather than
            // asking the server for a page it cannot serve.
            var pager = new Pager(PageSize);
            _host.Add(pager);
            pager.PageRequested += page => RenderPage(tableSlot, filtered, pager, page);
            RenderPage(tableSlot, filtered, pager, 1);
        }

        private void RenderPage(VisualElement slot, List<LocalizationResponseDto> rows, Pager pager, int page)
        {
            pager.SetTotal(rows.Count, page);
            var table = new DataTable(Columns())
                .WithZebra()
                .WithMaxHeight(520f)
                .WithRowClick(o => ShowKey((LocalizationResponseDto)o))
                .WithSort(0, true);
            table.Bind(Pager.Slice(rows, pager.Page, PageSize));
            Replace(slot, table);
        }

        private VisualElement BuildKpis()
        {
            var kpis = new KpiRow();

            if (_rows.Count == 0)
            {
                kpis.AddZero("Keys", LucideIcon.Hash);
            }
            else
            {
                kpis.Add("Keys", LucideIcon.Hash, Fmt.Number(_rows.Count));
            }

            if (_languages.Count == 0)
            {
                kpis.AddZero("Languages", LucideIcon.Languages);
            }
            else
            {
                kpis.Add("Languages", LucideIcon.Languages, _languages.Count.ToString());
            }

            if (!_language.HasValue || _rows.Count == 0)
            {
                kpis.AddZero("Coverage", LucideIcon.Percent, Fmt.Dash);
            }
            else
            {
                int done = TranslatedIn(_language.Value);
                float ratio = (float)done / _rows.Count;
                kpis.Add("Coverage · " + _language.Value.ToLanguageString(), LucideIcon.Percent,
                    Fmt.Percent(ratio), done + " of " + _rows.Count + " keys", done == _rows.Count);
            }
            return kpis;
        }

        /// <summary>Keys translated per language, with the selected one picked out — the shape of the
        /// backlog, which a single coverage figure hides.</summary>
        private VisualElement BuildCoverageChart()
        {
            var points = new List<ChartPoint>(_languages.Count);
            foreach (var language in _languages)
            {
                bool selected = _language.HasValue && _language.Value == language;
                points.Add(new ChartPoint(language.ToLanguageString(), TranslatedIn(language),
                    selected ? Meta.Accent : ShowcaseTheme.TextDim));
            }

            var chart = new BarChart(170f);
            chart.AddToClassList("sc-loc-chart");
            chart.SetAccent(Meta.Accent)
                .SetValueFormatter(v => Fmt.Number(v))
                .SetData(points)
                .SetEmptyText("No translations in this collection yet");
            return chart;
        }

        private List<LocalizationResponseDto> Filter()
        {
            var kept = new List<LocalizationResponseDto>();
            foreach (var row in _rows)
            {
                if (row == null)
                {
                    continue;
                }
                if (_query.Length == 0
                    || (!string.IsNullOrEmpty(row.KeyName)
                        && row.KeyName.IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    kept.Add(row);
                }
            }
            return kept;
        }

        private DataColumn[] Columns()
        {
            var language = _language;
            int languages = _languages.Count;

            return new[]
            {
                new DataColumn
                {
                    Header = "KEY", Grow = 1.5f,
                    SortKey = o => ((LocalizationResponseDto)o).KeyName,
                    Cell = o =>
                    {
                        var label = new Label(Fmt.OrDash(((LocalizationResponseDto)o).KeyName));
                        label.enableRichText = false;
                        label.AddToClassList("sc-loc-key");
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = language.HasValue
                        ? "VALUE · " + language.Value.ToLanguageString().ToUpperInvariant()
                        : "VALUE",
                    Grow = 2.6f,
                    SortKey = o => language.HasValue
                        ? ValueFor((LocalizationResponseDto)o, language.Value)
                        : null,
                    Cell = o =>
                    {
                        string text = language.HasValue
                            ? ValueFor((LocalizationResponseDto)o, language.Value)
                            : null;
                        var label = new Label(text == null ? Fmt.Dash : Fmt.Truncate(text, 72));
                        label.enableRichText = false;
                        label.AddToClassList(text == null ? "sc-loc-missing" : "sc-loc-value");
                        label.tooltip = text ?? "Not translated into this language yet";
                        return label;
                    },
                },
                new DataColumn
                {
                    Header = "TRANSLATIONS", FixedWidth = true, Px = 130, Align = "center",
                    SortKey = o => TranslationCount((LocalizationResponseDto)o),
                    Cell = o =>
                    {
                        int count = TranslationCount((LocalizationResponseDto)o);
                        return new Badge(count + " / " + languages,
                            count >= languages && languages > 0 ? ChipTone.Ok : ChipTone.Warn);
                    },
                },
                new DataColumn
                {
                    Header = "UPDATED", FixedWidth = true, Px = 108, Align = "right",
                    SortKey = o => ((LocalizationResponseDto)o).UpdatedDate,
                    Cell = o => new Label(Fmt.Date(((LocalizationResponseDto)o).UpdatedDate)),
                },
            };
        }

        // ----- one key --------------------------------------------------------------------------

        private void ShowKey(LocalizationResponseDto row)
        {
            if (Popup == null || row == null)
            {
                return;
            }

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.style.maxHeight = 460f;

            var kv = new VisualElement();
            kv.AddToClassList("sc-kv-list");
            kv.Add(Kv("Key", Fmt.OrDash(row.KeyName), row.KeyName));
            kv.Add(Kv("Stable id", Fmt.Id(row.StableId, 14), row.StableId));
            if (!string.IsNullOrEmpty(row.GroupId))
            {
                kv.Add(Kv("Group id", Fmt.Id(row.GroupId, 14), row.GroupId));
            }
            kv.Add(Kv("Created", Fmt.DateTime2(row.CreatedDate), null));
            kv.Add(Kv("Updated", Fmt.DateTime2(row.UpdatedDate), null));
            body.Add(kv);

            int translated = TranslationCount(row);
            body.Add(new SectionHeader("Translations", translated + " of " + _languages.Count));

            if (_languages.Count == 0 || translated == 0)
            {
                body.Add(ZeroState.Panel(LucideIcon.Languages, "No text yet",
                    "The key exists in the collection but carries no string in any language. Add the "
                    + "text in the Mirra Hub console; the SDK reads translations and never writes them."));
            }
            else
            {
                var values = new VisualElement();
                values.AddToClassList("sc-kv-list");
                foreach (var language in _languages)
                {
                    string text = ValueFor(row, language);
                    values.Add(Kv(language.ToLanguageString(), text ?? Fmt.Dash, text, text == null));
                }
                body.Add(values);
            }

            Popup.Open(body, Fmt.Truncate(Fmt.OrDash(row.KeyName), 34));
        }

        private VisualElement Kv(string key, string value, string copyable, bool dim = false)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-kv");

            var k = new Label(key);
            k.AddToClassList("sc-kv__k");
            row.Add(k);

            var v = new Label(value);
            v.enableRichText = false;
            v.AddToClassList("sc-kv__v");
            if (dim)
            {
                v.AddToClassList("sc-loc-missing");
            }
            row.Add(v);

            if (!string.IsNullOrEmpty(copyable))
            {
                row.Add(new CopyButton(copyable, Toasts));
            }
            return row;
        }
    }
}
