using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using TopMostFriend.Languages;

namespace TopMostFriend;

public static class Locale
{
    public const string DEFAULT = "en-GB";

    private static readonly XmlSerializer Serializer = new(typeof(Language));
    private static readonly Dictionary<string, Language> Languages = new(StringComparer.OrdinalIgnoreCase);
    private static Language? _activeLanguage;

    static Locale()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        foreach (string resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith("TopMostFriend.Languages.", StringComparison.Ordinal) ||
                !resource.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using Stream? stream = assembly.GetManifestResourceStream(resource);
                if (stream != null)
                    LoadLanguage(stream, replaceExisting: true);
            }
            catch (Exception ex)
            {
                AppLog.Write($"Unable to load embedded language '{resource}'.", ex);
            }
        }

        if (Languages.TryGetValue(DEFAULT, out Language? fallback))
            _activeLanguage = fallback;
        else if (Languages.Count > 0)
            _activeLanguage = Languages.Values.First();
    }

    public static string LoadLanguage(Stream stream, bool replaceExisting = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Language language = (Language?)Serializer.Deserialize(stream)
            ?? throw new InvalidDataException("Language XML did not contain a language object.");

        if (string.IsNullOrWhiteSpace(language.Info.Id))
            throw new InvalidDataException("Language XML is missing Info/Id.");

        language.Strings ??= Array.Empty<LanguageString>();
        foreach (LanguageString entry in language.Strings)
            entry.Value = (entry.Value ?? string.Empty).Trim();

        if (replaceExisting)
            Languages[language.Info.Id] = language;
        else
            Languages.Add(language.Info.Id, language);

        if (_activeLanguage == null || string.Equals(language.Info.Id, DEFAULT, StringComparison.OrdinalIgnoreCase))
            _activeLanguage ??= language;

        return language.Info.Id;
    }

    public static LanguageInfo GetCurrentLanguage() =>
        (_activeLanguage ?? throw new InvalidOperationException("No language is loaded.")).Info;

    public static LanguageInfo[] GetAvailableLanguages() =>
        Languages.Values
            .Select(l => l.Info)
            .OrderBy(i => i.NameEnglish, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public static string GetPreferredLanguage() =>
        Settings.Has(Program.LANGUAGE)
            ? Settings.Get(Program.LANGUAGE, DEFAULT)
            : CultureInfo.InstalledUICulture.Name;

    public static void SetLanguage(string? languageId)
    {
        if (string.IsNullOrWhiteSpace(languageId) || !Languages.TryGetValue(languageId, out Language? language))
        {
            if (!Languages.TryGetValue(DEFAULT, out language))
                language = Languages.Values.FirstOrDefault();
        }

        if (language != null)
            _activeLanguage = language;
    }

    public static void SetLanguage(LanguageInfo? languageInfo) => SetLanguage(languageInfo?.Id);

    public static string String(string name, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        Language? active = _activeLanguage;
        LanguageString? entry = active?.GetString(name);
        entry ??= Languages.TryGetValue(DEFAULT, out Language? fallback) ? fallback.GetString(name) : null;
        if (entry == null)
            return name;

        List<object> formatArgs = new() { Program.TITLE };
        formatArgs.AddRange(args);

        try
        {
            return entry.Format(formatArgs.ToArray());
        }
        catch (FormatException ex)
        {
            AppLog.Write($"Invalid format string in locale entry '{name}'.", ex);
            return entry.Value;
        }
    }
}
