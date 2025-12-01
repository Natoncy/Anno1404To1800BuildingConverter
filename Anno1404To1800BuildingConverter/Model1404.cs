namespace Anno1404To1800BuildingConverter;

public class ModelMaterial1404 : ModelOrClothMaterial1404
{
    public bool Ripples { get; set; }
    public bool Water { get; set; }
}

public class Model1404 : ModelOrCloth1404
{
    public double Scale { get; set; }

    public List<ModelMaterial1404> Materials { get; set; } = [];
}
