using System.Xml;
using Quaternion = System.Numerics.Quaternion;

namespace Anno1404To1800BuildingConverter;

public static class XmlNodeExtensions
{
    public static Quaternion GetConvertedQuarternion(this XmlNode node)
    {
        return new Quaternion(
            (float)Program.ConvertDimension(node?.GetChild("x")),
            (float)Program.ConvertDimension(node?.GetChild("y")),
            (float)Program.ConvertDimension(node?.GetChild("z")),
            (float)Program.ConvertDimension(node?.GetChild("w")));
    }

    public static XmlNode? GetChildConfig(this XmlNode node, string name)
    {
        return node?.GetChild(name)?.GetConfig();
    }

    public static XmlNode? GetConfig(this XmlNode node)
    {
        return node?.GetChild("m_Config");
    }

    public static List<XmlNode?> GetChildConfigs(this XmlNode node, string name)
    {
        return node?.GetChild(name)?.GetConfigs() ?? new List<XmlNode?>();
    }

    public static List<XmlNode?> GetConfigs(this XmlNode node)
    {
        return node?.GetChildren("m_Config");
    }

    public static XmlNode? GetChild(this XmlNode node, string name)
    {
        return node?.GetChildren(name)?.FirstOrDefault();
    }

    public static double GetNumber(this XmlNode node)
    {
        return Program.ParseNumber(node?.InnerText);
    }

    public static List<XmlNode?> GetChildren(this XmlNode node, string name)
    {
        return node?.ChildNodes?.Cast<XmlNode?>()?.Where(x => x?.Name == name)?.ToList() ?? new List<XmlNode?>();
    }
}
