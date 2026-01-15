using Pastel;
using PZPack;
using PZPack.Interface;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace PZTextureShrinker;

internal class Shrinker(int min, int max, float ratio)
{
    internal void ShrinkTexture(string[] list)
    {
        int processedCount = 0;
        int skippedCount = 0;

        foreach (var texturePath in list)
        {
            try
            {
                using var image = Image.Load(texturePath);

                int originalWidth = image.Width;
                int originalHeight = image.Height;

                // Calculate new dimensions using scale_ratio first
                int newWidth = (int)Math.Round(originalWidth * ratio);
                int newHeight = (int)Math.Round(originalHeight * ratio);
                long maxPixels = (long)max * max;
                long minPixels = (long)min * min;
                long newPixels = (long)newWidth * newHeight;

                // Check if new pixels exceed max*max, if so scale down with longest side as max
                if (newPixels > maxPixels)
                {
                    if (newWidth > newHeight)
                    {
                        newWidth = max;
                        newHeight = (int)Math.Round((double)originalHeight * max / originalWidth);
                    }
                    else
                    {
                        newHeight = min;
                        newWidth = (int)Math.Round((double)originalWidth * min / originalHeight);
                    }
                    newPixels = (long)newWidth * newHeight;
                }

                // Check if new pixels are less than min*min, if so scale up with shortest side as min
                if (newPixels < minPixels)
                {
                    if (newWidth < newHeight)
                    {
                        newWidth = min;
                        newHeight = (int)Math.Round((double)originalHeight * min / originalWidth);
                    }
                    else
                    {
                        newHeight = min;
                        newWidth = (int)Math.Round((double)originalWidth * min / originalHeight);
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
        Console.WriteLine($"Resized: {processedCount} textures");
        Console.WriteLine($"Skipped: {skippedCount} textures");
    }

    internal void ShrinkPack(string[] list)
    {
        int processedCount = 0;
        int skippedCount = 0;

        foreach (var packPath in list)
        {
            try
            {
                var packType = PZPack.PZPack.IsFileAPZPack(packPath);
                IPZPack? pack = null;
                switch (packType)
                {
                    case IPZPack.PZPackType.V1:
                        {
                            pack = PZPack.PZPack.OpenV1(packPath);
                            break;
                        }
                    case IPZPack.PZPackType.V2:
                        {
                            pack = PZPack.PZPack.OpenV2(packPath);
                            break;
                        }
                    default:
                        {
                            Console.Error.WriteLine($"{packType} is not a PZ pack file".Pastel(ConsoleColor.Red));
                            continue;
                        }
                }


            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString().Pastel(ConsoleColor.Red));
            }
        }
        Console.WriteLine($"Resized: {processedCount} pack");
        Console.WriteLine($"Skipped: {skippedCount} pack");
    }
}
