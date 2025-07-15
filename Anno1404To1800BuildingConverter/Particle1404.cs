using System.Drawing;
using System.Numerics;

namespace Anno1404To1800BuildingConverter;

public class Particle1404
{
    public string Model { get; set; }
    public List<(Color, double)>? DiffColors { get; set; }
    public Vector Position { get; set; }
    public Quaternion Rotation { get; set; }
    public double Scale { get; set; }

}