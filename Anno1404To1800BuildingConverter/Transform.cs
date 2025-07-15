using System.Xml;
using SixLabors.ImageSharp;
using Quaternion = System.Numerics.Quaternion;

namespace Anno1404To1800BuildingConverter;

public class Transform
{
    public Transform(XmlNode? node)
    {
        Position = new Vector(node?.GetChild("Position"));
        Rotation = node?.GetChild("Rotation")?.GetConvertedQuarternion() ?? Quaternion.Identity;
        Extents = new Vector(node?.GetChild("Extents"));
    }

    public Transform(Polygon polygon, int yPosition, int yExtent)
    {
        var xMin = polygon.Select(x => x.X).Min();
        var xMax = polygon.Select(x => x.X).Max();
        var xPosition = (xMin + xMax) / 2;
        var xExtent = Math.Abs(xMax) - Math.Abs(xPosition);
        var zMin = polygon.Select(x => x.Z).Min();
        var zMax = polygon.Select(x => x.Z).Max();
        var zPosition = (zMin + zMax) / 2;
        var zExtent = Math.Abs(zMax) - Math.Abs(zPosition);

        Position = new Vector(xPosition, yPosition, zPosition);
        Rotation = new Quaternion(0, 0, 0, 1);
        Extents = new Vector(xExtent, yExtent, zExtent);
    }

    public Transform(Vector position, Quaternion rotation, Vector extents)
    {
        Position = position;
        Rotation = rotation;
        Extents = extents;
    }

    public Vector Position { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector Extents { get; set; }

    public override string ToString()
    {
        return $"{Position} / {Rotation} / {Extents}";
    }
}
