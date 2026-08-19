using System;
using System.Linq;
using System.Xml.Serialization;

namespace TopMostFriend.Languages;

[XmlRoot("Language")]
public sealed class Language
{
    [XmlElement("Info")]
    public LanguageInfo Info { get; set; } = new();

    [XmlArray("Strings")]
    [XmlArrayItem("String", Type = typeof(LanguageString))]
    public LanguageString[] Strings { get; set; } = Array.Empty<LanguageString>();

    public LanguageString? GetString(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Strings.FirstOrDefault(s => string.Equals(name, s.Name, StringComparison.Ordinal));
    }
}
