namespace KerkenezMail.Languages
{
    /// <summary>
    /// Ergonomic static accessors for localization lookup.
    /// Usage: <c>Lang.T(StringKeys.NavInbox)</c> or <c>Lang.Format(StringKeys.InboxMultiSelectCount, 5)</c>.
    /// </summary>
    public static class Lang
    {
        /// <summary>
        /// Translate a string key into the active language.
        /// </summary>
        public static string T(string key) => LanguageManager.Instance.Get(key);

        /// <summary>
        /// Translate a formatted string key with positional arguments into the active language.
        /// </summary>
        public static string Format(string key, params object[] args) => LanguageManager.Instance.Format(key, args);
    }
}
