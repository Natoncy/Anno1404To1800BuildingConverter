namespace Anno1404To1800BuildingConverter;

internal class Paths
{
    public string FilePath1404 { get; private set; }
    public string InternalPath1404 { get; private set; }
    public string FilePath1800 { get; private set; }
    public string InternalPath1800 { get; private set; }

    private Paths() { }

    public static Paths CreateFrom1404AbsoluteFilePath(string path, bool isProp)
    {
        var result = new Paths();
        result.FilePath1404 = StandardizeSlashes(path);
        result.InternalPath1404 = result.FilePath1404.Replace(Program.Config.DataPath1404, "");

        result.InternalPath1800 = result.InternalPath1404
            .Replace(Program.Config.OriginalPathPart, Program.Config.ReplacementPathPart);
        if (isProp)
        {
            result.InternalPath1800 = result.InternalPath1800
                .Replace($"{Program.Config.ReplacementPathPart}\\buildings\\", $"{Program.Config.ReplacementPathPart}\\props\\");
        }
        result.FilePath1800 = Program.Config.DataPathMod + result.InternalPath1800;

        return result;
    }

    public static Paths CreateFrom1404InternalPath(string path, bool isProp)
    {

        var result = new Paths();
        result.InternalPath1404 = StandardizeSlashes(path);
        result.FilePath1404 = Program.Config.DataPath1404 + result.InternalPath1404;

        result.InternalPath1800 = result.InternalPath1404
            .Replace(Program.Config.OriginalPathPart, Program.Config.ReplacementPathPart);
        if (isProp)
        {
            result.InternalPath1800 = result.InternalPath1800
                .Replace($"{Program.Config.ReplacementPathPart}\\buildings\\", $"{Program.Config.ReplacementPathPart}\\props\\");
        }
        result.FilePath1800 = Program.Config.DataPathMod + result.InternalPath1800;

        return result;
    }

    public static string CreateInternalFrom1800AbsoluteFilePath(string path)
    {
        return StandardizeSlashes(path).Replace(Program.Config.DataPathMod, "");
    }

    private static string StandardizeSlashes(string path)
    {
        return path?.Replace("/", "\\") ?? "";
    }
}
