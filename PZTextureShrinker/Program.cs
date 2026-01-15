using Assimp;
using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

//ceshi
args = ["D:\\SteamLibrary\\steamapps\\workshop\\content\\108600", "-max", "512", "-min", "128", "-mto"];


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

var modelTextureOnlyOption = new Option<bool>("--model-texture-only", "-mto")
{
    DefaultValueFactory = _ => false,
    Description = "Only process model textures."
};

rootCommand.Add(pathArgument);
rootCommand.Add(maxSizeOption);
rootCommand.Add(minSizeOption);
rootCommand.Add(modelTextureOnlyOption);

ParseResult parseResult = rootCommand.Parse(args);
if (parseResult.Errors.Count == 0 &&
    parseResult.GetValue(pathArgument) is string path &&
    parseResult.GetValue(maxSizeOption) is int maxSize &&
    parseResult.GetValue(minSizeOption) is int minSize &&
    parseResult.GetValue(modelTextureOnlyOption) is bool modelTextureOnly)
{
    var di = new DirectoryInfo(path);
    if (!di.Exists)
    {
        Console.Error.WriteLine($"The path '{path}' does not exist.");
        return 2;
    }
    List<string> pending_textures = [];
    using var ass_importer = new AssimpContext();
    var mods = di.GetDirectories();
    Console.WriteLine($"Found {mods.Length} mods, processing");
    foreach (var m in mods)
    {
        DirectoryInfo submods = new(Path.Combine(m.FullName, "mods"));
        if (!submods.Exists)
        {
            Console.Error.WriteLine($"Corrupted mod {m}, has no any submod");
            continue;
        }
        foreach (var item in submods.GetDirectories())
        {
            if (modelTextureOnly)
            {
                var fbxFiles = item.EnumerateFiles("*.fbx", SearchOption.AllDirectories);
                var xFiles = item.EnumerateFiles("*.x", SearchOption.AllDirectories);
                var modelFiles = fbxFiles.Concat(xFiles);
                Console.WriteLine($"Processing mod '{item.Name}', found {modelFiles.Count()} models");
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
                                Console.Error.WriteLine($"Texture path {tex} is fully qualified! this is a stupid erorr for mod, try to use relative path");
                                tex = Path.GetFileName(tex);
                            }
                            
                            // Extract base name without extension
                            string baseName = Path.GetFileNameWithoutExtension(tex);
                            // Define valid image extensions
                            var validExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tga" };
                            
                            // Find all files with matching base name and valid image extensions
                            var matchingFiles = item
                                .EnumerateFiles($"{baseName}.*", SearchOption.AllDirectories)
                                .Where(file => validExtensions.Contains(file.Extension.ToLower()))
                                .Select(file => file.FullName)
                                .ToList();
                            
                            pending_textures.AddRange(matchingFiles);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Meet a exception when parsing model {model.Name}");
                        continue;
                    }
                }
            }
            else
            {
                // Process all texture files
                var validExtensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga" };
                List<string> textureFiles = [];
                
                foreach (var ext in validExtensions)
                {
                    textureFiles.AddRange(item.EnumerateFiles(ext, SearchOption.AllDirectories)
                        .Select(file => file.FullName));
                }
                
                Console.WriteLine($"Processing mod '{item.Name}', found {textureFiles.Count} textures");
                pending_textures.AddRange(textureFiles);
            }
        }
    }
    pending_textures = [.. pending_textures.Distinct()];
    Console.WriteLine($"Found {pending_textures.Count} unique textures to process");
    
    int processedCount = 0;
    int skippedCount = 0;
    
    foreach (var texturePath in pending_textures)
    {
        try
        {
            using var image = Image.Load(texturePath);
            
            int originalWidth = image.Width;
            int originalHeight = image.Height;
            
            // Check if the image needs resizing
            if (originalWidth <= minSize && originalHeight <= minSize)
            {
                Console.WriteLine($"Skipping {Path.GetFileName(texturePath)} (already smaller than minimum size: {originalWidth}x{originalHeight})");
                skippedCount++;
                continue;
            }
            
            // Calculate new dimensions while maintaining aspect ratio
            int newWidth, newHeight;
            if (originalWidth > originalHeight)
            {
                if (originalWidth > maxSize)
                {
                    newWidth = maxSize;
                    newHeight = (int)Math.Round((double)originalHeight * maxSize / originalWidth);
                }
                else
                {
                    newWidth = originalWidth;
                    newHeight = originalHeight;
                }
            }
            else
            {
                if (originalHeight > maxSize)
                {
                    newHeight = maxSize;
                    newWidth = (int)Math.Round((double)originalWidth * maxSize / originalHeight);
                }
                else
                {
                    newWidth = originalWidth;
                    newHeight = originalHeight;
                }
            }
            
            // Ensure minimum size
            newWidth = Math.Max(newWidth, minSize);
            newHeight = Math.Max(newHeight, minSize);
            
            // Only resize if dimensions have changed
            if (newWidth != originalWidth || newHeight != originalHeight)
            {
                Console.WriteLine($"Resizing {Path.GetFileName(texturePath)} from {originalWidth}x{originalHeight} to {newWidth}x{newHeight}");
                
                // Resize the image using high quality resampling
                image.Mutate(x => x.Resize(newWidth, newHeight, KnownResamplers.Lanczos3));
                
                // Save the resized image, overwriting the original
                image.Save(texturePath);
                processedCount++;
            }
            else
            {
                Console.WriteLine($"Skipping {Path.GetFileName(texturePath)} (already within size limits: {originalWidth}x{originalHeight})");
                skippedCount++;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error processing {texturePath}: {ex.Message}");
            skippedCount++;
            continue;
        }
    }
    
    Console.WriteLine($"\nProcessing complete!");
    Console.WriteLine($"Resized: {processedCount} textures");
    Console.WriteLine($"Skipped: {skippedCount} textures");
    return 0;
}

foreach (ParseError parseError in parseResult.Errors)
{
    Console.Error.WriteLine(parseError.Message);
}
return 1;