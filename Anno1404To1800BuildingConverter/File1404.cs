using Quaternion = System.Numerics.Quaternion;

namespace Anno1404To1800BuildingConverter;

public class File1404
{
    public string? Model { get; set; }
    public Vector Position { get; set; }
    public Quaternion Rotation { get; set; }
    public double Scale { get; set; }
}
