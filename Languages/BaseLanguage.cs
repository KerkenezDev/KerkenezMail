using System;
using System.Collections.Generic;

namespace KerkenezMail.Languages
{
    /// <summary>
    /// Base class for all language definitions. Provides an aligned key-value dictionary table
    /// and standard lookup helpers.
    /// </summary>
    public abstract class BaseLanguage : ILanguage
    {
        public abstract string Code { get; }
        public abstract string Name { get; }
        public abstract string EnglishName { get; }
        public virtual string FlagEmoji => "";

        protected readonly Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);

        protected BaseLanguage()
        {
            InitTranslations();
        }

        /// <summary>
        /// Populate the language dictionary table using <see cref="Set(string, string)"/>.
        /// </summary>
        protected abstract void InitTranslations();

        /// <summary>
        /// Maps a string key to its translated value in this language.
        /// </summary>
        protected void Set(string key, string value)
        {
            _translations[key] = value;
        }

        public virtual string GetString(string key)
        {
            if (_translations.TryGetValue(key, out var value))
            {
                return value;
            }
            return key;
        }

        public bool HasString(string key)
        {
            return _translations.ContainsKey(key);
        }

        public IReadOnlyDictionary<string, string> GetAllStrings()
        {
            return _translations;
        }

        public override string ToString() => $"{FlagEmoji} {Name} ({Code})".Trim();
    }
}
