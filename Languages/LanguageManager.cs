using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KerkenezMail.Languages
{
    /// <summary>
    /// Event arguments for active language change notifications.
    /// </summary>
    public class LanguageChangedEventArgs : EventArgs
    {
        public ILanguage Language { get; }

        public LanguageChangedEventArgs(ILanguage language)
        {
            Language = language;
        }
    }

    /// <summary>
    /// Manages language discovery, current active locale, fallbacks, and event propagation.
    /// Auto-discovers all <see cref="ILanguage"/> implementations in the assembly.
    /// </summary>
    public class LanguageManager
    {
        private static readonly Lazy<LanguageManager> _lazyInstance = new(() => new LanguageManager());
        public static LanguageManager Instance => _lazyInstance.Value;

        private readonly Dictionary<string, ILanguage> _languages = new(StringComparer.OrdinalIgnoreCase);
        private ILanguage _currentLanguage = null!;
        private ILanguage _fallbackLanguage = null!;

        public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

        private LanguageManager()
        {
            DiscoverLanguages();
        }

        private void DiscoverLanguages()
        {
            var langInterface = typeof(ILanguage);
            var assembly = Assembly.GetExecutingAssembly();

            try
            {
                var types = assembly.GetTypes()
                    .Where(t => langInterface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var type in types)
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is ILanguage instance && !string.IsNullOrWhiteSpace(instance.Code))
                        {
                            _languages[instance.Code] = instance;
                        }
                    }
                    catch
                    {
                        // Ignore types that cannot be instantiated with default constructor
                    }
                }
            }
            catch
            {
                // Fallback safe manual registration if reflection fails
            }

            // Ensure baseline English is registered
            if (!_languages.ContainsKey("en"))
            {
                _languages["en"] = new EnglishLanguage();
            }

            _fallbackLanguage = _languages["en"];
            _currentLanguage = _fallbackLanguage;
        }

        /// <summary>
        /// Gets all discovered languages ordered by name.
        /// </summary>
        public IReadOnlyList<ILanguage> AvailableLanguages =>
            _languages.Values.OrderBy(l => l.Name).ToList();

        /// <summary>
        /// Gets the current active language.
        /// </summary>
        public ILanguage CurrentLanguage => _currentLanguage;

        /// <summary>
        /// Sets the active language by ISO code (e.g., "en", "tr").
        /// Falls back to English if the code is unknown.
        /// </summary>
        public void SetLanguage(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                code = "en";
            }

            ILanguage target;
            if (_languages.TryGetValue(code, out var found))
            {
                target = found;
            }
            else
            {
                target = _fallbackLanguage;
            }

            if (_currentLanguage.Code != target.Code)
            {
                _currentLanguage = target;
                LanguageChanged?.Invoke(this, new LanguageChangedEventArgs(target));
            }
        }

        /// <summary>
        /// Gets the translated string for a key with fallback to English and key name.
        /// </summary>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            // 1. Try active language
            if (_currentLanguage.HasString(key))
            {
                return _currentLanguage.GetString(key);
            }

            // 2. Try default English fallback
            if (_fallbackLanguage.HasString(key))
            {
                return _fallbackLanguage.GetString(key);
            }

            // 3. Fallback to key itself
            return key;
        }

        /// <summary>
        /// Formats a localized string with parameters.
        /// </summary>
        public string Format(string key, params object[] args)
        {
            string format = Get(key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }
    }
}
