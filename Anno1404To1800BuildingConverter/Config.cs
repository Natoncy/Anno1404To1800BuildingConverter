namespace Anno1404To1800BuildingConverter;

public class Config
{
    /// <summary>
    /// Path to data folder for 1404
    /// </summary>
    public required string DataPath1404 { get; set; }
    /// <summary>
    /// Path to data folder of the mod
    /// </summary>
    public required string DataPathMod { get; set; }
    /// <summary>
    /// Path to data folder for 1800, in case you want to copy it there too
    /// </summary>
    public string? DataPath1800 { get; set; }

    /// <summary>
    /// Part of original path in 1404 to be replaced by 
    /// </summary>
    public required string OriginalPathPart { get; set; }
    /// <summary>
    /// this in the mod folder
    /// </summary>
    public required string ReplacementPathPart { get; set; }

    public required string AnnotexPath { get; set; }
    public required string Evegr2toobjPath { get; set; }
    public required string Rdm4binPath { get; set; }
    public required string BlenderPath { get; set; }
    public required string UpscaylPath { get; set; }
    public required string UpscaylModel { get; set; }

    /// <summary>
    /// Skips upscaling diff textures with upscayl, upscaling takes a lot of time (like double of everything else combined)
    /// </summary>
    public bool SkipUpscale = true;

    /// <summary>
    /// Particle effects are not converted but can be replced with an 1800 particle effect
    /// Key is the path to the original 1404 particle effect
    /// Value is the path to the replacing 1800 particle effect
    /// Missing particles in the dictionary will be logged in console and can be added here
    /// If none are found then the particle will not be added
    /// </summary>
    public Dictionary<string, string?> ParticleMappings { get; set; } = [];

    /// <summary>
    /// Indices of vertices of cloth that should be physically dynamic
    /// Missing vertex indices in the dictionary will be logged in console and can be added here
    /// If none are found then the all vertices will be stiff like a regular model
    /// Select the dynamic vertices then use the copySelectedVertexIndices script in blender to copy the vertex indices
    /// </summary>
    public Dictionary<string, int[]> ClothDynamicVertices { get; set; } = [];
    
    /// <summary>
    /// Paths to the buildings that should be converted
    /// </summary>
    public ConfigPath[] Paths { get; set; } = [];
}
