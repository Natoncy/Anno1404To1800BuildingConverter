
using System.Numerics;

namespace Anno1404To1800BuildingConverter;

public class Ground1404
{
    public string Diff { get; set; }
    public double TextCoordXStart { get; set; }
    public double TextCoordYStart { get; set; }
    public double TextCoordXEnd { get; set; }
    public double TextCoordYEnd { get; set; }
    public double ExtendsX { get; set; }
    public double ExtendsY { get; set; }
    public Vector Position { get; set; }
    public Quaternion Rotation { get; set; }
    public double RotatedExtendsX { get => GetRotatedExtents().X; }
    public double RotatedExtendsY { get => GetRotatedExtents().Y; }


    private Vector2 GetRotatedExtents()
    {
        Vector2 extents = new Vector2((float)ExtendsX, (float)ExtendsY);
        Quaternion rotation = Rotation;

        // Compute original corners relative to center
        Vector2[] corners = new Vector2[]
        {
        new Vector2(-extents.X, -extents.Y), // Bottom-left
        new Vector2(-extents.X,  extents.Y), // Top-left
        new Vector2( extents.X,  extents.Y), // Top-right
        new Vector2( extents.X, -extents.Y)  // Bottom-right
        };

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var corner in corners)
        {
            // Convert 2D vector to 3D for quaternion rotation
            Vector3 corner3D = new Vector3(corner.X, corner.Y, 0);

            // Rotate using quaternion
            Vector3 rotatedCorner3D = Vector3.Transform(corner3D, rotation);

            // Extract rotated 2D position
            Vector2 rotatedCorner = new Vector2(rotatedCorner3D.X, rotatedCorner3D.Y);

            // Update min/max bounds
            minX = MathF.Min(minX, rotatedCorner.X);
            maxX = MathF.Max(maxX, rotatedCorner.X);
            minY = MathF.Min(minY, rotatedCorner.Y);
            maxY = MathF.Max(maxY, rotatedCorner.Y);
        }

        // Compute new extents (half-width and half-height)
        float newExtX = (maxX - minX) / 2f;
        float newExtY = (maxY - minY) / 2f;

        return new Vector2(newExtX, newExtY);
    }
}

public class Building1404
{
    public List<Model1404> Models { get; set; }
    public List<Cloth1404> Clothes { get; set; }
    public List<File1404> Files { get; internal set; }
    public List<DamageTransform1404> DamageImpacts { get; set; }
    public List<Collision1404> Collisions { get; set; }
    public List<Particle1404> Particles { get; internal set; }

    public List<Ground1404> Grounds { get; set; }
    public Transform BoundingBox { get; set; }
    public Transform MeshBoundingBox { get; set; }
    public List<Transform> IntersectBoxes { get; set; }
    public Polygon BuildBlocker { get; set; }
    public Transform InfoLayer { get; internal set; }
    public List<Polygon> PathBlockers { get; internal set; }
    public Vector TransporterSpawn { get; internal set; }
    public double GroundX { get => Grounds.Count == 0 ? 1 : Grounds.Max(x => Math.Abs(x.Position.X) + Math.Abs(x.RotatedExtendsX)); }
    public double GroundY { get => Grounds.Count == 0 ? 1 : Grounds.Max(x => Math.Abs(x.Position.Y) + Math.Abs(x.RotatedExtendsY)); }
}
