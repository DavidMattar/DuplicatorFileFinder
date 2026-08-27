using System.IO.Abstractions.TestingHelpers;
using DuplicatorFinder.Core.Detection;
using DuplicatorFinder.Core.Hashing;
using DuplicatorFinder.Core.Models;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace DuplicatorFinder.Core.Tests.Detection;

/// <summary>
/// Testa <see cref="ImageSimilarityDetector"/> de ponta a ponta com imagens reais geradas em
/// memória (não apenas bytes arbitrários) — necessário porque o detector decodifica o
/// conteúdo da imagem de verdade para calcular o hash perceptual.
/// </summary>
public class ImageSimilarityDetectorTests
{
    [Fact]
    public async Task DetectAsync_GroupsResizedAndRecompressedCopyWithOriginal()
    {
        var fileSystem = new MockFileSystem();

        using var basePattern = CreateSoftBlobPattern(400, 300, centerXFraction: 0.5, centerYFraction: 0.5);
        fileSystem.AddFile(@"C:\photos\original.png", new MockFileData(EncodePng(basePattern)));

        using var resized = basePattern.CloneAs<Rgba32>();
        resized.Mutate(ctx => ctx.Resize(200, 150));
        fileSystem.AddFile(@"C:\photos\resized.jpg", new MockFileData(EncodeJpeg(resized, quality: 60)));

        using var differentPattern = CreateSoftBlobPattern(400, 300, centerXFraction: 0.15, centerYFraction: 0.85);
        fileSystem.AddFile(@"C:\photos\different.png", new MockFileData(EncodePng(differentPattern)));

        var detector = new ImageSimilarityDetector(new ImageHasher(), fileSystem);

        var candidates = new[]
        {
            MakeEntry(fileSystem, @"C:\photos\original.png"),
            MakeEntry(fileSystem, @"C:\photos\resized.jpg"),
            MakeEntry(fileSystem, @"C:\photos\different.png"),
        };

        // Threshold deliberadamente mais tolerante que o padrão da UI (0.90): resize + JPEG
        // de baixa qualidade sempre altera vários bits do hash perceptual mesmo para a MESMA
        // imagem — este teste valida que o pipeline (bucket index + union-find + threshold)
        // funciona, não qual é o threshold "ideal" de produção (esse é um ajuste de UX,
        // configurável pelo usuário via slider).
        var options = new ScanOptions
        {
            RootFolders = [@"C:\photos"],
            ImageSimilarityThreshold = 0.65,
            MaxDegreeOfParallelism = 2,
        };

        var groups = await detector.DetectAsync(candidates, options, progress: null, CancellationToken.None);

        groups.Should().HaveCount(1);
        groups[0].Files.Select(f => f.File.FullPath)
            .Should().BeEquivalentTo([@"C:\photos\original.png", @"C:\photos\resized.jpg"]);
    }

    [Fact]
    public async Task DetectAsync_ReturnsNoGroups_WhenFewerThanTwoCandidates()
    {
        var fileSystem = new MockFileSystem();
        using var image = CreateSoftBlobPattern(100, 100, centerXFraction: 0.5, centerYFraction: 0.5);
        fileSystem.AddFile(@"C:\photos\only.png", new MockFileData(EncodePng(image)));

        var detector = new ImageSimilarityDetector(new ImageHasher(), fileSystem);
        var candidates = new[] { MakeEntry(fileSystem, @"C:\photos\only.png") };

        var groups = await detector.DetectAsync(candidates, new ScanOptions { RootFolders = [@"C:\photos"] }, progress: null, CancellationToken.None);

        groups.Should().BeEmpty();
    }

    /// <summary>
    /// Gera uma "mancha" radial suave (sem bordas nítidas), determinística, centrada na
    /// fração informada de largura/altura. Hash perceptual é baseado em frequências baixas
    /// da imagem (após reduzi-la e convertê-la para escala de cinza) — um degradê suave se
    /// comporta de forma muito mais parecida com uma foto real do que um padrão geométrico
    /// de bordas duras (blocos, xadrez), que introduz artefatos de alta frequência ao ser
    /// redimensionado e distorce a comparação.
    /// </summary>
    private static Image<Rgba32> CreateSoftBlobPattern(int width, int height, double centerXFraction, double centerYFraction)
    {
        var image = new Image<Rgba32>(width, height);
        var centerX = width * centerXFraction;
        var centerY = height * centerYFraction;
        var maxDistance = Math.Sqrt((width * width) + (height * height)) / 2.0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var distance = Math.Sqrt(((x - centerX) * (x - centerX)) + ((y - centerY) * (y - centerY)));
                var intensity = Math.Clamp(1.0 - (distance / maxDistance), 0.0, 1.0);
                var value = (byte)(intensity * 255);
                image[x, y] = new Rgba32(value, (byte)((value / 2) + 40), (byte)(255 - value));
            }
        }

        return image;
    }

    private static byte[] EncodePng(Image<Rgba32> image)
    {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static byte[] EncodeJpeg(Image<Rgba32> image, int quality)
    {
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = quality });
        return stream.ToArray();
    }

    private static FileEntry MakeEntry(MockFileSystem fileSystem, string path)
    {
        var info = fileSystem.FileInfo.New(path);
        return new FileEntry(info.FullName, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc, info.Extension);
    }
}
