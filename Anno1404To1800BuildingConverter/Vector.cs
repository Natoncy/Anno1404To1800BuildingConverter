using System.Xml;

namespace Anno1404To1800BuildingConverter;

public class Vector
{
    public Vector(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector(XmlNode? node)
    {
        X = Program.ConvertDimension(node?.GetChild("x"));
        Y = Program.ConvertDimension(node?.GetChild("y"));
        Z = Program.ConvertDimension(node?.GetChild("z"));
    }

    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }

    public double Length()
    {
        return Math.Sqrt(
            Math.Pow(X, 2) +
            Math.Pow(Y, 2) +
            Math.Pow(Z, 2)
        );
    }

    public double DistanceTo(Vector other)
    {
        return Math.Sqrt(
            Math.Pow(other.X - X, 2) +
            Math.Pow(other.Y - Y, 2) +
            Math.Pow(other.Z - Z, 2)
        );
    }
}
