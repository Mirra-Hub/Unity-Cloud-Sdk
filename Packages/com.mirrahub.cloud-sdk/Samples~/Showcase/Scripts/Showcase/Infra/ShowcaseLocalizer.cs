using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using MirraCloud.Core;
using MirraCloud.Core.Enums;
using MirraCloud.Core.Localization;
using MirraCloud.Core.Localization.Dto;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Translates the whole sample through the Localization service, from the <c>showcase_ui</c> collection
    /// the SDK Showcase package installs.
    /// <para>
    /// The sample is written in English, and the English text <i>is</i> the key: a key named
    /// <c>Leaderboards</c> holds <c>Лидерборды</c> in Russian. So no screen calls a translation function —
    /// a light pass over the visual tree swaps every text that has a translation, and swaps it back when the
    /// language changes. Keys may carry <c>{0}</c>-style holes for text built at runtime
    /// (<c>{0} keys</c> → <c>ключей: {0}</c>).
    /// </para>
    /// <para>
    /// Without the collection — no package, no network — nothing is swapped and the sample stays in English.
    /// The collection is public, so it loads before sign-in and the sign-in screen is translated too.
    /// </para>
    /// </summary>
    public sealed class ShowcaseLocalizer
    {
        /// <summary>Key of the collection the sample is translated from.</summary>
        public const string CollectionKey = "showcase_ui";

        private const string LanguagePref = "mirracloud.showcase.language";
        private const long PassIntervalMs = 250;
        private const string InputClass = "unity-base-text-field__input";

        private static readonly Regex Hole = new Regex(@"\\\{(\d)}", RegexOptions.Compiled);

        private readonly IMirraCloudSdk _sdk;
        private readonly VisualElement _root;
        private readonly ConditionalWeakTable<TextElement, Shown> _texts = new ConditionalWeakTable<TextElement, Shown>();
        private readonly ConditionalWeakTable<VisualElement, Shown> _tooltips = new ConditionalWeakTable<VisualElement, Shown>();
        private readonly Dictionary<string, string> _exact = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<Template> _templates = new List<Template>();
        private readonly Dictionary<string, string> _memo = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<LanguageCode> _languages = new List<LanguageCode>();

        private LocalizationCollectionDto _collection;
        private bool _loading;
        private Button _switch;

        public ShowcaseLocalizer(IMirraCloudSdk sdk, VisualElement root)
        {
            _sdk = sdk;
            _root = root;
        }

        /// <summary>The language the sample is shown in.</summary>
        public LanguageCode Language { get; private set; } = LanguageCode.En;

        /// <summary>Whether the collection has been read.</summary>
        public bool IsLoaded => _collection != null;

        /// <summary>Starts the tree pass and adds the language switch to <paramref name="host"/>.</summary>
        public void Attach(VisualElement host)
        {
            _switch = new Button(NextLanguage);
            _switch.AddToClassList("sc-btn");
            _switch.style.position = Position.Absolute;
            _switch.style.left = 16;
            _switch.style.bottom = 16;
            _switch.style.minWidth = 44;
            _switch.style.display = DisplayStyle.None;
            _switch.tooltip = "Language";
            host.Add(_switch);

            _root.schedule.Execute(Pass).Every(PassIntervalMs);
        }

        /// <summary>
        /// Reads the collection. Auto goes anonymously before sign-in and as the player after, so calling
        /// it again once signed in also picks up a collection that is not public.
        /// </summary>
        public async void Load()
        {
            if (_loading)
            {
                return;
            }

            _loading = true;
            try
            {
                var op = _sdk.Localization.GetCollectionAsync(CollectionKey, LocalizationAccess.Auto);
                await op.Task();
                var result = op.Result;
                if (result == null || !result.IsSuccess || result.Data == null)
                {
                    Debug.Log("[Showcase] localization collection '" + CollectionKey + "' not available ("
                        + (result == null ? "no result" : "HTTP " + result.HttpStatusCode) + "), staying in English");
                    return;
                }

                _collection = result.Data;
                _languages.Clear();
                _languages.AddRange(_collection.Languages ?? new List<LanguageCode>());
                if (!_languages.Contains(LanguageCode.En))
                {
                    _languages.Insert(0, LanguageCode.En);
                }

                SetLanguage(PickLanguage());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] loading the localization collection failed: " + e.Message);
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>Shows the sample in <paramref name="language"/> and remembers the choice.</summary>
        public void SetLanguage(LanguageCode language)
        {
            Language = language;
            PlayerPrefs.SetString(LanguagePref, language.ToLanguageString());
            Rebuild();
            Pass();

            if (_switch != null)
            {
                _switch.text = language.ToLanguageString().ToUpperInvariant();
                _switch.style.display = _languages.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>The translation of <paramref name="source"/>, for text set outside the visual tree.</summary>
        public string T(string source)
        {
            return Translate(source) ?? source;
        }

        private void NextLanguage()
        {
            if (_languages.Count < 2)
            {
                return;
            }

            int index = _languages.IndexOf(Language);
            SetLanguage(_languages[(index + 1) % _languages.Count]);
        }

        private LanguageCode PickLanguage()
        {
            string saved = PlayerPrefs.GetString(LanguagePref, string.Empty);
            foreach (var language in _languages)
            {
                if (language.ToLanguageString() == saved)
                {
                    return language;
                }
            }

            LanguageCode? system = FromSystemLanguage(Application.systemLanguage);
            if (system.HasValue && _languages.Contains(system.Value))
            {
                return system.Value;
            }

            return LanguageCode.En;
        }

        private void Rebuild()
        {
            _exact.Clear();
            _templates.Clear();
            _memo.Clear();
            if (_collection?.Keys == null)
            {
                return;
            }

            foreach (var entry in _collection.Keys)
            {
                if (entry == null || string.IsNullOrEmpty(entry.KeyName))
                {
                    continue;
                }

                string value = _collection.Get(entry.KeyName, Language);
                if (string.IsNullOrEmpty(value) || value == entry.KeyName)
                {
                    continue;
                }

                if (entry.KeyName.Contains("{0}"))
                {
                    _templates.Add(new Template(entry.KeyName, value));
                }
                else
                {
                    _exact[entry.KeyName] = value;
                }
            }
        }

        private string Translate(string source)
        {
            if (string.IsNullOrEmpty(source) || (_exact.Count == 0 && _templates.Count == 0))
            {
                return null;
            }

            if (_exact.TryGetValue(source, out string exact))
            {
                return exact;
            }

            if (_memo.TryGetValue(source, out string memo))
            {
                return memo;
            }

            string found = null;
            foreach (var template in _templates)
            {
                if (template.TryApply(source, out found))
                {
                    break;
                }
            }

            _memo[source] = found;
            return found;
        }

        private void Pass()
        {
            _root.Query<TextElement>().ForEach(SwapText);
            _root.Query<VisualElement>().Where(e => !string.IsNullOrEmpty(e.tooltip)).ForEach(SwapTooltip);
        }

        private void SwapText(TextElement element)
        {
            if (element == _switch || IsUserInput(element))
            {
                return;
            }

            var shown = _texts.GetValue(element, _ => new Shown());
            string source = shown.Text != null && element.text == shown.Text ? shown.Source : element.text;
            string target = Translate(source) ?? source;

            shown.Source = source;
            shown.Text = target;
            if (element.text != target)
            {
                element.text = target;
            }
        }

        private void SwapTooltip(VisualElement element)
        {
            var shown = _tooltips.GetValue(element, _ => new Shown());
            string source = shown.Text != null && element.tooltip == shown.Text ? shown.Source : element.tooltip;
            string target = Translate(source) ?? source;

            shown.Source = source;
            shown.Text = target;
            if (element.tooltip != target)
            {
                element.tooltip = target;
            }
        }

        // What the player types into a field is theirs: a nickname that happens to read "Back" stays "Back".
        private static bool IsUserInput(VisualElement element)
        {
            for (var current = element.parent; current != null; current = current.parent)
            {
                if (current.ClassListContains(InputClass))
                {
                    return true;
                }
            }

            return false;
        }

        private static LanguageCode? FromSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.English: return LanguageCode.En;
                case SystemLanguage.Russian: return LanguageCode.Ru;
                case SystemLanguage.German: return LanguageCode.De;
                case SystemLanguage.French: return LanguageCode.Fr;
                case SystemLanguage.Spanish: return LanguageCode.Es;
                case SystemLanguage.Italian: return LanguageCode.It;
                case SystemLanguage.Portuguese: return LanguageCode.Pt;
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return LanguageCode.ZhHans;
                case SystemLanguage.ChineseTraditional: return LanguageCode.ZhHant;
                case SystemLanguage.Japanese: return LanguageCode.Ja;
                case SystemLanguage.Korean: return LanguageCode.Ko;
                case SystemLanguage.Arabic: return LanguageCode.Ar;
                case SystemLanguage.Turkish: return LanguageCode.Tr;
                case SystemLanguage.Polish: return LanguageCode.Pl;
                case SystemLanguage.Ukrainian: return LanguageCode.Uk;
                case SystemLanguage.Czech: return LanguageCode.Cs;
                case SystemLanguage.Dutch: return LanguageCode.Nl;
                case SystemLanguage.Hungarian: return LanguageCode.Hu;
                case SystemLanguage.Greek: return LanguageCode.El;
                default: return null;
            }
        }

        private sealed class Shown
        {
            public string Source;
            public string Text;
        }

        /// <summary>A key with <c>{0}</c>-style holes, matched against the whole text.</summary>
        private sealed class Template
        {
            private readonly Regex _pattern;
            private readonly string _value;

            public Template(string key, string value)
            {
                _pattern = new Regex("^" + Hole.Replace(Regex.Escape(key), "(?<h$1>.+?)") + "$", RegexOptions.Singleline);
                _value = value;
            }

            public bool TryApply(string source, out string translated)
            {
                translated = null;
                Match match = _pattern.Match(source);
                if (!match.Success)
                {
                    return false;
                }

                var builder = new StringBuilder(_value);
                for (int i = 0; i < 10; i++)
                {
                    Group hole = match.Groups["h" + i];
                    if (hole.Success)
                    {
                        builder.Replace("{" + i + "}", hole.Value);
                    }
                }

                translated = builder.ToString();
                return true;
            }
        }
    }
}
