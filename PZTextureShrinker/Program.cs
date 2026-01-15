using Assimp;
using Pastel;
using PZTextureShrinker;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.CommandLine;
using System.CommandLine.Parsing;

//ceshi
args = ["D:\\SteamLibrary\\steamapps\\workshop\\content\\108600", "-max", "256", "-min", "64", "-mt"];


// 创建根命令
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

var modelTextureOption = new Option<bool>("--model-texture", "-mt")
{
    DefaultValueFactory = _ => false,
    Description = "process model textures."
};
var packTextureOption = new Option<bool>("--tiles-pack", "-tp")
{
    DefaultValueFactory = _ => false,
    Description = "process tiles textures."
};
var allTextureOption = new Option<bool>("--all-texture", "-all")
{
    DefaultValueFactory = _ => false,
    Description = "process all png"
};

rootCommand.Add(pathArgument);
rootCommand.Add(maxSizeOption);
rootCommand.Add(minSizeOption);
rootCommand.Add(scaleratioOption);
rootCommand.Add(modelTextureOption);
rootCommand.Add(packTextureOption);
rootCommand.Add(allTextureOption);

ParseResult parseResult = rootCommand.Parse(args);
if (parseResult.Errors.Count == 0 &&
    parseResult.GetValue(pathArgument) is string path &&
    parseResult.GetValue(maxSizeOption) is int maxSize &&
    parseResult.GetValue(minSizeOption) is int minSize &&
    parseResult.GetValue(scaleratioOption) is float scaleratio &&
    parseResult.GetValue(packTextureOption) is bool packTexture &&
    parseResult.GetValue(allTextureOption) is bool allTexture &&
    parseResult.GetValue(modelTextureOption) is bool modelTexture)
{
    var di = new DirectoryInfo(path);
    if (!di.Exists)
    {
        Console.Error.WriteLine($"The path '{path}' does not exist.".Pastel(ConsoleColor.Red));
        return 2;
    }
    IEnumerable<string> pending_textures = [];
    IEnumerable<string> pending_packs = [];
    using var ass_importer = new AssimpContext();
    var mods = di.GetDirectories();
    Console.WriteLine($"Found {mods.Length} mods, processing");
    foreach (var m in mods)
    {
        DirectoryInfo submods = new(Path.Combine(m.FullName, "mods"));
        if (!submods.Exists)
        {
            Console.Error.WriteLine($"Corrupted mod {m}, has no any submod".Pastel(ConsoleColor.Red));
            continue;
        }
        foreach (var item in submods.GetDirectories())
        {
            if(allTexture)
            {
                pending_textures = pending_textures.Concat(item.EnumerateFiles("*.png", SearchOption.AllDirectories)
                    .Select(file => file.FullName));
            }
            else if (modelTexture)
            {
                var fbxFiles = item.EnumerateFiles("*.fbx", SearchOption.AllDirectories);
                var xFiles = item.EnumerateFiles("*.x", SearchOption.AllDirectories);
                var modelFiles = fbxFiles.Concat(xFiles).ToArray();
                Console.WriteLine($"Processing mod '{item.Name}', found {modelFiles.Length} models");
                foreach (var model in modelFiles)
                {
                    try
                    {
                        var ass_models = ass_importer.ImportFile(model.FullName, PostProcessSteps.None);
                        if (!ass_models.HasMaterials)
                            continue;
                        foreach (var mat in ass_models.Materials)
                        {
                            var mat_slots = mat.GetAllMaterialTextures();
                            foreach (var slot in mat_slots)
                            {
                                string tex = slot.FilePath;
                                if (Path.IsPathFullyQualified(tex))
                                {
                                    Console.Error.WriteLine($"Texture path {tex} is fully qualified! this is a stupid erorr for mod, try to use relative path".Pastel(ConsoleColor.Yellow));
                                    tex = Path.GetFileName(tex);
                                }

                                // Extract base name without extension
                                string baseName = Path.GetFileNameWithoutExtension(tex);
                                // Find all files with matching base name and valid image extensions
                                var matchingFiles = item
                                    .EnumerateFiles($"{baseName}.*", SearchOption.AllDirectories)
                                    .Where(file => string.Compare(file.Extension, ".png", true) == 0)
                                    .Select(file => file.FullName);

                                pending_textures = pending_textures.Concat(matchingFiles);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Console.Error.WriteLine($"Meet a exception when parsing model {model.Name}".Pastel(ConsoleColor.Red));
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
    Console.WriteLine($"Found {unique_packs.Length} unique textures to process");

    var sk = new Shrinker(minSize, maxSize, scaleratio);
    if(unique_texture.Length > 0)
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