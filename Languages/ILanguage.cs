using System.Collections.Generic;

namespace KerkenezMail.Languages
{
    /// <summary>
    /// Contract for all language definitions in Kerkenez Mail.
    /// To add a new language, implement this interface or inherit from <see cref="BaseLanguage"/>.
    /// </summary>
    public interface ILanguage
    {
        /// <summary>
        /// ISO 639-1 two-letter code (e.g., "en", "tr", "de", "fr", "es").
        /// </summary>
        string Code { get; }

        /// <summary>
        /// Native display name of the language (e.g., "English", "Türkçe", "Deutsch").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// English display name of the language (e.g., "English", "Turkish", "German").
        /// </summary>
        string EnglishName { get; }

        /// <summary>
        /// Flag emoji representing the primary locale (e.g., "🇬🇧", "🇹🇷", "🇩🇪").
        /// </summary>
        string FlagEmoji { get; }

        /// <summary>
        /// Returns the translated text for the specified key.
        /// </summary>
        string GetString(string key);

        /// <summary>
        /// Checks if this language definition explicitly contains a translation for the specified key.
        /// </summary>
        bool HasString(string key);

        /// <summary>
        /// Returns all string key-value pairs defined in this language.
        /// </summary>
        IReadOnlyDictionary<string, string> GetAllStrings();
    }
}
