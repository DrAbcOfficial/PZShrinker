using Assimp;
using Pastel;
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
    Description = "Only process model textures."
};

var validExtensions = new[] { ".png" };

rootCommand.Add(pathArgument);
rootCommand.Add(maxSizeOption);
rootCommand.Add(minSizeOption);
rootCommand.Add(scaleratioOption);
rootCommand.Add(modelTextureOption);

ParseResult parseResult = rootCommand.Parse(args);
if (parseResult.Errors.Count == 0 &&
    parseResult.GetValue(pathArgument) is string path &&
    parseResult.GetValue(maxSizeOption) is int maxSize &&
    parseResult.GetValue(minSizeOption) is int minSize &&
    parseResult.GetValue(scaleratioOption) is float scaleratio &&
    parseResult.GetValue(modelTextureOption) is bool modelTexture)
{
    var di = new DirectoryInfo(path);
    if (!di.Exists)
    {
        Console.Error.WriteLine($"The path '{path}' does not exist.".Pastel(ConsoleColor.Red));
        return 2;
    }
    IEnumerable<string> pending_textures = Enumerable.Empty<string>();
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
            if (modelTexture)
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
                                    .Where(file => validExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
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
        }
    }
    pending_textures = pending_textures.Distinct();
    // 使用ToArray避免多次枚举
    var uniqueTextures = pending_textures.ToArray();
    Console.WriteLine($"Found {uniqueTextures.Length} unique textures to process");

    int processedCount = 0;
    int skippedCount = 0;

    foreach (var texturePath in uniqueTextures)
    {
        try
        {
            using var image = Image.Load(texturePath);

            int originalWidth = image.Width;
            int originalHeight = image.Height;

            // Calculate new dimensions using scale_ratio first
            int newWidth = (int)Math.Round(originalWidth * scaleratio);
            int newHeight = (int)Math.Round(originalHeight * scaleratio);
            long maxPixels = (long)maxSize * maxSize;
            long minPixels = (long)minSize * minSize;
            long newPixels = (long)newWidth * newHeight;

            // Check if new pixels exceed max*max, if so scale down with longest side as max
            if (newPixels > maxPixels)
            {
                if (newWidth > newHeight)
                {
                    newWidth = maxSize;
                    newHeight = (int)Math.Round((double)originalHeight * maxSize / originalWidth);
                }
                else
                {
                    newHeight = maxSize;
                    newWidth = (int)Math.Round((double)originalWidth * maxSize / originalHeight);
                }
                newPixels = (long)newWidth * newHeight;
            }

            // Check if new pixels are less than min*min, if so scale up with shortest side as min
            if (newPixels < minPixels)
            {
                if (newWidth < newHeight)
                {
                    newWidth = minSize;
                    newHeight = (int)Math.Round((double)originalHeight * minSize / originalWidth);
                }
                else
                {
                    newHeight = minSize;
                    newWidth = (int)Math.Round((double)originalWidth * minSize / originalHeight);
                }
            }

            // Ensure new dimensions are at least 1x1
            newWidth = Math.Max(newWidth, 1);
            newHeight = Math.Max(newHeight, 1);

            // Only resize if dimensions have changed
            if (newWidth != originalWidth || newHeight != originalHeight)
            {
                Console.WriteLine($"Resizing {Path.GetFileName(texturePath)} from {originalWidth}x{originalHeight} to {newWidth}x{newHeight}".Pastel(ConsoleColor.Green));

                // Resize the image using high quality resampling
                image.Mutate(x => x.Resize(newWidth, newHeight, KnownResamplers.Lanczos3));

                // Save the resized image, overwriting the original
                image.Save(texturePath);
                processedCount++;
            }
            else
            {
                Console.WriteLine($"Skipping {Path.GetFileName(texturePath)} (no resizing needed: {originalWidth}x{originalHeight})");
                skippedCount++;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error processing {texturePath}: {ex.Message}".Pastel(ConsoleColor.Red));
            skippedCount++;
            continue;
        }
    }

    Console.WriteLine($"\nProcessing complete!".Pastel(ConsoleColor.Green));
    Console.WriteLine($"Resized: {processedCount} textures");
    Console.WriteLine($"Skipped: {skippedCount} textures");
    return 0;
}

foreach (ParseError parseError in parseResult.Errors)
{
    Console.Error.WriteLine(parseError.Message.Pastel(ConsoleColor.Red));
}
return 1;