using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PZShrinker.Lib;

public static partial class Finder
{
    public static string[] GetModAllBasePath(string path)
    {
        var di = new DirectoryInfo(path);
        if (!di.Exists)
        {
            throw new DirectoryNotFoundException($"The path '{path}' does not exist.");
        }
        DirectoryInfo submods = new(Path.Combine(di.FullName, "mods"));
        if (!submods.Exists)
        {
            throw new DirectoryNotFoundException($"Corrupted mod {path}, has no submods");
        }
        return [.. submods.EnumerateFiles("mod.info", SearchOption.AllDirectories)
            .Select(m => Path.GetDirectoryName(m.FullName) ?? string.Empty)];
    }

    // Path是某个Mod的路径，如108600/1992785456
    public static IEnumerable<string> FindFBXModels(string path)
    {
        var m = new DirectoryInfo(path);
        if (!m.Exists)
        {
            throw new DirectoryNotFoundException($"The path '{path}' does not exist.");
        }
        var pending_models = Enumerable.Empty<string>();
        DirectoryInfo submods = new(Path.Combine(m.FullName, "mods"));
        if (!submods.Exists)
            throw new DirectoryNotFoundException($"Corrupted mod {m}, has no submods");
        var modinfos = GetModAllBasePath(m.FullName);
        foreach (var mpp in modinfos)
        {
            if (mpp == null)
                continue;
            DirectoryInfo item = new(mpp);
            if (!item.Exists)
                continue;
            pending_models = pending_models.Concat(item.EnumerateFiles("*.fbx", SearchOption.AllDirectories)
                .Select(file => file.FullName));
        }
        return pending_models.Distinct();
    }

    // Path是某个Mod的路径，如108600/1992785456
    public static IEnumerable<string> FindD3DModels(string path)
    {
        var m = new DirectoryInfo(path);
        if (!m.Exists)
        {
            throw new DirectoryNotFoundException($"The path '{path}' does not exist.");
        }
        var pending_models = Enumerable.Empty<string>();
        DirectoryInfo submods = new(Path.Combine(m.FullName, "mods"));
        if (!submods.Exists)
            throw new DirectoryNotFoundException($"Corrupted mod {m}, has no submods");
        var modinfos = GetModAllBasePath(m.FullName);
        foreach (var mpp in modinfos)
        {
            if (mpp == null)
                continue;
            DirectoryInfo item = new(mpp);
            if (!item.Exists)
                continue;
            pending_models = pending_models.Concat(item.EnumerateFiles("*.x", SearchOption.AllDirectories)
                .Select(file => file.FullName));
        }
        return pending_models.Distinct();
    }

    // Path是某个Mod的路径，如108600/1992785456
    public static IEnumerable<string> FindTextures(string path, bool allTexture, bool iconTexture, bool modelTexture)
    {
        var m = new DirectoryInfo(path);
        if (!m.Exists)
        {
            throw new DirectoryNotFoundException($"The path '{path}' does not exist.");
        }
        var pending_textures = Enumerable.Empty<string>();
        var modinfos = GetModAllBasePath(m.FullName);
        foreach (var mpp in modinfos)
        {
            if (mpp == null)
                continue;
            DirectoryInfo item = new(mpp);
            if (!item.Exists)
                continue;
            if (allTexture)
            {
                pending_textures = pending_textures.Concat(item.EnumerateFiles("*.png", SearchOption.AllDirectories)
                    .Select(file => file.FullName));
                break;
            }
            if (iconTexture || modelTexture)
            {
                var txtFiles = item.EnumerateFiles("*.txt", searchOption: SearchOption.AllDirectories);
                foreach (var txtFile in txtFiles)
                {
                    string file = File.ReadAllText(txtFile.FullName);

                    if (modelTexture)
                    {
                        Regex regex = TextureRegex();
                        var regexResult = regex.Matches(file);
                        foreach (Match match in regexResult)
                        {
                            string? value = match.Groups[1].Success ? match.Groups[1].Value :
                                           match.Groups[2].Success ? match.Groups[2].Value :
                                           match.Groups[3].Success ? match.Groups[3].Value :
                                           null;
                            if (value != null)
                            {
                                var p = Path.Combine(mpp, "media/textures", value) + ".png";
                                if (File.Exists(p))
                                    pending_textures = pending_textures.Append(p);
                            }
                        }
                    }
                    if (iconTexture)
                    {
                        Regex regex = IconRegex();
                        var regexResult = regex.Matches(file);
                        foreach (Match match in regexResult)
                        {
                            string? value = match.Groups[1].Success ? match.Groups[1].Value :
                                           match.Groups[2].Success ? match.Groups[2].Value :
                                           match.Groups[3].Success ? match.Groups[3].Value :
                                           null;
                            if (value != null)
                            {
                                var p = Path.Combine(mpp, "media/textures", ($"Item_{value}")) + ".png";
                                if (File.Exists(p))
                                    pending_textures = pending_textures.Append(p);
                            }
                        }
                    }
                }
            }
            if (modelTexture)
            {
                var xmlFiles = item.EnumerateFiles("*.xml", SearchOption.AllDirectories);
                foreach (var xmlFile in xmlFiles)
                {
                    XDocument doc = XDocument.Load(xmlFile.FullName);
                    var texture_choices = from tc in doc.Descendants("textureChoices")
                                          select new
                                          {
                                              tc.Value
                                          };
                    var base_textures = from tc in doc.Descendants("m_BaseTextures")
                                        select new
                                        {
                                            tc.Value
                                        };
                    foreach (var t in texture_choices.Concat(base_textures))
                    {
                        var p = Path.Combine(mpp, "media/textures", t.Value) + ".png";
                        if (File.Exists(p))
                            pending_textures = pending_textures.Append(p);
                    }
                }
            }
        }

        return pending_textures.Distinct();
    }

    // Path是某个Mod的路径，如108600/1992785456
    public static IEnumerable<string> FindPacks(string path)
    {
        var m = new DirectoryInfo(path);
        if (!m.Exists)
        {
            throw new DirectoryNotFoundException($"The path '{path}' does not exist.");
        }
        var pending_packs = Enumerable.Empty<string>();
        DirectoryInfo submods = new(Path.Combine(m.FullName, "mods"));
        if (!submods.Exists)
             throw new DirectoryNotFoundException($"Corrupted mod {m}, has no submods");
        var modinfos = GetModAllBasePath(m.FullName);
        foreach (var mpp in modinfos)
        {
            if (mpp == null)
                continue;
            DirectoryInfo item = new(mpp);
            if (!item.Exists)
                continue;
            pending_packs = pending_packs.Concat(item.EnumerateFiles("*.pack", SearchOption.AllDirectories)
                .Select(file => file.FullName));
        }
        return pending_packs.Distinct();
    }

    [GeneratedRegex("texture\\s*=\\s*(?:\"([^\"\\r\\n]*?)\"|'([^'\\r\\n]*?)'|([^,\\r\\n]+))")]
    private static partial Regex TextureRegex();

    [GeneratedRegex("Icon\\s*=\\s*(?:\"([^\"\\r\\n]*?)\"|'([^'\\r\\n]*?)'|([^,\\r\\n]+))")]
    private static partial Regex IconRegex();
}
