using Pastel;
using PZPack.Interface;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace PZTextureShrinker;

internal class Shrinker(int min, int max, float ratio)
{
    internal void GetShrinkSize(int originalWidth, int originalHeight, out int newWidth, out int newHeight)
    {
        // Calculate new dimensions using scale_ratio first
        newWidth = (int)Math.Round(originalWidth * ratio);
        newHeight = (int)Math.Round(originalHeight * ratio);
        long maxPixels = (long)max * max;
        long newPixels = (long)newWidth * newHeight;

        if(originalWidth < min || originalHeight < min)
        {
            newWidth = originalWidth;
            newHeight = originalHeight;
            return;
        }
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
                newHeight = max;
                newWidth = (int)Math.Round((double)originalWidth * max / originalHeight);
            }
        }

        // Check if shortest side is less than min, if so scale up with shortest side as min
        if (newWidth < min || newHeight < min)
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
    }
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
                GetShrinkSize(originalWidth, originalHeight, out int newWidth, out int newHeight);
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
                            Console.Error.WriteLine($"{Path.GetFileName(packPath)} is not a PZ pack file".Pastel(ConsoleColor.Red));
                            skippedCount++;
                            continue;
                        }
                }

                if (pack == null)
                {
                    Console.Error.WriteLine($"Failed to open pack file: {Path.GetFileName(packPath)}".Pastel(ConsoleColor.Red));
                    skippedCount++;
                    continue;
                }

                // Get PNG data from pack
                byte[] pngData = pack.Png;
                if (pngData == null || pngData.Length == 0)
                {
                    Console.Error.WriteLine($"No PNG data found in pack: {Path.GetFileName(packPath)}".Pastel(ConsoleColor.Yellow));
                    skippedCount++;
                    continue;
                }

                // Load and process the image
                using var image = Image.Load(pngData);
                int originalWidth = image.Width;
                int originalHeight = image.Height;
                GetShrinkSize(originalWidth, originalHeight, out int newWidth, out int newHeight);
                // Only resize if dimensions have changed
                if (newWidth != originalWidth || newHeight != originalHeight)
                {
                    Console.WriteLine($"Resizing pack {Path.GetFileName(packPath)} from {originalWidth}x{originalHeight} to {newWidth}x{newHeight}".Pastel(ConsoleColor.Green));

                    // Resize the image using high quality resampling
                    image.Mutate(x => x.Resize(newWidth, newHeight, KnownResamplers.Lanczos3));

                    // Save resized image to memory stream
                    using var ms = new MemoryStream();
                    image.SaveAsPng(ms);
                    byte[] resizedPngData = ms.ToArray();

                    // Update pack with resized PNG data
                    pack.Png = resizedPngData;

                    // Update pages
                    float rt_w = (float)newWidth / originalWidth;
                    float rt_h = (float)newHeight / originalHeight;
                    foreach (var pg in pack.Pages)
                    {
                        foreach (var et in pg.Entries)
                        {
                            System.Drawing.Size offset = new((int)Math.Round(et.Offset.Width * rt_w), (int)Math.Round(et.Offset.Height * rt_h));
                            et.Offset = offset;

                            System.Drawing.Size size = new((int)Math.Round(et.Size.Width * rt_w), (int)Math.Round(et.Size.Height * rt_h));
                            et.Size = size;
                        }
                    }

                    // Save the modified pack back to file
                    using var fs = new FileStream(packPath, FileMode.Create, FileAccess.Write);
                    pack.Encode(fs);
                    processedCount++;
                }
                else
                {
                    Console.WriteLine($"Skipping pack {Path.GetFileName(packPath)} (no resizing needed: {originalWidth}x{originalHeight})");

                    skippedCount++;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing pack {Path.GetFileName(packPath)}: {ex.Message}".Pastel(ConsoleColor.Red));
                skippedCount++;
            }
        }
        Console.WriteLine($"Resized: {processedCount} packs");
        Console.WriteLine($"Skipped: {skippedCount} packs");
    }
}
