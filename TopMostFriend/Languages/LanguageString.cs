using System.Xml.Serialization;

namespace TopMostFriend.Languages;

public sealed class LanguageString
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlText]
    public string Value { get; set; } = string.Empty;

    public string Format(params object[] args) => string.Format(Value, args);

    public override string ToString() => $"{Name}: {Value}";
}
