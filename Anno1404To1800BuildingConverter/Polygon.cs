using System.Xml;

namespace Anno1404To1800BuildingConverter;

public class Polygon : List<PolygonVertice>
{
    public Polygon(XmlNode? node)
    {
        var positions = node?.GetChildren("Position") ?? new List<XmlNode?>();

        if (positions.Any())
        {
            foreach (var position in positions)
            {
                Add(position);
            }
        }
        else
        {
            Add(-1, -1);
            Add(-1, 1);
            Add(1, 1);
            Add(1, -1);
        }
    }

    public void Add(XmlNode? node)
    {
        Add(new PolygonVertice(node));
    }

    public void Add(double x, double z)
    {
        Add(new PolygonVertice(x, z));
    }
}
