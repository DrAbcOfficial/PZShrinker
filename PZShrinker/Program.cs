using Pastel;
using PZShrinker.Lib;
using System.CommandLine;
using System.CommandLine.Parsing;

var rootCommand = new RootCommand("Project zomboid texture size shrinker");

var pathArgument = new Argument<string>("path")
{
    Description = "The path to the workshop folder.(e.g: D:\\SteamLibrary\\steamapps\\workshop\\content\\108600)",
    Arity = ArgumentArity.ExactlyOne
};

var maxSizeOption = new Option<int>("--texture-max-size", "-tmax")
{
    DefaultValueFactory = _ => 512,
    Description = "The maximum size of the texture. Default is 512."
};
var minSizeOption = new Option<int>("--texture-min-size", "-tmin")
{
    DefaultValueFactory = _ => 64,
    Description = "The minimum size of the texture. Default is 64."
};
var scaleratioOption = new Option<float>("--texture-scale-ratio", "-tsr")
{
    DefaultValueFactory = _ => 0.25f,
    Description = "The scale ratio"
};

var modelRemoveOtherUV = new Option<bool>("--model-remove-other-uv", "-mrouv")
{
    DefaultValueFactory = _ => false,
    Description = "Remove other UV channel (1 ~ 7), keep UV0 only"
};
var modelRemoveTextureInfo = new Option<bool>("--model-remove-texture", "-mrtt")
{
    DefaultValueFactory = _ => false,
    Description = "Remove all embedded textures and embedded materials"
};
var modelRemoveTangent = new Option<bool>("--model-remove-tangents", "-mrtg")
{
    DefaultValueFactory = _ => false,
    Description = "Remove tangents and bitagents"
};
var modelRemoveColor = new Option<bool>("--model-remove-vertex-color", "-mrvc")
{
    DefaultValueFactory = _ => false,
    Description = "Remove all vertex colors"
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
var fbxModelOption = new Option<bool>("--fbx-model", "-fm")
{
    DefaultValueFactory = _ => false,
    Description = "Process all FBX model files"
};
var d3dModelOption = new Option<bool>("--x-model", "-xm")
{
    DefaultValueFactory = _ => false,
    Description = "Process all X model files"
};


rootCommand.Add(pathArgument);
rootCommand.Add(maxSizeOption);
rootCommand.Add(minSizeOption);
rootCommand.Add(scaleratioOption);
rootCommand.Add(modelTextureOption);
rootCommand.Add(packTextureOption);
rootCommand.Add(iconTextureOption);
rootCommand.Add(allTextureOption);
rootCommand.Add(fbxModelOption);
rootCommand.Add(d3dModelOption);
rootCommand.Add(modelRemoveColor);
rootCommand.Add(modelRemoveOtherUV);
rootCommand.Add(modelRemoveTangent);
rootCommand.Add(modelRemoveTextureInfo);


ParseResult parseResult = rootCommand.Parse(args);
if (parseResult.Errors.Count == 0 &&
    parseResult.GetValue(pathArgument) is string path &&
    parseResult.GetValue(maxSizeOption) is int maxSize &&
    parseResult.GetValue(minSizeOption) is int minSize &&
    parseResult.GetValue(scaleratioOption) is float scaleratio &&
    parseResult.GetValue(packTextureOption) is bool packTexture &&
    parseResult.GetValue(iconTextureOption) is bool iconTexture &&
    parseResult.GetValue(allTextureOption) is bool allTexture &&
    parseResult.GetValue(fbxModelOption) is bool fbxModel &&
    parseResult.GetValue(d3dModelOption) is bool d3dModel &&
    parseResult.GetValue(modelRemoveColor) is bool removeColor &&
    parseResult.GetValue(modelRemoveOtherUV) is bool removeOtherUV &&
    parseResult.GetValue(modelRemoveTextureInfo) is bool removeOtherTextureInfo &&
    parseResult.GetValue(modelRemoveTangent) is bool removeTangent &&
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

    var mods = di.GetDirectories();
    Console.WriteLine($"Found {mods.Length} mods, processing");

    IEnumerable<string> texture = [];
    IEnumerable<string> packs = [];
    IEnumerable<string> models = [];
    foreach (var mod in mods)
    {
        if (allTexture || iconTexture || modelTexture)
            texture = texture.Concat(Finder.FindTextures(mod.FullName, allTexture, iconTexture, modelTexture));
        if (packTexture)
            packs = packs.Concat(Finder.FindPacks(mod.FullName));
        if (fbxModel)
            models = models.Concat(Finder.FindFBXModels(mod.FullName));
        if (d3dModel)
            models = models.Concat(Finder.FindD3DModels(mod.FullName));
    }

    string[] unique_texture = [.. texture.Distinct()];
    string[] unique_packs = [.. packs.Distinct()];
    string[] unique_models = [.. models.Distinct()];

    Console.WriteLine($"Found {unique_texture.Length} unique textures to process");
    Console.WriteLine($"Found {unique_packs.Length} unique texture packs to process");
    Console.WriteLine($"Found {unique_models.Length} unique unique_models to process");

    if (unique_texture.Length > 0)
    {
        try
        {
            (int p, int s) = Shrinker.ShrinkTexture(unique_texture, minSize, maxSize, scaleratio);
            Console.WriteLine($"Processed {p} textures, skipped {s}".Pastel(ConsoleColor.Green));
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
    if (unique_packs.Length > 0)
    {
        try
        {
            (int p, int s) = Shrinker.ShrinkPack(unique_packs, minSize, maxSize, scaleratio);
            Console.WriteLine($"Processed {p} tiles packs, skipped {s}".Pastel(ConsoleColor.Green));
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
    if (unique_models.Length > 0)
    {
        try
        {
            (int p, int s) = Shrinker.ShrinkModel(unique_models, removeOtherUV, removeOtherTextureInfo, removeTangent, removeColor);
            Console.WriteLine($"Processed {p} models, skipped {s}".Pastel(ConsoleColor.Green));
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
    Console.WriteLine($"\nProcessing complete!".Pastel(ConsoleColor.Green));
    return 0;
}

foreach (ParseError parseError in parseResult.Errors)
{
    Console.Error.WriteLine(parseError.Message.Pastel(ConsoleColor.Red));
}
return 1;