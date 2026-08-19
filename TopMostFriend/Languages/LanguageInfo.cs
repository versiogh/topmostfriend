using System.Xml.Serialization;

namespace TopMostFriend.Languages;

public sealed class LanguageInfo
{
    [XmlElement("Id")]
    public string Id { get; set; } = string.Empty;

    [XmlElement("NameNative")]
    public string NameNative { get; set; } = string.Empty;

    [XmlElement("NameEnglish")]
    public string NameEnglish { get; set; } = string.Empty;

    [XmlElement("TargetVersion")]
    public string TargetVersion { get; set; } = string.Empty;

    public override string ToString() => $"{NameNative} / {NameEnglish} ({Id})";
}
