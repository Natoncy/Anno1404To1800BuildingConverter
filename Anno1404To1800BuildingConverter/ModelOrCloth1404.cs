using System.Numerics;

namespace Anno1404To1800BuildingConverter;

public abstract class ModelOrCloth1404
{
    public string? Model { get; set; }
    public string? Diff { get; set; }
    public string? Norm { get; set; }
    public bool HasMask { get; set; }
    public bool HasDye { get; set; }
    public bool HasHeight { get; set; }
    public Vector Position { get; set; }
    public Quaternion Rotation { get; set; }
}
