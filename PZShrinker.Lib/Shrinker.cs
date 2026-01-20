using Assimp;
using PZPack.Interface;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Numerics;
using Image = SixLabors.ImageSharp.Image;

namespace PZShrinker.Lib;

public static class Shrinker
{
    public static void GetTextureShrinkSize(int originalWidth, int originalHeight, out int newWidth, out int newHeight, int min, int max, float ratio)
    {
        // Calculate new dimensions using scale_ratio first
        newWidth = (int)Math.Round(originalWidth * ratio);
        newHeight = (int)Math.Round(originalHeight * ratio);
        long maxPixels = (long)max * max;
        long newPixels = (long)newWidth * newHeight;

        if (originalWidth < min || originalHeight < min)
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
    public static (int processedCount, int skippedCount) ShrinkTexture(string[] list, int min, int max, float ratio)
    {
        int processedCount = 0;
        int skippedCount = 0;
        var exceptions = new List<Exception>();

        foreach (var texturePath in list)
        {
            try
            {
                using var image = Image.Load(texturePath);
                int originalWidth = image.Width;
                int originalHeight = image.Height;
                GetTextureShrinkSize(originalWidth, originalHeight, out int newWidth, out int newHeight, min, max, ratio);
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
                    Console.WriteLine($"Skipping {Path.GetFileName(texturePath)} (no resizing needed: {originalWidth}x{originalHeight})");
                    skippedCount++;
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                skippedCount++;
                continue;
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException("One or more items failed to process.", exceptions);
        }
        return (processedCount, skippedCount);
    }
    public static (int processedCount, int skippedCount) ShrinkPack(string[] list, int min, int max, float ratio)
    {
        int processedCount = 0;
        int skippedCount = 0;
        var exceptions = new List<Exception>();

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
                            Console.Error.WriteLine($"{Path.GetFileName(packPath)} is not a PZ pack file");
                            skippedCount++;
                            continue;
                        }
                }

                if (pack == null)
                {
                    Console.Error.WriteLine($"Failed to open pack file: {Path.GetFileName(packPath)}");
                    skippedCount++;
                    continue;
                }

                // Get PNG data from pack
                byte[] pngData = pack.Png;
                if (pngData == null || pngData.Length == 0)
                {
                    Console.Error.WriteLine($"No PNG data found in pack: {Path.GetFileName(packPath)}");
                    skippedCount++;
                    continue;
                }

                // Load and process the image
                using var image = Image.Load(pngData);
                int originalWidth = image.Width;
                int originalHeight = image.Height;
                GetTextureShrinkSize(originalWidth, originalHeight, out int newWidth, out int newHeight, min, max, ratio);
                // Only resize if dimensions have changed
                if (newWidth != originalWidth || newHeight != originalHeight)
                {
                    Console.WriteLine($"Resizing pack {Path.GetFileName(packPath)} from {originalWidth}x{originalHeight} to {newWidth}x{newHeight}");

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
                exceptions.Add(ex);
                skippedCount++;
            }
        }
        if (exceptions.Count > 0)
        {
            throw new AggregateException("One or more items failed to process.", exceptions);
        }
        return (processedCount, skippedCount);
    }
    public static (int processedCount, int skippedCount) ShrinkModel(string[] list, bool removeOtherUV, bool removeTextureInfo, bool removeTangent, bool removeColor, bool mergeAllMesh)
    {
        int processedCount = 0;
        int skippedCount = 0;
        var importer = new AssimpContext();
        var exceptions = new List<Exception>();
        foreach (var model in list)
        {
            Console.WriteLine($"starting processing model {model}");
            try
            {
                Scene scene = importer.ImportFile(model, PostProcessSteps.Triangulate |
                                   PostProcessSteps.GenerateNormals |
                                   PostProcessSteps.JoinIdenticalVertices |
                                   PostProcessSteps.GenerateUVCoords);
                if (mergeAllMesh)
                {
                    // 全局合并模式：在所有网格之间合并顶点
                    var vertexMap = new Dictionary<VertexHashKey, int>();
                    var globalPositions = new List<Vector3>();
                    var globalNormals = new List<Vector3>();
                    var globalUVs = new List<Vector3>();

                    foreach (var mesh in scene.Meshes)
                    {
                        if (removeTangent)
                        {
                            mesh.Tangents.Clear();
                            mesh.BiTangents.Clear();
                        }

                        if (removeOtherUV)
                        {
                            if (mesh.TextureCoordinateChannelCount > 1)
                            {
                                for (int i = 1; i < mesh.TextureCoordinateChannelCount; i++)
                                {
                                    mesh.TextureCoordinateChannels[i].Clear();
                                }
                            }
                        }

                        if (removeColor)
                        {
                            foreach (var ch in mesh.VertexColorChannels)
                            {
                                ch.Clear();
                            }
                        }
                        if (mesh.VertexCount == 0 || mesh.Faces.Count == 0)
                        {
                            skippedCount++;
                            continue;
                        }
                        
                        var newIndices = new List<int[]>();
                        foreach (var face in mesh.Faces)
                        {
                            if (face.IndexCount != 3)
                                continue;
                            int[] triIndices = new int[3];
                            for (int i = 0; i < 3; i++)
                            {
                                int origIdx = face.Indices[i];
                                var pos = mesh.Vertices[origIdx];
                                var normal = mesh.HasNormals
                                    ? mesh.Normals[origIdx]
                                    : new Vector3(0, 1, 0);
                                var uvVec3 = mesh.HasTextureCoords(0)
                                    ? mesh.TextureCoordinateChannels[0][origIdx]
                                    : new Vector3(0, 0, 0);
                                var uv = new Vector2(uvVec3.X, uvVec3.Y);
                                var key = new VertexHashKey(pos, normal, uv);
                                if (!vertexMap.TryGetValue(key, out int newIndex))
                                {
                                    newIndex = globalPositions.Count;
                                    vertexMap[key] = newIndex;

                                    globalPositions.Add(pos);
                                    globalNormals.Add(normal);
                                    globalUVs.Add(uvVec3);
                                }
                                triIndices[i] = newIndex;
                            }
                            newIndices.Add(triIndices);
                        }
                        
                        mesh.Faces.Clear();
                        foreach (var face in newIndices)
                        {
                            mesh.Faces.Add(new Face(face));
                        }
                    }

                    // 现在更新所有网格的顶点数据，使用共享的全局顶点列表
                    foreach (var mesh in scene.Meshes)
                    {
                        if (mesh.VertexCount == 0 || mesh.Faces.Count == 0)
                            continue;

                        mesh.Vertices.Clear();
                        mesh.Normals.Clear();
                        if (mesh.TextureCoordinateChannelCount > 0)
                        {
                            mesh.TextureCoordinateChannels[0].Clear();
                        }

                        mesh.Vertices.AddRange(globalPositions);
                        mesh.Normals.AddRange(globalNormals);
                        if (globalUVs.Count > 0 && mesh.TextureCoordinateChannelCount > 0)
                        {
                            mesh.TextureCoordinateChannels[0].AddRange(globalUVs);
                        }
                    }
                }
                else
                {
                    // 单网格合并模式：只在每个网格内部合并顶点
                    foreach (var mesh in scene.Meshes)
                    {
                        if (removeTangent)
                        {
                            mesh.Tangents.Clear();
                            mesh.BiTangents.Clear();
                        }

                        if (removeOtherUV)
                        {
                            if (mesh.TextureCoordinateChannelCount > 1)
                            {
                                for (int i = 1; i < mesh.TextureCoordinateChannelCount; i++)
                                {
                                    mesh.TextureCoordinateChannels[i].Clear();
                                }
                            }
                        }

                        if (removeColor)
                        {
                            foreach (var ch in mesh.VertexColorChannels)
                            {
                                ch.Clear();
                            }
                        }
                        if (mesh.VertexCount == 0 || mesh.Faces.Count == 0)
                        {
                            skippedCount++;
                            continue;
                        }
                        var vertexMap = new Dictionary<VertexHashKey, int>();
                        var newPositions = new List<Vector3>();
                        var newNormals = new List<Vector3>();
                        var newUVs = new List<Vector3>();
                        var newIndices = new List<int[]>();
                        foreach (var face in mesh.Faces)
                        {
                            if (face.IndexCount != 3)
                                continue;
                            int[] triIndices = new int[3];
                            for (int i = 0; i < 3; i++)
                            {
                                int origIdx = face.Indices[i];
                                var pos = mesh.Vertices[origIdx];
                                var normal = mesh.HasNormals
                                    ? mesh.Normals[origIdx]
                                    : new Vector3(0, 1, 0);
                                var uvVec3 = mesh.HasTextureCoords(0)
                                    ? mesh.TextureCoordinateChannels[0][origIdx]
                                    : new Vector3(0, 0, 0);
                                var uv = new Vector2(uvVec3.X, uvVec3.Y);
                                var key = new VertexHashKey(pos, normal, uv);
                                if (!vertexMap.TryGetValue(key, out int newIndex))
                                {
                                    newIndex = newPositions.Count;
                                    vertexMap[key] = newIndex;

                                    newPositions.Add(pos);
                                    newNormals.Add(normal);
                                    newUVs.Add(uvVec3);
                                }
                                triIndices[i] = newIndex;
                            }
                            newIndices.Add(triIndices);
                        }
                        mesh.Vertices.Clear();
                        mesh.Normals.Clear();
                        mesh.TextureCoordinateChannels[0].Clear();
                        mesh.Vertices.AddRange(newPositions);
                        mesh.Normals.AddRange(newNormals);
                        if (newUVs.Count > 0)
                        {
                            mesh.TextureCoordinateChannels[0].AddRange(newUVs);
                        }
                        mesh.Faces.Clear();
                        foreach (var face in newIndices)
                        {
                            mesh.Faces.Add(new Face(face));
                        }
                    }
                }

                if (removeTextureInfo)
                {
                    if (scene.HasMaterials)
                        scene.Materials.Clear();
                    if (scene.HasTextures)
                        scene.Textures.Clear();
                }

                string formatId = Path.GetExtension(model).ToLower();
                importer.ExportFile(scene, model, formatId);

                processedCount++;
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                skippedCount++;
            }
        }
        if (exceptions.Count > 0)
        {
            throw new AggregateException("One or more items failed to process.", exceptions);
        }
        return (processedCount, skippedCount);
    }
}
