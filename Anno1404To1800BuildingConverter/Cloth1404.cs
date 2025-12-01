using System.Drawing;

namespace Anno1404To1800BuildingConverter;

public class ClothMaterial1404 : ModelOrClothMaterial1404
{
    public Color? DiffColor { get; set; }
    public Color? SpecularColor { get; set; }
}

public class Cloth1404 : ModelOrCloth1404
{
    public List<ClothMaterial1404> Materials { get; set; } = [];
}
