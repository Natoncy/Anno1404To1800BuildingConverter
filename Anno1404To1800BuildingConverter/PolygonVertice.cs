using System.Xml;

namespace Anno1404To1800BuildingConverter;

public class PolygonVertice
{
    public PolygonVertice(double x, double z)
    {
        X = x;
        Z = z;
    }

    public PolygonVertice(XmlNode? node)
    {
        X = Program.ConvertDimension(node?.GetChild("x"));
        Z = Program.ConvertDimension(node?.GetChild("z"));
    }

    public double X { get; set; }
    public double Z { get; set; }

    public override string ToString()
    {
        return $"({X}, {Z})";
    }
}
