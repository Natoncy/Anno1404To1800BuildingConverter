using Assimp;
using KUtility;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Xml;
using Quaternion = System.Numerics.Quaternion;

namespace Anno1404To1800BuildingConverter;

/* 
 * Not supported
 *      Vegetations like palm trees or cypresses
 *      Unit paths
 *      Animations
 */

internal class Program
{
    public static Config Config { get; private set; } = null!;

    static void Main(string[] args)
    {
        var json = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "config.json"));
        Config = JsonConvert.DeserializeObject<Config>(json)!;

        var defaultCulture = new CultureInfo("en-US");
        Thread.CurrentThread.CurrentCulture = defaultCulture;
        Thread.CurrentThread.CurrentUICulture = defaultCulture;

        Console.WriteLine($"CONVERTER STARTED");


        foreach (var path in Config.Paths)
        {
            if (path?.Path == null)
            {
                return;
            }

            if (!path.Path.EndsWith(".cfg"))
            {
                ConvertAllInFolder(path.Path, path.IsProp, path.DoNotAdjustToTerrainHeight);
            }
            else
            {
                ConvertBuilding(path.Path, path.IsProp, path.DoNotAdjustToTerrainHeight);
            }
        }

        if (!string.IsNullOrWhiteSpace(Config.DataPath1800))
        {
            CopyDirectory( // Optional: Copies all generated files into data folder for blender import
                @$"{Config.DataPathMod}{Config.ReplacementPathPart}",
                @$"{Config.DataPath1800}{Config.ReplacementPathPart}");
        }

        Console.WriteLine();
        Console.WriteLine($"CONVERTER COMPLETED");
    }

    private static void ConvertAllInFolder(string folderPath, bool isProp, bool doNotAdjustToTerrainHeight)
    {
        folderPath = Paths.CreateFrom1404InternalPath(folderPath, isProp).FilePath1404;
        if (Directory.Exists(folderPath))
        {
            string[] cfgPaths = Directory.GetFiles(folderPath, "*.cfg", SearchOption.AllDirectories);

            foreach (string cfgPath in cfgPaths)
            {
                ConvertBuilding(Paths.CreateFrom1404AbsoluteFilePath(cfgPath, isProp), isProp, doNotAdjustToTerrainHeight);
            }
        }
    }

    private static void ConvertBuilding(string cfgPath, bool isProp, bool doNotAdjustToTerrainHeight)
    {
        ConvertBuilding(Paths.CreateFrom1404InternalPath(cfgPath, isProp), isProp, doNotAdjustToTerrainHeight);
    }

    private static void ConvertBuilding(Paths paths, bool isProp, bool doNotAdjustToTerrainHeight)
    {
        var filename = Path.GetFileNameWithoutExtension(paths.FilePath1404);

        var building = LoadBuilding(paths, isProp);
        var mainFolder = Path.GetDirectoryName(paths.FilePath1800)!;
        var mapsFolder = Path.Combine(mainFolder, "maps");
        var rdmFolder = Path.Combine(mainFolder, "rdm");

        Directory.CreateDirectory(mapsFolder);
        Directory.CreateDirectory(rdmFolder);

        foreach (var ground in building.Grounds)
        {
            ConvertGroundMap(ground, mapsFolder);
        }

        foreach (var diff in building.Models.Where(x => !x.Water).GroupBy(x => x.Diff))
        {
            ConvertModelMap(diff.Key, mapsFolder, false, diff.ToList(), isProp);
        }

        foreach (var norm in building.Models.Where(x => !x.Water).GroupBy(x => x.Norm))
        {
            ConvertModelMap(norm.Key, mapsFolder, true, norm.ToList(), isProp);
        }

        foreach (var diff in building.Clothes.GroupBy(x => x.Diff))
        {
            ConvertModelMap(diff.Key, mapsFolder, false, diff.ToList(), isProp);
        }

        foreach (var norm in building.Clothes.GroupBy(x => x.Norm))
        {
            ConvertModelMap(norm.Key, mapsFolder, true, norm.ToList(), isProp);
        }

        var vertices = new List<Vector>();
        foreach (var model in building.Models.GroupBy(x => x.Model).Select(x => x.Key))
        {
            vertices.AddRange(ConvertModelMesh(model, rdmFolder));
        }
        foreach (var model in building.Clothes.GroupBy(x => x.Model).Select(x => x.Key))
        {
            vertices.AddRange(ConvertClothMesh(model, rdmFolder));
        }

        CalculateBuildingValuesFromVertices(building, vertices);

        GenerateCfgFile(filename, mainFolder, building, isProp, doNotAdjustToTerrainHeight);
        GenerateIfoFile(filename, mainFolder, building, isProp);

        if (!isProp)
        {
            Console.WriteLine(@$"<Item>
    <Filename>{paths.InternalPath1800.Replace("\\", "/")}</Filename>
</Item>");
        }
    }

    private static void GenerateCfgFile(string filename, string path, Building1404 building, bool isProp, bool doNotAdjustToTerrainHeight)
    {
        var relativePathMain = Paths.CreateInternalFrom1800AbsoluteFilePath(path).Replace("\\", "/") + "/";
        var relativePathMaps = relativePathMain + "maps/";
        var relativePathRdm = relativePathMain + "rdm/";

        var cfg = @$"
<Config>
	<ConfigType>MAIN</ConfigType>
	<RenderPropertyFlags>134414464</RenderPropertyFlags>
	<SimplygonFlags>0</SimplygonFlags>
	<Center.x>{building.BoundingBox.Position.X}</Center.x>
	<Center.y>{building.BoundingBox.Position.Y}</Center.y>
	<Center.z>{building.BoundingBox.Position.Z}</Center.z>
	<Extent.x>{building.BoundingBox.Extents.X}</Extent.x>
	<Extent.y>{building.BoundingBox.Extents.Y}</Extent.y>
	<Extent.z>{building.BoundingBox.Extents.Z}</Extent.z>
	<Radius>{building.BoundingBox.Extents.Length()}</Radius>
	<Mass>1.000000</Mass>
	<Drag>1.000000</Drag>
	<MeshCenter.x>{building.MeshBoundingBox.Position.X}</MeshCenter.x>
	<MeshCenter.y>{building.MeshBoundingBox.Position.Y}</MeshCenter.y>
	<MeshCenter.z>{building.MeshBoundingBox.Position.Z}</MeshCenter.z>
	<MeshExtent.x>{building.MeshBoundingBox.Extents.X}</MeshExtent.x>
	<MeshExtent.y>{building.MeshBoundingBox.Extents.Y}</MeshExtent.y>
	<MeshExtent.z>{building.MeshBoundingBox.Extents.Z}</MeshExtent.z>
	<MeshRadius>{building.MeshBoundingBox.Extents.Length()}</MeshRadius>
	<Sequences>
	</Sequences>
";
        if (!isProp)
        {
            cfg += @$"
	<Decals>
";
            foreach (var ground in building.Grounds)
            {
                var groundDiffPath = relativePathMaps + ground.Diff.Split("\\").Last();

                cfg += @$"
		<Config>
			<ConfigType>DECAL</ConfigType>
			<Type>Terrain</Type>
			<DetailTemplateFileName>data/graphics/props/terrain_props/vegetation/grass/decal_grass_single_01.prp</DetailTemplateFileName>
			<OnStreets>1</OnStreets>
			<FadeDuration>1000</FadeDuration>
			<HasDetails>1</HasDetails>
			<DetailMeshEmpty>0</DetailMeshEmpty>
			<DetailAlphaRef>0.800000</DetailAlphaRef>
			<DetailDensity>2.000000</DetailDensity>
			<DetailColorKey>
				<x>0.467000</x>
				<y>0.557000</y>
				<z>0.137000</z>
			</DetailColorKey>
			<DetailColorTolerance>
				<x>0.100000</x>
				<y>0.300000</y>
				<z>0.400000</z>
			</DetailColorTolerance>
			<DetailScaleRange>
				<x>0.300000</x>
				<y>0.500000</y>
			</DetailScaleRange>
			<Name />
			<Extents.x>{ground.ExtendsX}</Extents.x>
			<Extents.y>0.250000</Extents.y>
			<Extents.z>{ground.ExtendsY}</Extents.z>
			<TexCoords.x>0</TexCoords.x>
			<TexCoords.y>0</TexCoords.y>
			<TexCoords.z>1</TexCoords.z>
			<TexCoords.w>1</TexCoords.w>
            <Transformer>
                <Config>
                    <ConfigType>ORIENTATION_TRANSFORM</ConfigType>
                    <Conditions>0</Conditions>
                    <Position.x>{ground.Position.X}</Position.x>
                    <Position.y>{ground.Position.Y}</Position.y>
                    <Position.z>{ground.Position.Z}</Position.z>
                    <Rotation.x>{ground.Rotation.X}</Rotation.x>
                    <Rotation.y>{ground.Rotation.Y}</Rotation.y>
                    <Rotation.z>{ground.Rotation.Z}</Rotation.z>
                    <Rotation.w>{ground.Rotation.W}</Rotation.w>
                    <Scale>1</Scale>
                </Config>
            </Transformer>
			<Materials>
				<Config>
					<ConfigType>MATERIAL</ConfigType>
					<VertexFormat>P3f_N3f_G3f_T2f_T1f_T1f_T1f</VertexFormat>
					<Common>Common</Common>
					<TerrainAdaption>TerrainAdaption</TerrainAdaption>
					<Environment>Environment</Environment>
					<Glow>Glow</Glow>
					<ShaderID>1</ShaderID>
					<NumBonesPerVertex>0</NumBonesPerVertex>
					<PARALLAX_MAPPING_ENABLED>1</PARALLAX_MAPPING_ENABLED>
					<VERTEX_COLORED_TERRAIN_ADAPTION>0</VERTEX_COLORED_TERRAIN_ADAPTION>
					<ABSOLUTE_TERRAIN_ADAPTION>0</ABSOLUTE_TERRAIN_ADAPTION>
					<cUseLocalEnvironmentBox>1</cUseLocalEnvironmentBox>
					<DisableReviveDistance>0</DisableReviveDistance>
					<cGlossinessFactor>0.917000</cGlossinessFactor>
					<cOpacity>1.000000</cOpacity>
					<cParallaxScale>1.000000</cParallaxScale>
					<cEnvironmentBoundingBox.x>0.000000</cEnvironmentBoundingBox.x>
					<cEnvironmentBoundingBox.y>0.000000</cEnvironmentBoundingBox.y>
					<cEnvironmentBoundingBox.z>0.000000</cEnvironmentBoundingBox.z>
					<cEnvironmentBoundingBox.w>4.000000</cEnvironmentBoundingBox.w>
					<METALLIC_TEX_ENABLED>0</METALLIC_TEX_ENABLED>
					<cUseTerrainTinting>0</cUseTerrainTinting>
					<DIFFUSE_ENABLED>1</DIFFUSE_ENABLED>
					<NORMAL_ENABLED>0</NORMAL_ENABLED>
					<HEIGHT_MAP_ENABLED>0</HEIGHT_MAP_ENABLED>
					<SELF_SHADOWING_ENABLED>0</SELF_SHADOWING_ENABLED>
					<ADJUST_TO_TERRAIN_HEIGHT>0</ADJUST_TO_TERRAIN_HEIGHT>
					<GLOW_ENABLED>1</GLOW_ENABLED>
					<Name>ground_material</Name>
					<cModelDiffTex>{groundDiffPath}</cModelDiffTex>
					<cHeightMap>data/graphics/buildings/effects/default_height.png</cHeightMap>
					<cDiffuseColor.r>0.997000</cDiffuseColor.r>
					<cDiffuseColor.g>0.997000</cDiffuseColor.g>
					<cDiffuseColor.b>0.997000</cDiffuseColor.b>
					<cEmissiveColor.r>2.000000</cEmissiveColor.r>
					<cEmissiveColor.g>2.000000</cEmissiveColor.g>
					<cEmissiveColor.b>2.000000</cEmissiveColor.b>
					<DIFFUSE_ENABLED>1</DIFFUSE_ENABLED>
					<NORMAL_ENABLED>0</NORMAL_ENABLED>
					<METALLIC_TEX_ENABLED>0</METALLIC_TEX_ENABLED>
					<SEPARATE_AO_TEXTURE>0</SEPARATE_AO_TEXTURE>
					<HEIGHT_MAP_ENABLED>0</HEIGHT_MAP_ENABLED>
					<NIGHT_GLOW_ENABLED>0</NIGHT_GLOW_ENABLED>
					<DYE_MASK_ENABLED>0</DYE_MASK_ENABLED>
				</Config>
			</Materials>
		</Config>
";
            }
            cfg += @$"
	</Decals>
";
        }
        cfg += @$"
    <Files>
";
        foreach (var file in building.Files)
        {
            var name = file.Model?.Split("\\")?.LastOrDefault()?.Replace(".cfg", "");

            cfg += @$"
        <Config>
            <ConfigType>FILE</ConfigType>
            <Transformer>
                <Config>
                    <ConfigType>ORIENTATION_TRANSFORM</ConfigType>
                    <Conditions>0</Conditions>
                    <Position.x>{file.Position.X}</Position.x>
					<Position.y>{file.Position.Y}</Position.y>
					<Position.z>{file.Position.Z}</Position.z>
					<Rotation.x>{file.Rotation.X}</Rotation.x>
					<Rotation.y>{file.Rotation.Y}</Rotation.y>
					<Rotation.z>{file.Rotation.Z}</Rotation.z>
					<Rotation.w>{file.Rotation.W}</Rotation.w>
					<Scale>{file.Scale}</Scale>
                </Config>
            </Transformer>
            <Name>{name}</Name>
            <FileName>{file.Model}</FileName>
            <AdaptTerrainHeight>1</AdaptTerrainHeight>
        </Config>
";
        }
        cfg += @$"
    </Files>
	<Models>
";
        var i = 0;
        foreach (var model in building.Models)
        {
            var modelPath = model.Model == null ? null : relativePathRdm + model.Model.Split("\\").Last().Replace(".gr2", ".rdm", StringComparison.OrdinalIgnoreCase);

            var diffPath = model.Diff == null ? null : relativePathMaps + model.Diff.Split("\\").Last();
            var normPath = model.Norm == null ? null : relativePathMaps + model.Norm.Split("\\").Last();

            var pathTemplate = diffPath?.Replace("_diff", "_{0}") ?? diffPath?.ReplaceNormal("{0}");

            var maskPath = !model.HasMask || pathTemplate == null ? null : string.Format(pathTemplate, "mask");
            var dyePath = !model.HasDye || pathTemplate == null ? null : string.Format(pathTemplate, "dye");
            var metalPath = pathTemplate == null ? null : string.Format(pathTemplate, "metal");
            var heightPath = !model.HasHeight || pathTemplate == null ? null : string.Format(pathTemplate, "height");

            cfg += @$"
		<Config>
			<ConfigType>MODEL</ConfigType>
			<IgnoreRuinState>0</IgnoreRuinState>
			<FileName>{modelPath}</FileName>
			<MaterialLODInfos>
				<i>
					<Indices>
						<i>0</i>
					</Indices>
				</i>
				<i>
					<Indices>
						<i>0</i>
					</Indices>
				</i>
				<i>
					<Indices>
						<i>0</i>
					</Indices>
				</i>
				<i>
					<Indices>
						<i>0</i>
					</Indices>
				</i>
				<i>
					<Indices>
						<i>0</i>
					</Indices>
				</i>
			</MaterialLODInfos>
			<Name></Name>
			<Transformer>
				<Config>
					<ConfigType>ORIENTATION_TRANSFORM</ConfigType>
					<Position.x>{model.Position.X}</Position.x>
					<Position.y>{model.Position.Y}</Position.y>
					<Position.z>{model.Position.Z}</Position.z>
					<Rotation.x>{model.Rotation.X}</Rotation.x>
					<Rotation.y>{model.Rotation.Y}</Rotation.y>
					<Rotation.z>{model.Rotation.Z}</Rotation.z>
					<Rotation.w>{model.Rotation.W}</Rotation.w>
					<Scale>{model.Scale}</Scale>
				</Config>
			</Transformer>
			<Materials>
";
            if (model.Water)
            {
                cfg += $@"
				<Config>
                    <ConfigType>MATERIAL</ConfigType>
                    <Name>02_water</Name>
                    <ShaderID>7</ShaderID>
                    <VertexFormat>P4h_N4b_G4b_B4b_T2h</VertexFormat>
                    <NumBonesPerVertex>0</NumBonesPerVertex>
                    <cFlowNormalIntensity>0.708000</cFlowNormalIntensity>
                    <cWaterTexScale>2.449000</cWaterTexScale>
                    <cWaterDistortion>0.766000</cWaterDistortion>
                    <cWaterDepthFade>0.000000</cWaterDepthFade>
                    <cWaterDepth>0.167000</cWaterDepth>
                    <cBaseReflectivity>0.507000</cBaseReflectivity>
                    <cWaterFoam>0.308000</cWaterFoam>
                    <VERTEX_COLOR_FOAM>0</VERTEX_COLOR_FOAM>
                    <FlowMap></FlowMap>
                    <FLOW_MAP_ENABLED>1</FLOW_MAP_ENABLED>
                    <cWaterFlowTex>data/graphics/effects/animated/still_water_flowmap.psd</cWaterFlowTex>
                    <cFlowSpeed>0.306000</cFlowSpeed>
                    <OceanWaveTexture></OceanWaveTexture>
                    <USE_SMALL_WAVE_TEXTURE>1</USE_SMALL_WAVE_TEXTURE>
                    <cSmallWaveTexScale>0.843000</cSmallWaveTexScale>
                    <DetailMap></DetailMap>
                    <DETAIL_MAP_ENABLED>0</DETAIL_MAP_ENABLED>
                    <cWaterDetailTex>data/graphics/effects/water/algae.psd</cWaterDetailTex>
                    <cWaterDetailNormTex>data/graphics/effects/water/algae_norm.psd</cWaterDetailNormTex>
                    <Common>Common</Common>
                    <DIFFUSE_ENABLED>0</DIFFUSE_ENABLED>
                    <cModelDiffTex>data/graphics/effects/default_model_diffuse.png</cModelDiffTex>
                    <NORMAL_ENABLED>1</NORMAL_ENABLED>
                    <cModelNormalTex>data/graphics/effects/water/water_plane_01_norm.psd</cModelNormalTex>
                    <cDiffuseColor.r>0.287625</cDiffuseColor.r>
                    <cDiffuseColor.g>1.373000</cDiffuseColor.g>
                    <cDiffuseColor.b>1.210194</cDiffuseColor.b>
                    <cGlossinessFactor>1.000000</cGlossinessFactor>
                    <TerrainAdaption>TerrainAdaption</TerrainAdaption>
                    <ADJUST_TO_TERRAIN_HEIGHT>{(doNotAdjustToTerrainHeight ? "0" : "1")}</ADJUST_TO_TERRAIN_HEIGHT>
                    <VERTEX_COLORED_TERRAIN_ADAPTION>0</VERTEX_COLORED_TERRAIN_ADAPTION>
                    <ABSOLUTE_TERRAIN_ADAPTION>0</ABSOLUTE_TERRAIN_ADAPTION>
                    <Environment>Environment</Environment>
                    <cUseLocalEnvironmentBox>1</cUseLocalEnvironmentBox>
                    <cEnvironmentBoundingBox.x>0.000000</cEnvironmentBoundingBox.x>
                    <cEnvironmentBoundingBox.y>0.000000</cEnvironmentBoundingBox.y>
                    <cEnvironmentBoundingBox.z>0.000000</cEnvironmentBoundingBox.z>
                    <cEnvironmentBoundingBox.w>4.000000</cEnvironmentBoundingBox.w>
                    <DisableReviveDistance>0</DisableReviveDistance>
                </Config>
";
            }
            else
            {
                cfg += $@"
				<Config>
					<ConfigType>MATERIAL</ConfigType>
					<VertexFormat>P4h_N4b_G4b_B4b_T2h</VertexFormat>
					<Common>Common</Common>
					<TerrainAdaption>TerrainAdaption</TerrainAdaption>
					<Environment>Environment</Environment>
					{(model.Ripples ? "<WindRipples>WindRipples</WindRipples>" : "<WindRipples />")}
					<ShaderID>8</ShaderID>
					<NumBonesPerVertex>0</NumBonesPerVertex>
					<PARALLAX_MAPPING_ENABLED>1</PARALLAX_MAPPING_ENABLED>
					<VERTEX_COLORED_TERRAIN_ADAPTION>0</VERTEX_COLORED_TERRAIN_ADAPTION>
					<ABSOLUTE_TERRAIN_ADAPTION>0</ABSOLUTE_TERRAIN_ADAPTION>
					<cUseLocalEnvironmentBox>1</cUseLocalEnvironmentBox>
					<WIND_RIPPLES_ENABLED>{(model.Ripples ? "1" : "0")}</WIND_RIPPLES_ENABLED>
					<DisableReviveDistance>0</DisableReviveDistance>
					<cTexScrollSpeed>0.000000</cTexScrollSpeed>
					<cParallaxScale>1.000000</cParallaxScale>
					<cEnvironmentBoundingBox.x>0.000000</cEnvironmentBoundingBox.x>
					<cEnvironmentBoundingBox.y>0.000000</cEnvironmentBoundingBox.y>
					<cEnvironmentBoundingBox.z>0.000000</cEnvironmentBoundingBox.z>
					<cEnvironmentBoundingBox.w>4.000000</cEnvironmentBoundingBox.w>
					<cWindRippleTex>data/graphics/effects/cloth/clothripple01_1404.psd</cWindRippleTex>
					<cWindRippleTiling>0.208000</cWindRippleTiling>
					<cWindRippleSpeed>1.063000</cWindRippleSpeed>
					<cWindRippleNormalIntensity>2.200000</cWindRippleNormalIntensity>
					<cWindRippleMeshIntensity>0.250000</cWindRippleMeshIntensity>
					<cUseTerrainTinting>0</cUseTerrainTinting>
					<SEPARATE_AO_TEXTURE>0</SEPARATE_AO_TEXTURE>
					<SELF_SHADOWING_ENABLED>1</SELF_SHADOWING_ENABLED>
					<WATER_CUTOUT_ENABLED>0</WATER_CUTOUT_ENABLED>
					<ADJUST_TO_TERRAIN_HEIGHT>{(doNotAdjustToTerrainHeight ? "0" : "1")}</ADJUST_TO_TERRAIN_HEIGHT>
					<Glow>{(maskPath != null ? "Glow" : "")}</Glow>
					<GLOW_ENABLED>{(maskPath != null ? "1" : "0")}</GLOW_ENABLED>
					<Name>building_material</Name>
					<DIFFUSE_ENABLED>{(diffPath != null ? "1" : "0")}</DIFFUSE_ENABLED>
					<cModelDiffTex>{diffPath}</cModelDiffTex>
					<NORMAL_ENABLED>{(normPath != null ? "1" : "0")}</NORMAL_ENABLED>
					<cModelNormalTex>{normPath}</cModelNormalTex>
					<METALLIC_TEX_ENABLED>{(metalPath != null ? "1" : "0")}</METALLIC_TEX_ENABLED>
                    <cModelMetallicTex>{metalPath}</cModelMetallicTex>
					<NIGHT_GLOW_ENABLED>{(maskPath != null ? "1" : "0")}</NIGHT_GLOW_ENABLED>
                    <cNightGlowMap>{maskPath}</cNightGlowMap>
					<DYE_MASK_ENABLED>{(dyePath != null ? "1" : "0")}</DYE_MASK_ENABLED>
                    <cDyeMask>{dyePath}</cDyeMask>
					<HEIGHT_MAP_ENABLED>{(heightPath != null ? "1" : "0")}</HEIGHT_MAP_ENABLED>
                    <cHeightMap>{heightPath}</cHeightMap>
					<SEPARATE_AO_TEXTURE>0</SEPARATE_AO_TEXTURE>
					<cDiffuseColor.r>1</cDiffuseColor.r>
					<cDiffuseColor.g>1</cDiffuseColor.g>
					<cDiffuseColor.b>1</cDiffuseColor.b>
					<cEmissiveColor.r>0.8</cEmissiveColor.r>
					<cEmissiveColor.g>0.8</cEmissiveColor.g>
					<cEmissiveColor.b>0.8</cEmissiveColor.b>
				</Config>
";
            }

            cfg += $@"
			</Materials>
		</Config>
";
            i++;
        }

        cfg += @$"
	</Models>
	<PropContainers>
		<Config>
			<ConfigType>PROPCONTAINER</ConfigType>
			<VariationEnabled>0</VariationEnabled>
			<VariationProbability>100</VariationProbability>
			<AllowYScale>0</AllowYScale>
			<AdaptTerrainHeight>0</AdaptTerrainHeight>
			<Transformer>
				<Config>
					<ConfigType>ORIENTATION_TRANSFORM</ConfigType>
					<Conditions>0</Conditions>
					<Scale>1</Scale>
					<Position.x>0</Position.x>
					<Position.y>0</Position.y>
					<Position.z>0</Position.z>
					<Rotation.x>0</Rotation.x>
					<Rotation.y>0</Rotation.y>
					<Rotation.z>0</Rotation.z>
					<Rotation.w>1</Rotation.w>
					<Scale.x>1</Scale.x>
					<Scale.y>1</Scale.y>
					<Scale.z>1</Scale.z>
				</Config>
			</Transformer>
			<Name />
		</Config>
	</PropContainers>
    <Clothes>
";
        foreach (var cloth in building.Clothes)
        {
            var modelPath = cloth.Model == null ? null : relativePathRdm + cloth.Model.Split("\\").Last().Replace(".gr2", ".rdm", StringComparison.OrdinalIgnoreCase);

            var diffPath = cloth.Diff == null ? null : relativePathMaps + cloth.Diff.Split("\\").Last();
            var normPath = cloth.Norm == null ? null : relativePathMaps + cloth.Norm.Split("\\").Last();

            var pathTemplate = diffPath?.Replace("_diff", "_{0}") ?? diffPath?.ReplaceNormal("{0}");

            var maskPath = !cloth.HasMask || pathTemplate == null ? null : string.Format(pathTemplate, "mask");
            var dyePath = !cloth.HasDye || pathTemplate == null ? null : string.Format(pathTemplate, "dye");
            var heightPath = !cloth.HasHeight || pathTemplate == null ? null : string.Format(pathTemplate, "height");

            cfg += @$"
        <Config>
            <ConfigType>CLOTH</ConfigType>
            <Transformer>
                <Config>
                    <ConfigType>ORIENTATION_TRANSFORM</ConfigType>
                    <Conditions>0</Conditions>
					<Position.x>{cloth.Position.X}</Position.x>
					<Position.y>{cloth.Position.Y}</Position.y>
					<Position.z>{cloth.Position.Z}</Position.z>
					<Rotation.x>{cloth.Rotation.X}</Rotation.x>
					<Rotation.y>{cloth.Rotation.Y}</Rotation.y>
					<Rotation.z>{cloth.Rotation.Z}</Rotation.z>
					<Rotation.w>{cloth.Rotation.W}</Rotation.w>
                    <Scale>1</Scale>
                </Config>
            </Transformer>
            <Materials>
                <Config>
                    <ConfigType>MATERIAL</ConfigType>
                    <Name></Name>
                    <ShaderID>0</ShaderID>
                    <VertexFormat>P3f_N3b,T2f</VertexFormat>
                    <NumBonesPerVertex>0</NumBonesPerVertex>
                    <DIFFUSE_ENABLED>1</DIFFUSE_ENABLED>
                    <cClothDiffuseTex>{diffPath}</cClothDiffuseTex>
                    <NORMAL_ENABLED>1</NORMAL_ENABLED>
                    <cClothNormalTex>{normPath}</cClothNormalTex>
					<NIGHT_GLOW_ENABLED>{(maskPath != null ? "1" : "0")}</NIGHT_GLOW_ENABLED>
                    <cNightGlowMap>{maskPath}</cNightGlowMap>
					<DYE_MASK_ENABLED>{(dyePath != null ? "1" : "0")}</DYE_MASK_ENABLED>
                    <cClothDyeMask>{dyePath}</cClothDyeMask>
					<HEIGHT_MAP_ENABLED>{(heightPath != null ? "1" : "0")}</HEIGHT_MAP_ENABLED>
                    <cHeightMap>{heightPath}</cHeightMap>
                    <cDiffuseColor.r>{(cloth.DiffColor?.R ?? 1) / 255.0}</cDiffuseColor.r>
                    <cDiffuseColor.g>{(cloth.DiffColor?.G ?? 1) / 255.0}</cDiffuseColor.g>
                    <cDiffuseColor.b>{(cloth.DiffColor?.B ?? 1) / 255.0}</cDiffuseColor.b>
                    <cSpecularColor.r>{(cloth.SpecularColor?.R ?? 1) / 255.0}</cSpecularColor.r>
                    <cSpecularColor.g>{(cloth.SpecularColor?.G ?? 1) / 255.0}</cSpecularColor.g>
                    <cSpecularColor.b>{(cloth.SpecularColor?.B ?? 1) / 255.0}</cSpecularColor.b>
                    <cGlossinessFactor>0.200000</cGlossinessFactor>
                    <cAlphaRef>0.100000</cAlphaRef>
                    <Atlas>Atlas</Atlas>
                    <LOGO_ATLAS_ENABLED>0</LOGO_ATLAS_ENABLED>
                    <INVERSE_LOGO_COLORING>0</INVERSE_LOGO_COLORING>
                    <RimEffect>RimEffect</RimEffect>
                    <cRimColor.r>0.479000</cRimColor.r>
                    <cRimColor.g>0.479000</cRimColor.g>
                    <cRimColor.b>0.479000</cRimColor.b>
                    <cRimIntensity>0.100000</cRimIntensity>
                    <Ripples>Ripples</Ripples>
                    <RIPPLES_ENABLED>1</RIPPLES_ENABLED>
                    <cRippleTiling>0.500000</cRippleTiling>
                    <cRippleSpeed>1.000000</cRippleSpeed>
                    <cRippleNormalIntensity>0.200000</cRippleNormalIntensity>
                    <TerrainAdaption>TerrainAdaption</TerrainAdaption>
                    <ADJUST_TO_TERRAIN_HEIGHT>0</ADJUST_TO_TERRAIN_HEIGHT>
                    <DisableReviveDistance>0</DisableReviveDistance>
                </Config>
            </Materials>
            <Name></Name>
            <FileName>{modelPath}</FileName>
            <UniqueSimulation>0</UniqueSimulation>
            <AllowLocalWind>1</AllowLocalWind>
            <LocalWindDirection>0.000000</LocalWindDirection>
            <WindStrength>1.000000</WindStrength>
            <Gravity>0.100000</Gravity>
            <LineSize>1.000000</LineSize>
        </Config>
";
        }
        cfg += @$"
    </Clothes>
    <Particles>
";
        foreach (var particle in building.Particles)
        {
            var color = particle.DiffColors?.OrderByDescending(x => x.Item2 == 4).FirstOrDefault().Item1;

            cfg += @$"
        <Config>
            <ConfigType>PARTICLE</ConfigType>
            <Transformer>
                <Config>
                    <ConfigType>ORIENTATION_TRANSFORM</ConfigType>
                    <Conditions>0</Conditions>
                    <Position.x>{particle.Position.X}</Position.x>
                    <Position.y>{particle.Position.Y}</Position.y>
                    <Position.z>{particle.Position.Z}</Position.z>
                    <Rotation.x>{particle.Rotation.X}</Rotation.x>
                    <Rotation.y>{particle.Rotation.Y}</Rotation.y>
                    <Rotation.z>{particle.Rotation.Z}</Rotation.z>
                    <Rotation.w>{particle.Rotation.W}</Rotation.w>
                    <Scale>{particle.Scale}</Scale>
                </Config>
            </Transformer>
            <Name></Name>
            <FileName>{particle.Model}</FileName>
            <TimeScale>3.300000</TimeScale>
            <WindImpact>0.000000</WindImpact>
            <ReceiveShadows>0</ReceiveShadows>
            <SoftParticlesEnabled>0</SoftParticlesEnabled>
            <EarlyPass>0</EarlyPass>
            <IsEmitterBound>1</IsEmitterBound>
            <UseDepthBias>0</UseDepthBias>
            <AdaptTerrainHeight>1</AdaptTerrainHeight>
            <DelayFadeOut>0</DelayFadeOut>
            <AlwaysVisible>0</AlwaysVisible>
            <DarkenAtNight>1</DarkenAtNight>
            <Color>
                <x>{(color?.R ?? 1) / 255.0}</x>
                <x>{(color?.G ?? 1) / 255.0}</x>
                <x>{(color?.B ?? 1) / 255.0}</x>
            </Color>
            <Alpha>{(color?.A ?? 1) / 255.0}</Alpha>
            <TextureAtlas></TextureAtlas>
        </Config>
";
        }
        cfg += @$"
    </Particles>
    <Collisions>
";
        foreach (var collision in building.Collisions)
        {
            cfg += @$"
        <Config>
            <ConfigType>COLLISION</ConfigType>
            <Transformer>
                <Config>
                    <ConfigType>ORIENTATION_TRANSFORM</ConfigType>
                    <Conditions>0</Conditions>
                    <Position.x>{collision.Position.X}</Position.x>
					<Position.y>{collision.Position.Y}</Position.y>
					<Position.z>{collision.Position.Z}</Position.z>
					<Rotation.x>{collision.Rotation.X}</Rotation.x>
					<Rotation.y>{collision.Rotation.Y}</Rotation.y>
					<Rotation.z>{collision.Rotation.Z}</Rotation.z>
					<Rotation.w>{collision.Rotation.W}</Rotation.w>
					<Scale>{collision.Scale}</Scale>
                </Config>
            </Transformer>
            <Name></Name>
            <CollisionType>{collision.Type}</CollisionType>
        </Config>
";
        }
        cfg += @$"
    </Collisions>
</Config>
";

        File.WriteAllText(Path.Combine(path, $"{filename}.cfg"), cfg);
    }

    private static void GenerateIfoFile(string filename, string path, Building1404 building, bool isProp)
    {
        var ifo = @$"
<Info>
	<DisableFeedbackArea>0</DisableFeedbackArea>
	<BoundingBox>
		<Name>BoundingBox</Name>
		<Position>
			<xf>{building.BoundingBox.Position.X}</xf>
			<yf>{building.BoundingBox.Position.Y}</yf>
			<zf>{building.BoundingBox.Position.Z}</zf>
		</Position>
		<Rotation>
			<xf>{building.BoundingBox.Rotation.X}</xf>
			<yf>{building.BoundingBox.Rotation.Y}</yf>
			<zf>{building.BoundingBox.Rotation.Z}</zf>
			<wf>{building.BoundingBox.Rotation.W}</wf>
		</Rotation>
		<Extents>
			<xf>{building.BoundingBox.Extents.X}</xf>
			<yf>{building.BoundingBox.Extents.Y}</yf>
			<zf>{building.BoundingBox.Extents.Z}</zf>
		</Extents>
	</BoundingBox>
";
        if (!isProp)
        {
            ifo += @$"
	<BuildBlocker>
";
            foreach (var vert in building.BuildBlocker)
            {
                ifo += @$"
		<Position>
			<xf>{vert.X}</xf>
			<zf>{vert.Z}</zf>
		</Position>
";
            }
            ifo += @$"
	</BuildBlocker>
";
        }
        var i = 0;
        foreach (var damageImpact in building.DamageImpacts)
        {
            ifo += @$"
	<Dummy>
		<Name>DamageImpact{i}</Name>
		<Position>
			<xf>{damageImpact.Position.X}</xf>
			<yf>{damageImpact.Position.Y}</yf>
			<zf>{damageImpact.Position.Z}</zf>
		</Position>
		<Rotation>
			<xf>{damageImpact.Rotation.X}</xf>
			<yf>{damageImpact.Rotation.Y}</yf>
			<zf>{damageImpact.Rotation.Z}</zf>
			<wf>{damageImpact.Rotation.W}</wf>
		</Rotation>
		<Extents>
			<xf>{damageImpact.Scale}</xf>
			<yf>{damageImpact.Scale}</yf>
			<zf>{damageImpact.Scale}</zf>
		</Extents>
	</Dummy>
";
            i++;
        }
        if (!isProp)
        {
            ifo += @$"
	<Dummy>
		<Name>infolayer</Name>
		<Position>
			<xf>{building.InfoLayer.Position.X}</xf>
			<yf>{building.InfoLayer.Position.Y}</yf>
			<zf>{building.InfoLayer.Position.Z}</zf>
		</Position>
		<Rotation>
			<xf>{building.InfoLayer.Rotation.X}</xf>
			<yf>{building.InfoLayer.Rotation.Y}</yf>
			<zf>{building.InfoLayer.Rotation.Z}</zf>
			<wf>{building.InfoLayer.Rotation.W}</wf>
		</Rotation>
		<Extents>
			<xf>{building.InfoLayer.Extents.X}</xf>
			<yf>{building.InfoLayer.Extents.Y}</yf>
			<zf>{building.InfoLayer.Extents.Z}</zf>
		</Extents>
	</Dummy>
";
            ifo += @$"
	<Dummy>
		<Name>transporter_spawn</Name>
		<Position>
			<xf>{building.TransporterSpawn.X}</xf>
			<yf>{building.TransporterSpawn.Y}</yf>
			<zf>{building.TransporterSpawn.Z}</zf>
		</Position>
		<Rotation>
			<xf>0.000000</xf>
			<yf>0.000000</yf>
			<zf>0.000000</zf>
			<wf>1.000000</wf>
		</Rotation>
		<Extents>
			<xf>0.100000</xf>
			<yf>0.100000</yf>
			<zf>0.100000</zf>
		</Extents>
	</Dummy>
";
        }
        foreach (var intersectBox in building.PathBlockers)
        {
            ifo += @$"
	<FeedbackBlocker>
";
            foreach (var vert in intersectBox)
            {
                ifo += @$"
		<Position>
			<xf>{vert.X}</xf>
			<zf>{vert.Z}</zf>
		</Position>
";
            }
            ifo += @$"
	</FeedbackBlocker>
";
        }
        i = 0;
        foreach (var intersectBox in building.IntersectBoxes)
        {
            ifo += @$"
	<IntersectBox>
		<Name>Hitbox_{i}</Name>
		<Position>
			<xf>{intersectBox.Position.X}</xf>
			<yf>{intersectBox.Position.Y}</yf>
			<zf>{intersectBox.Position.Z}</zf>
		</Position>
		<Rotation>
			<xf>{intersectBox.Rotation.X}</xf>
			<yf>{intersectBox.Rotation.Y}</yf>
			<zf>{intersectBox.Rotation.Z}</zf>
			<wf>{intersectBox.Rotation.W}</wf>
		</Rotation>
		<Extents>
			<xf>{intersectBox.Extents.X}</xf>
			<yf>{intersectBox.Extents.Y}</yf>
			<zf>{intersectBox.Extents.Z}</zf>
		</Extents>
	</IntersectBox>
";
            i++;
        }
        ifo += @$"
	<MeshBoundingBox>
		<Name>MeshBoundingBox</Name>
		<Position>
			<xf>{building.MeshBoundingBox.Position.X}</xf>
			<yf>{building.MeshBoundingBox.Position.Y}</yf>
			<zf>{building.MeshBoundingBox.Position.Z}</zf>
		</Position>
		<Rotation>
			<xf>{building.MeshBoundingBox.Rotation.X}</xf>
			<yf>{building.MeshBoundingBox.Rotation.Y}</yf>
			<zf>{building.MeshBoundingBox.Rotation.Z}</zf>
			<wf>{building.MeshBoundingBox.Rotation.W}</wf>
		</Rotation>
		<Extents>
			<xf>{building.MeshBoundingBox.Extents.X}</xf>
			<yf>{building.MeshBoundingBox.Extents.Y}</yf>
			<zf>{building.MeshBoundingBox.Extents.Z}</zf>
		</Extents>
	</MeshBoundingBox>
</Info>
";
        File.WriteAllText(Path.Combine(path, $"{filename}.ifo"), ifo);
    }








    private static List<Vector> ConvertModelMesh(string? file, string rdmFolder)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return new List<Vector>();
        }

        var fileName = Path.GetFileNameWithoutExtension(file);
        var objPath = Path.Combine(rdmFolder, $"{fileName}.obj");
        ConvertGr2ToObj(Path.Combine(Config.DataPath1404, file), objPath);
        var vertices = GetVertices(objPath);

        ConvertObjToGlb(fileName, rdmFolder);

        ConvertGlbToRdm(fileName, rdmFolder, "P4h_N4b_G4b_B4b_T2h");
        return vertices;
    }


    private static List<Vector> ConvertClothMesh(string? file, string rdmFolder)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return new List<Vector>();
        }

        var fileName = Path.GetFileNameWithoutExtension(file);
        var objPath = Path.Combine(rdmFolder, $"{fileName}.obj");
        ConvertGr2ToObj(Path.Combine(Config.DataPath1404, file), objPath);
        var vertices = GetVertices(objPath);

        var clothVertexIndices = Array.Empty<int>();
        if (Config.ClothDynamicVertices.ContainsKey(fileName))
        {
            clothVertexIndices = Config.ClothDynamicVertices[fileName] ?? Array.Empty<int>();
        }
        else
        {
            WriteWarningLine($"MISSING CLOTH_DYNAMIC_VERTICES {fileName}");
        }
        ConvertObjToGlb(fileName, rdmFolder, clothVertexIndices);

        ConvertGlbToRdm(fileName, rdmFolder, "P3f_N3f_G3f_B3f_T2f_C4b");
        return vertices;
    }

    private static List<Vector> GetVertices(string objPath)
    {
        try
        {
            AssimpContext importer = new AssimpContext();
            Scene scene = importer.ImportFile(objPath, PostProcessSteps.None);

            return scene.Meshes
                .SelectMany(x => x.Vertices)
                .Select(vert => new Vector(vert.X, vert.Y, vert.Z))
                .ToList();
        }
        catch
        {
            return new List<Vector>();
        }
    }

    private static void ConvertModelMap<T>(string? file, string mapsFolder, bool isNormal, List<T> models, bool isProp) where T : ModelOrCloth1404
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }

        var filename = Path.GetFileNameWithoutExtension(file);
        var pngPath = ConvertDdsToPng(file, mapsFolder, $"{filename}_original", isProp);

        if (pngPath == null)
        {
            return;
        }

        using (Image<Rgba32> image = Image.Load<Rgba32>(pngPath))
        {
            image.Mutate(ctx => ctx.Flip(FlipMode.Vertical));

            if (isNormal)
            {
                // RG is normalmap
                // B is Glowmap
                // A is glossiness

                var customMapFolderPath = GetCurrentDirectory("custom_maps", Path.GetDirectoryName(file));
                Directory.CreateDirectory(customMapFolderPath);

                var exampleFilePath = Path.Combine(customMapFolderPath, $"{filename.ReplaceNormal("diff")}.png");
                var diffFilePath = pngPath.Replace("_norm_original", $"_diff");
                if (!File.Exists(exampleFilePath) && File.Exists(diffFilePath))
                {
                    File.Copy(diffFilePath, exampleFilePath);
                }

                // White means lit at night
                var hasMask = CopyCustomMap(image.Width, image.Height, "mask", customMapFolderPath, filename, mapsFolder);
                models.ForEach(x => x.HasMask = hasMask);

                // White means player color
                var hasDye = CopyCustomMap(image.Width, image.Height, "dye", customMapFolderPath, filename, mapsFolder);
                models.ForEach(x => x.HasDye = hasDye);

                // Grayscale means height
                var hasHeight = CopyCustomMap(image.Width, image.Height, "height", customMapFolderPath, filename, mapsFolder);
                models.ForEach(x => x.HasHeight = hasHeight);

                using (Image<Rgba32> metallicMap = new Image<Rgba32>(image.Width, image.Height))
                {
                    // RGB Metallic
                    // Alpha Ambient Occlusion Map

                    var mapFilename = filename.ReplaceNormal("metal");
                    var customMap = GetCustomMap(customMapFolderPath, $"{mapFilename}.png");
                    var customMapWidth = customMap.GetLength(0);
                    var customMapHeight = customMap.GetLength(1);

                    for (var x = 0; x < image.Width; x++)
                    {
                        for (var y = 0; y < image.Height; y++)
                        {
                            var color = image[x, y];
                            var metallicColor = metallicMap[x, y];

                            if (x < customMapWidth && y < customMapHeight)
                            {
                                var customColor = customMap[x, y];

                                metallicColor.R = customColor.R;
                                metallicColor.G = customColor.G;
                                metallicColor.B = customColor.B;
                            }
                            else
                            {
                                metallicColor.R = 0;
                                metallicColor.G = 0;
                                metallicColor.B = 0;
                            }
                            metallicColor.A = color.B;

                            color.B = 0;
                            color.A = 0;

                            image[x, y] = color;
                            metallicMap[x, y] = metallicColor;
                        }
                    }

                    var metallicMapFilename = filename.ReplaceNormal("metal");
                    var metallicMapPath = Path.Combine(mapsFolder, $"{metallicMapFilename}.png");
                    metallicMap.SaveAsPng(metallicMapPath);
                    ConvertPngToDds(metallicMapFilename, mapsFolder);
                }
            }

            var imagePath = Path.Combine(mapsFolder, $"{filename}.png");
            image.SaveAsPng(imagePath);

            if (!isNormal)
            {
                UpscalePng(imagePath, image);
            }
            ConvertPngToDds(filename, mapsFolder);
        }
    }

    private static bool CopyCustomMap(int mapWidth, int mapHeight, string name, string customMapFolderPath, string filename, string mapsFolder)
    {
        using (Image<Rgba32> map = new Image<Rgba32>(mapWidth, mapHeight))
        {
            // White means lit at night

            var mapFilename = filename.ReplaceNormal(name);
            var customMap = GetCustomMap(customMapFolderPath, $"{mapFilename}.png");

            if (customMap.Length == 0)
            {
                return false;
            }

            var width = Math.Min(map.Width, customMap.GetLength(0));
            var height = Math.Min(map.Height, customMap.GetLength(1));

            for (var w = 0; w < width; w++)
            {
                for (var h = 0; h < height; h++)
                {
                    map[w, h] = customMap[w, h];
                }
            }

            var mapPath = Path.Combine(mapsFolder, $"{mapFilename}.png");
            map.SaveAsPng(mapPath);
            ConvertPngToDds(mapFilename, mapsFolder);

            return true;
        }
    }

    private static Rgba32[,] GetCustomMap(string customMapFolderPath, string name)
    {
        var filePath = Path.Combine(customMapFolderPath, name);

        if (!Path.Exists(filePath))
        {
            return new Rgba32[0, 0];
        }

        Rgba32[,] result;
        using (Image<Rgba32> customMaskMap = Image.Load<Rgba32>(filePath))
        {
            result = new Rgba32[customMaskMap.Width, customMaskMap.Height];

            for (var w = 0; w < customMaskMap.Width; w++)
            {
                for (var h = 0; h < customMaskMap.Height; h++)
                {
                    result[w, h] = customMaskMap[w, h];
                }
            }
        }

        return result;
    }













    private static void ConvertGroundMap(Ground1404 ground, string mapsFolder)
    {
        var file = ground.Diff;
        var filename = Path.GetFileNameWithoutExtension(file);
        var pngPath = ConvertDdsToPng(file, mapsFolder, $"{filename}_original", false);

        if (pngPath == null)
        {
            return;
        }

        using (Image<Rgba32> image = Image.Load<Rgba32>(pngPath))
        {
            int x1 = (int)(ground.TextCoordXStart * image.Width);
            int y1 = (int)(ground.TextCoordYStart * image.Height);
            int x2 = (int)(ground.TextCoordXEnd * image.Width);
            int y2 = (int)(ground.TextCoordYEnd * image.Height);

            var xStart = Math.Min(x1, x2);
            var yStart = Math.Min(y1, y2);
            var xEnd = Math.Max(x1, x2);
            var yEnd = Math.Max(y1, y2);

            var width = xEnd - xStart;
            var height = yEnd - yStart;

            image.Mutate(img => img.Crop(new Rectangle(xStart, yStart, width, height)));

            if (x2 < x1)
            {
                image.Mutate(img => img.Flip(FlipMode.Horizontal));
            }

            if (y2 < y1)
            {
                image.Mutate(img => img.Flip(FlipMode.Vertical));
            }

            var finalFilename = $"{filename}_{x1}_{y1}_{x2}_{y2}";

            var imagePath = Path.Combine(mapsFolder, $"{finalFilename}.png");
            image.SaveAsPng(imagePath);
            ground.Diff = ground.Diff.Replace(filename, finalFilename);

            UpscalePng(imagePath, image);
            // No dds: no ground is shown if generated
        }
    }














    private static string? ConvertDdsToPng(string relativePath, string outputFolder, string outputFile, bool isProp)
    {
        var ddsPath = Paths.CreateFrom1404InternalPath(relativePath, isProp).FilePath1404.Replace(".png", "_0.dds");
        if (!File.Exists(ddsPath))
        {
            return null;
        }
        var pngPath = Path.Combine(outputFolder, $"{outputFile}.png");

        DDSImage img = new DDSImage(File.ReadAllBytes(ddsPath));

        img?.images?.FirstOrDefault()?.Save(pngPath, ImageFormat.Png);

        return pngPath;
    }

    private static string UpscalePng(string imagePath, Image<Rgba32> image, int multiplier = 4)
        => UpscalePng(imagePath, image.Width * multiplier);

    private static string UpscalePng(string imagePath, int width = 4096)
    {
        if (Config.SkipUpscale)
        {
            return string.Empty;
        }

        return Execute(Config.UpscaylPath, @$"-i ""{imagePath}"" -o ""{imagePath}"" -w {width} -n {Config.UpscaylModel}", GetCurrentDirectory());
    }

    private static string ConvertPngToDds(string file, string outputFolder, int levels = 3)
    {
        return Execute(Config.AnnotexPath, @$"""{file}.png"" -l={levels}", outputFolder);
    }

    private static string ConvertGr2ToObj(string inputPath, string outputPath)
    {
        var cmdResult = Execute(Config.Evegr2toobjPath, @$"""{inputPath}"" ""{outputPath}""");

        try
        {
            var lines = File.ReadAllLines(outputPath);
            var rotatedLines = new string[lines.Length];
            double angleDegrees = 90;
            double radians = angleDegrees * Math.PI / 180.0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.StartsWith("v ") || line.StartsWith("vn "))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        double x = double.Parse(parts[1], CultureInfo.InvariantCulture);
                        double y = double.Parse(parts[2], CultureInfo.InvariantCulture);
                        double z = double.Parse(parts[3], CultureInfo.InvariantCulture);

                        // Apply rotation around X axis:
                        double yRot = y * Math.Cos(radians) - z * Math.Sin(radians);
                        double zRot = y * Math.Sin(radians) + z * Math.Cos(radians);

                        rotatedLines[i] = $"{parts[0]} {x.ToString(CultureInfo.InvariantCulture)} {yRot.ToString(CultureInfo.InvariantCulture)} {zRot.ToString(CultureInfo.InvariantCulture)}";
                    }
                    else
                    {
                        rotatedLines[i] = line;
                    }
                }
                else
                {
                    rotatedLines[i] = line;
                }
            }
            File.WriteAllLines(outputPath, rotatedLines);
        }
        catch (Exception) { }

        return cmdResult;
    }

    private static string ConvertObjToGlb(string file, string path, params int[] clothVertexIndices)
    {
        var pyPath = GetCurrentDirectory("2gltf2.py");
        return Execute(Config.BlenderPath, @$"-b -P ""{pyPath}"" {file}.obj {string.Join(" ", clothVertexIndices)}", path);
    }

    private static string ConvertGlbToRdm(string inputFile, string path, string vertexFormat = "P4h_N4b_G4b_B4b_T2h")
    {
        return Execute(Config.Rdm4binPath, $@"-g={vertexFormat} -i {inputFile}.glb -o ""{path}"" -n --no_transform --force", path);
    }

    private static string Execute(string fileName, string arguments, string? workingDirectory = null)
    {
        var processStartInfo = new ProcessStartInfo()
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        using (Process? process = Process.Start(processStartInfo))
        {
            process?.WaitForExit();
            return process?.StandardOutput?.ReadToEnd() ?? "";
        }
    }

    private static Building1404 LoadBuilding(Paths paths, bool isProp)
    {
        var cfgPath = paths.FilePath1404;
        var doc = new XmlDocument();
        doc.Load(cfgPath);

        var building = new Building1404()
        {
            Models = new List<Model1404>(),
            Clothes = new List<Cloth1404>(),
            Files = new List<File1404>(),
            DamageImpacts = new List<DamageTransform1404>(),
            Collisions = new List<Collision1404>(),
            Particles = new List<Particle1404>(),
            Grounds = new List<Ground1404>(),
        };

        var mainConfig = doc?.GetConfig();

        var modelNodes = mainConfig?.GetChildConfigs("m_Models") ?? new List<XmlNode?>();
        modelNodes.AddRange(mainConfig?.GetChildConfigs("m_InstancingModels") ?? new List<XmlNode?>());
        foreach (XmlNode? modelNode in modelNodes)
        {
            var fileName = modelNode?.GetChild("m_FileName")?.InnerText;
            var materialNode = modelNode?.GetChildConfig("m_Materials");
            var transformerNode = modelNode?.GetChildConfig("m_Transformer");

            if (materialNode != null)
            {
                var diffuseTexture = materialNode?.GetChild("m_DiffuseTexture")?.InnerText;

                building.Models.Add(new Model1404()
                {
                    Model = fileName,
                    Diff = diffuseTexture,
                    Norm = materialNode?.GetChild("m_NormalTexture")?.InnerText,
                    Ripples = materialNode?.GetChild("m_RipplesEnabled")?.InnerText == "1",
                    Water = diffuseTexture?.StartsWith("data\\graphics\\effects\\water") == true,
                    Position = new Vector(
                        transformerNode?.GetChild("m_Position.x")?.GetNumber() ?? 0,
                        transformerNode?.GetChild("m_Position.y")?.GetNumber() ?? 0,
                        transformerNode?.GetChild("m_Position.z")?.GetNumber() ?? 0),
                    Rotation = new Quaternion(
                        (float)(transformerNode?.GetChild("m_Rotation.x")?.GetNumber() ?? 0),
                        (float)(transformerNode?.GetChild("m_Rotation.y")?.GetNumber() ?? 0),
                        (float)(transformerNode?.GetChild("m_Rotation.z")?.GetNumber() ?? 0),
                        (float)(transformerNode?.GetChild("m_Rotation.w")?.GetNumber() ?? 1)),
                    Scale = transformerNode?.GetChild("m_Scale")?.GetNumber() ?? 1
                });
            }
        }

        var clothesNodes = mainConfig?.GetChildConfigs("m_Clothes") ?? new List<XmlNode?>();
        foreach (var clothesNode in clothesNodes)
        {
            var fileName = clothesNode?.GetChild("m_FileName")?.InnerText;
            var materialNode = clothesNode?.GetChildConfig("m_Materials");
            var transformerNode = clothesNode?.GetChildConfig("m_Transformer");

            if (materialNode != null)
            {
                var diffuseTexture = materialNode?.GetChild("m_DiffuseTexture")?.InnerText;

                building.Clothes.Add(new Cloth1404()
                {
                    Model = fileName,
                    Diff = diffuseTexture,
                    Norm = materialNode?.GetChild("m_NormalTexture")?.InnerText,
                    DiffColor = System.Drawing.Color.FromArgb(
                        (int)((materialNode?.GetChild("m_DiffuseColor.r")?.GetNumber() ?? 1) * 255),
                        (int)((materialNode?.GetChild("m_DiffuseColor.g")?.GetNumber() ?? 1) * 255),
                        (int)((materialNode?.GetChild("m_DiffuseColor.b")?.GetNumber() ?? 1) * 255)),
                    SpecularColor = System.Drawing.Color.FromArgb(
                        (int)((materialNode?.GetChild("m_SpecularColor.r")?.GetNumber() ?? 1) * 255),
                        (int)((materialNode?.GetChild("m_SpecularColor.g")?.GetNumber() ?? 1) * 255),
                        (int)((materialNode?.GetChild("m_SpecularColor.b")?.GetNumber() ?? 1) * 255)),
                    Position = new Vector(
                        transformerNode?.GetChild("m_Position.x")?.GetNumber() ?? 0,
                        transformerNode?.GetChild("m_Position.y")?.GetNumber() ?? 0,
                        transformerNode?.GetChild("m_Position.z")?.GetNumber() ?? 0),
                    Rotation = new Quaternion(
                        (float)(transformerNode?.GetChild("m_Rotation.x")?.GetNumber() ?? 0),
                        (float)(transformerNode?.GetChild("m_Rotation.y")?.GetNumber() ?? 0),
                        (float)(transformerNode?.GetChild("m_Rotation.z")?.GetNumber() ?? 0),
                        (float)(transformerNode?.GetChild("m_Rotation.w")?.GetNumber() ?? 1)),
                });
            }
        }

        foreach (var groundNode in mainConfig?.GetChildConfigs("m_TerrainDecals") ?? new List<XmlNode?>())
        {
            building.Grounds.Add(new Ground1404()
            {
                Diff = groundNode?.GetChildConfig("m_Materials")?.GetChild("m_DiffuseTexture")?.InnerText ?? "",
                TextCoordXStart = groundNode?.GetChild("m_TexCoords.x")?.GetNumber() ?? 0,
                TextCoordYStart = groundNode?.GetChild("m_TexCoords.y")?.GetNumber() ?? 0,
                TextCoordXEnd = groundNode?.GetChild("m_TexCoords.z")?.GetNumber() ?? 1,
                TextCoordYEnd = groundNode?.GetChild("m_TexCoords.w")?.GetNumber() ?? 1,
                ExtendsX = groundNode?.GetChild("m_Extend.x")?.GetNumber() ?? 0,
                ExtendsY = groundNode?.GetChild("m_Extend.y")?.GetNumber() ?? 0,
                Position = new Vector(
                    groundNode?.GetChildConfig("m_Transformer")?.GetChild("m_Position.x")?.GetNumber() ?? 0,
                    groundNode?.GetChildConfig("m_Transformer")?.GetChild("m_Position.y")?.GetNumber() ?? 0,
                    groundNode?.GetChildConfig("m_Transformer")?.GetChild("m_Position.z")?.GetNumber() ?? 0),
                Rotation = new Quaternion(
                    (float)(groundNode?.GetChildConfig("m_Transformer")?.GetChild("m_Rotation.x")?.GetNumber() ?? 0),
                    (float)(groundNode?.GetChildConfig("m_Transformer")?.GetChild("m_Rotation.y")?.GetNumber() ?? 0),
                    (float)(groundNode?.GetChildConfig("m_Transformer")?.GetChild("m_Rotation.z")?.GetNumber() ?? 0),
                    (float)(groundNode?.GetChildConfig("m_Transformer")?.GetChild("m_Rotation.w")?.GetNumber() ?? 1))
            });
        }

        var fileNodes = mainConfig?.GetChildConfigs("m_Files") ?? new List<XmlNode?>();
        foreach (var fileNode in fileNodes)
        {
            var fileName = fileNode?.GetChild("m_FileName")?.InnerText;
            var transformerNode = fileNode?.GetChildConfig("m_Transformer");

            building.Files.Add(new File1404()
            {
                Model = fileName == null ? null : Paths.CreateFrom1404InternalPath(fileName, isProp).InternalPath1800,
                Position = new Vector(
                    transformerNode?.GetChild("m_Position.x")?.GetNumber() ?? 0,
                    transformerNode?.GetChild("m_Position.y")?.GetNumber() ?? 0,
                    transformerNode?.GetChild("m_Position.z")?.GetNumber() ?? 0),
                Rotation = new Quaternion(
                    (float)(transformerNode?.GetChild("m_Rotation.x")?.GetNumber() ?? 0),
                    (float)(transformerNode?.GetChild("m_Rotation.y")?.GetNumber() ?? 0),
                    (float)(transformerNode?.GetChild("m_Rotation.z")?.GetNumber() ?? 0),
                    (float)(transformerNode?.GetChild("m_Rotation.w")?.GetNumber() ?? 1)),
                Scale = transformerNode?.GetChild("m_Scale")?.GetNumber() ?? 1
            });
        }

        var effectNodes = mainConfig?.GetChildConfigs("m_Effects") ?? new List<XmlNode?>();
        foreach (var effectNode in effectNodes)
        {
            var transformerNodes = effectNode?.GetChildConfigs("m_Transformer");
            var transformerNode = transformerNodes?.FirstOrDefault(x => x?.GetChild("m_ConfigType")?.InnerText == "ORIENTATION_TRANSFORM");

            if (transformerNodes?.Any(x => x?.GetChild("m_ConfigType")?.InnerText == "DAMAGE_TRANSFORM") == true)
            {
                building.DamageImpacts.Add(new DamageTransform1404()
                {
                    Position = new Vector(
                        transformerNode?.GetChild("m_Position.x")?.GetNumber() ?? 0,
                        transformerNode?.GetChild("m_Position.y")?.GetNumber() ?? 0,
                        transformerNode?.GetChild("m_Position.z")?.GetNumber() ?? 0),
                    Rotation = new Quaternion(
                        (float)(transformerNode?.GetChild("m_Rotation.x")?.GetNumber() ?? 0),
                        (float)(transformerNode?.GetChild("m_Rotation.y")?.GetNumber() ?? 0),
                        (float)(transformerNode?.GetChild("m_Rotation.z")?.GetNumber() ?? 0),
                        (float)(transformerNode?.GetChild("m_Rotation.w")?.GetNumber() ?? 1)),
                    Scale = transformerNode?.GetChild("m_Scale")?.GetNumber() ?? 1
                });
            }
            else if (transformerNodes?.Any(x => x?.GetChild("m_ConfigType")?.InnerText == "COLOR_TRANSFORM") == true)
            {
                var colorNode = transformerNodes?.FirstOrDefault(x => x?.GetChild("m_ConfigType")?.InnerText == "COLOR_TRANSFORM");

                var fileName = effectNode?.GetChild("m_FileName")?.InnerText;

                if (Config.ParticleMappings.ContainsKey(fileName ?? ""))
                {
                    fileName = Config.ParticleMappings[fileName ?? ""];

                    if (fileName != null)
                    {
                        building.Particles.Add(new Particle1404()
                        {
                            Model = fileName ?? "",
                            DiffColors = colorNode?.GetChildren("m_State")?.Select(x => (System.Drawing.Color.FromArgb(
                                    (int)((x?.GetChild("m_Color.r")?.GetNumber() ?? 1) * 255),
                                    (int)((x?.GetChild("m_Color.g")?.GetNumber() ?? 1) * 255),
                                    (int)((x?.GetChild("m_Color.b")?.GetNumber() ?? 1) * 255)), x?.GetChild("m_Condition")?.GetNumber() ?? 0))?
                                .ToList() ?? new List<(System.Drawing.Color, double)>(),
                            Position = new Vector(
                                transformerNode?.GetChild("m_Position.x")?.GetNumber() ?? 0,
                                transformerNode?.GetChild("m_Position.y")?.GetNumber() ?? 0,
                                transformerNode?.GetChild("m_Position.z")?.GetNumber() ?? 0),
                            Rotation = new Quaternion(
                                (float)(transformerNode?.GetChild("m_Rotation.x")?.GetNumber() ?? 0),
                                (float)(transformerNode?.GetChild("m_Rotation.y")?.GetNumber() ?? 0),
                                (float)(transformerNode?.GetChild("m_Rotation.z")?.GetNumber() ?? 0),
                                (float)(transformerNode?.GetChild("m_Rotation.w")?.GetNumber() ?? 1)),
                            Scale = transformerNode?.GetChild("m_Scale")?.GetNumber() ?? 1
                        });
                    }
                }
                else
                {
                    WriteWarningLine($"MISSING PARTICLE_MAPPING {fileName}");
                }
            }
        }

        var collisionsNodes = mainConfig?.GetChildConfigs("m_Collisions") ?? new List<XmlNode?>();
        foreach (var collisionsNode in collisionsNodes)
        {
            var transformerNode = collisionsNode?.GetChildConfig("m_Transformer");

            building.Collisions.Add(new Collision1404()
            {
                Type = collisionsNode?.GetChild("m_CollisionType")?.InnerText ?? "Cylinder",
                Position = new Vector(
                    transformerNode?.GetChild("m_Position.x")?.GetNumber() ?? 0,
                    transformerNode?.GetChild("m_Position.y")?.GetNumber() ?? 0,
                    transformerNode?.GetChild("m_Position.z")?.GetNumber() ?? 0),
                Rotation = new Quaternion(
                    (float)(transformerNode?.GetChild("m_Rotation.x")?.GetNumber() ?? 0),
                    (float)(transformerNode?.GetChild("m_Rotation.y")?.GetNumber() ?? 0),
                    (float)(transformerNode?.GetChild("m_Rotation.z")?.GetNumber() ?? 0),
                    (float)(transformerNode?.GetChild("m_Rotation.w")?.GetNumber() ?? 1)),
                Scale = transformerNode?.GetChild("m_Scale")?.GetNumber() ?? 1
            });
        }




        doc?.Load(cfgPath.Replace(".cfg", ".ifo"));

        var ifo = doc?.GetChild("Info");

        building.BoundingBox = new Transform(ifo?.GetChild("BoundingBox"));

        building.IntersectBoxes = ifo?.GetChildren("IntersectBox")?.Select(x => new Transform(x))?.ToList() ?? new List<Transform>();

        building.BuildBlocker = new Polygon(ifo?.GetChild("BuildBlocker"));

        var dummies = ifo?.GetChildren("Dummy") ?? new List<XmlNode?>();
        building.InfoLayer = new Transform(dummies?.FirstOrDefault(x => x?.GetChild("Name")?.InnerText == "infolayer"));


        building.PathBlockers = ifo?.GetChildren("PathBlocker")?.Select(x => new Polygon(x))?.ToList() ?? new List<Polygon>();

        return building;
    }

    private static void CalculateBuildingValuesFromVertices(Building1404 building, List<Vector> vertices)
    {
        if (vertices.Count > 0)
        {
            var xs = vertices.Select(x => x.X).ToList();
            var ys = vertices.Select(x => x.Y).ToList();
            var zs = vertices.Select(x => x.Z).ToList();
            var minX = xs.Min();
            var maxX = xs.Max();
            var minY = ys.Min();
            var maxY = ys.Max();
            var minZ = zs.Min();
            var maxZ = zs.Max();

            var centerX = (minX + maxX) / 2;
            var centerY = (minY + maxY) / 2;
            var centerZ = (minZ + maxZ) / 2;

            building.MeshBoundingBox = new Transform(
                new Vector(centerX, centerZ, centerY),
                new Quaternion(0, 0, 0, 1),
                new Vector(maxX - centerX + 0.1, maxZ - centerZ + 0.1, maxY - centerY + 0.1));
        }
        else
        {
            building.MeshBoundingBox = new Transform(
                new Vector(0, 0, 0),
                new Quaternion(0, 0, 0, 1),
                new Vector(0, 0, 0));
        }

        var transportSpawns = GetPossibleTransporterSpawns(building.GroundX, building.GroundY);

        building.TransporterSpawn = transportSpawns?
            .OrderBy(x => vertices.Count(y => y.DistanceTo(x) < 0.5))
            .FirstOrDefault() ?? new Vector(0, 0, 0);
    }

    private static List<Vector> GetPossibleTransporterSpawns(double groundX, double groundY, double offset = 0.25)
    {
        var result = new List<Vector>();
        for (var i = groundX * (-1); i <= groundX; i++)
        {
            result.Add(new Vector(i, 0, groundY));
            result.Add(new Vector(i, 0, groundY * (-1)));
        }

        for (var i = groundY * (-1) + 1; i <= groundY - 1; i++)
        {
            result.Add(new Vector(groundX, 0, i));
            result.Add(new Vector(groundX * (-1), 0, i));
        }

        result.ForEach(vector =>
        {
            if (vector.X == groundX)
            {
                vector.X -= offset;
            }
            else if (vector.X == groundX * (-1))
            {
                vector.X += offset;
            }

            if (vector.Z == groundY)
            {
                vector.Z -= offset;
            }
            else if (vector.Z == groundY * (-1))
            {
                vector.Z += offset;
            }
        });

        return result;
    }

    public static double ConvertDimension(XmlNode? node)
    {
        return (node?.GetNumber() ?? 0) / 4096;
    }

    public static double ParseNumber(string? text)
    {
        if (text == null)
        {
            return 0;
        }
        if (double.TryParse(text, out double number))
        {
            return number;
        }

        return 0;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        // Ensure the destination directory exists
        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        // Copy all files
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, true); // Overwrite if exists
        }

        // Copy all subdirectories recursively
        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }

    private static string GetCurrentDirectory(params string[] subParts)
    {
        var dir = Directory.GetCurrentDirectory().Replace(@"bin\Debug\net8.0", "");
        if (subParts?.Any() != true)
        {
            return dir;
        }

        return Path.Combine(new[] { dir }.Concat(subParts).ToArray());
    }

    private static void WriteWarningLine(string message)
    {
        Console.WriteLine($"<!-- {message} -->");
    }
}

public static class Extensions
{
    public static string? ReplaceNormal(this string text, string replaceString)
    {
        return text?
            .Replace("_normal", $"_{replaceString}")?
            .Replace("_norm", $"_{replaceString}");
    }
}
