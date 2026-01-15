using Pastel;
using PZTextureShrinker;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var rootCommand = new RootCommand("Project zomboid texture size shrinker");

var pathArgument = new Argument<string>("path")
{
    Description = "The path to the workshop folder.(e.g: D:\\SteamLibrary\\steamapps\\workshop\\content\\108600)",
    Arity = ArgumentArity.ExactlyOne
};

var maxSizeOption = new Option<int>("--max-size", "-max")
{
    DefaultValueFactory = _ => 512,
    Description = "The maximum size of the texture. Default is 512."
};
var minSizeOption = new Option<int>("--min-size", "-min")
{
    DefaultValueFactory = _ => 64,
    Description = "The minimum size of the texture. Default is 64."
};
var scaleratioOption = new Option<float>("--scale-ratio", "-sr")
{
    DefaultValueFactory = _ => 0.25f,
    Description = "The scale ratio"
};

var iconTextureOption = new Option<bool>("--icon-texture", "-it")
{
    DefaultValueFactory = _ => false,
    Description = "Process item icon textures"
};
var modelTextureOption = new Option<bool>("--model-texture", "-mt")
{
    DefaultValueFactory = _ => false,
    Description = "Process model textures"
};
var packTextureOption = new Option<bool>("--tiles-pack", "-tp")
{
    DefaultValueFactory = _ => false,
    Description = "Process tileset texture packs"
};
var allTextureOption = new Option<bool>("--all-texture", "-all")
{
    DefaultValueFactory = _ => false,
    Description = "Process all PNG texture files"
};

rootCommand.Add(pathArgument);
rootCommand.Add(maxSizeOption);
rootCommand.Add(minSizeOption);
rootCommand.Add(scaleratioOption);
rootCommand.Add(modelTextureOption);
rootCommand.Add(packTextureOption);
rootCommand.Add(iconTextureOption);
rootCommand.Add(allTextureOption);

ParseResult parseResult = rootCommand.Parse(args);
if (parseResult.Errors.Count == 0 &&
    parseResult.GetValue(pathArgument) is string path &&
    parseResult.GetValue(maxSizeOption) is int maxSize &&
    parseResult.GetValue(minSizeOption) is int minSize &&
    parseResult.GetValue(scaleratioOption) is float scaleratio &&
    parseResult.GetValue(packTextureOption) is bool packTexture &&
    parseResult.GetValue(iconTextureOption) is bool iconTexture &&
    parseResult.GetValue(allTextureOption) is bool allTexture &&
    parseResult.GetValue(modelTextureOption) is bool modelTexture)
{
    var di = new DirectoryInfo(path);
    if (!di.Exists)
    {
        Console.Error.WriteLine($"The path '{path}' does not exist.".Pastel(ConsoleColor.Red));
        return 2;
    }
    if (maxSize < 0 || minSize < 0 || scaleratio < 0)
    {
        Console.Error.WriteLine($"Negetive?".Pastel(ConsoleColor.Red));
        return 3;
    }
    IEnumerable<string> pending_textures = [];
    IEnumerable<string> pending_packs = [];
    var mods = di.GetDirectories();
    Console.WriteLine($"Found {mods.Length} mods, processing");
    foreach (var m in mods)
    {
        DirectoryInfo submods = new(Path.Combine(m.FullName, "mods"));
        if (!submods.Exists)
        {
            Console.Error.WriteLine($"Corrupted mod {m}, has no submods".Pastel(ConsoleColor.Red));
            continue;
        }
        var modinfos = submods.EnumerateFiles("mod.info", SearchOption.AllDirectories)
            .Select(m => Path.GetDirectoryName(m.FullName)).ToArray();

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
                    Console.WriteLine($"Start parsing TXT {txtFile}");

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
                    Console.WriteLine($"Start parsing XML {xmlFile}");
                    try
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
                    catch (Exception)
                    {
                        Console.Error.WriteLine($"{xmlFile} is not a valid XML file. This is a common error in mods.".Pastel(ConsoleColor.Red));
                        continue;
                    }
                }
            }
            if (packTexture)
            {
                pending_packs = pending_packs.Concat(item.EnumerateFiles("*.pack", SearchOption.AllDirectories)
                    .Select(file => file.FullName));
            }
        }
    }

    string[] unique_texture = [.. pending_textures.Distinct()];
    Console.WriteLine($"Found {unique_texture.Length} unique textures to process");
    string[] unique_packs = [.. pending_packs.Distinct()];
    Console.WriteLine($"Found {unique_packs.Length} unique texture packs to process");

    var sk = new Shrinker(minSize, maxSize, scaleratio);
    if (unique_texture.Length > 0)
        sk.ShrinkTexture(unique_texture);
    if (unique_packs.Length > 0)
        sk.ShrinkPack(unique_packs);

    Console.WriteLine($"\nProcessing complete!".Pastel(ConsoleColor.Green));
    return 0;
}

foreach (ParseError parseError in parseResult.Errors)
{
    Console.Error.WriteLine(parseError.Message.Pastel(ConsoleColor.Red));
}
return 1;

partial class Program
{
    [GeneratedRegex("texture\\s*=\\s*(?:\"([^\"\\r\\n]*?)\"|'([^'\\r\\n]*?)'|([^,\\r\\n]+))")]
    private static partial Regex TextureRegex();

    [GeneratedRegex("Icon\\s*=\\s*(?:\"([^\"\\r\\n]*?)\"|'([^'\\r\\n]*?)'|([^,\\r\\n]+))")]
    private static partial Regex IconRegex();
}