using System;
using System.Collections.Generic;
using MirraCloud.Core.Enums;
using MirraCloud.Json;

namespace MirraCloud.Core.Localization.Dto
{
    /// <summary>
    /// A whole localization collection: every key with all its translations, and the project's languages.
    /// One request brings everything a screen needs; <see cref="Get"/> then answers locally.
    /// </summary>
    [Serializable]
    public sealed class LocalizationCollectionDto
    {
        /// <summary>Business key of the collection — what the game asks for.</summary>
        [JsonNameCamel] public string Key;

        /// <summary>Display name from the console.</summary>
        [JsonNameCamel] public string Name;

        /// <summary>What the values hold: <c>String</c>, <c>Image</c> or <c>Audio</c> (asset stable ids).</summary>
        [JsonNameCamel] public string Type;

        /// <summary>The project's default language — the fallback of <see cref="Get"/>.</summary>
        [JsonNameCamel] public LanguageCode DefaultLanguage;

        /// <summary>Languages enabled in the project.</summary>
        [JsonNameCamel] public List<LanguageCode> Languages;

        /// <summary>Every key of the collection.</summary>
        [JsonNameCamel] public List<LocalizationResponseDto> Keys;

        [NonSerialized] private Dictionary<string, LocalizationResponseDto> _byName;

        /// <summary>
        /// The value of <paramref name="key"/> in <paramref name="language"/>; when that translation is missing,
        /// the value in <see cref="DefaultLanguage"/>; when the key itself is missing, null.
        /// </summary>
        public string Get(string key, LanguageCode language)
        {
            return TryGet(key, language, out string value) ? value : null;
        }

        /// <summary>
        /// Same as <see cref="Get"/>, reporting whether anything was found.
        /// </summary>
        public bool TryGet(string key, LanguageCode language, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(key) || Index().TryGetValue(key, out LocalizationResponseDto entry) == false)
            {
                return false;
            }

            return TryValue(entry, language, out value) || TryValue(entry, DefaultLanguage, out value);
        }

        /// <summary>Whether the collection has <paramref name="key"/>.</summary>
        public bool Contains(string key)
        {
            return string.IsNullOrEmpty(key) == false && Index().ContainsKey(key);
        }

        private Dictionary<string, LocalizationResponseDto> Index()
        {
            if (_byName != null)
            {
                return _byName;
            }

            _byName = new Dictionary<string, LocalizationResponseDto>(StringComparer.Ordinal);
            if (Keys != null)
            {
                foreach (LocalizationResponseDto entry in Keys)
                {
                    if (entry?.KeyName != null && _byName.ContainsKey(entry.KeyName) == false)
                    {
                        _byName.Add(entry.KeyName, entry);
                    }
                }
            }

            return _byName;
        }

        private static bool TryValue(LocalizationResponseDto entry, LanguageCode language, out string value)
        {
            value = null;
            if (entry.Values == null)
            {
                return false;
            }

            foreach (LocalizationValueDto candidate in entry.Values)
            {
                if (candidate != null && candidate.LanguageCode == language && string.IsNullOrEmpty(candidate.Value) == false)
                {
                    value = candidate.Value;
                    return true;
                }
            }

            return false;
        }
    }
}
