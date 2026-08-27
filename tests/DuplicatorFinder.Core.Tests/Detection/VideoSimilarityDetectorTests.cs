using System.Numerics;
using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Detection;
using DuplicatorFinder.Core.Models;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DuplicatorFinder.Core.Tests.Detection;

/// <summary>
/// Testa <see cref="VideoSimilarityDetector"/> com <see cref="IVideoFrameExtractor"/> e
/// <see cref="IImageHasher"/> substituídos por dublês (NSubstitute) — o ffmpeg real já foi
/// validado manualmente (geração de vídeo sintético, leitura de metadados e extração de
/// frame) durante o desenvolvimento; aqui o objetivo é testar a lógica de negócio do
/// detector (pré-filtro por duração, pareamento, threshold) de forma rápida e sem depender
/// de um processo externo.
/// </summary>
public class VideoSimilarityDetectorTests
{
    [Fact]
    public async Task DetectAsync_GroupsVideosWithSimilarDurationAndFrames_AndSkipsFrameExtractionForUnrelatedDuration()
    {
        var frameExtractor = Substitute.For<IVideoFrameExtractor>();
        var imageHasher = Substitute.For<IImageHasher>();

        var videoA = MakeEntry(@"C:\videos\a.mp4");
        var videoB = MakeEntry(@"C:\videos\b.mp4");
        var videoC = MakeEntry(@"C:\videos\c.mp4");

        frameExtractor.GetMetadataAsync(videoA.FullPath, Arg.Any<CancellationToken>())
            .Returns((TimeSpan.FromSeconds(60), 1920, 1080));
        frameExtractor.GetMetadataAsync(videoB.FullPath, Arg.Any<CancellationToken>())
            .Returns((TimeSpan.FromSeconds(61), 1280, 720));
        // Duração muito diferente de A e B: nunca deve nem chegar a ter frames extraídos.
        frameExtractor.GetMetadataAsync(videoC.FullPath, Arg.Any<CancellationToken>())
            .Returns((TimeSpan.FromSeconds(600), 1920, 1080));

        var (framesA, framesB) = (new[] { new MemoryStream(), new MemoryStream(), new MemoryStream() },
                                   new[] { new MemoryStream(), new MemoryStream(), new MemoryStream() });

        frameExtractor.ExtractFramesAsync(videoA.FullPath, Arg.Any<TimeSpan[]>(), Arg.Any<CancellationToken>())
            .Returns(framesA);
        frameExtractor.ExtractFramesAsync(videoB.FullPath, Arg.Any<TimeSpan[]>(), Arg.Any<CancellationToken>())
            .Returns(framesB);

        // Frames correspondentes (mesma posição) de A e B recebem o mesmo hash -> distância zero.
        var hashByFrame = new Dictionary<Stream, ulong>
        {
            [framesA[0]] = 0x1111_1111_1111_1111UL, [framesB[0]] = 0x1111_1111_1111_1111UL,
            [framesA[1]] = 0x2222_2222_2222_2222UL, [framesB[1]] = 0x2222_2222_2222_2222UL,
            [framesA[2]] = 0x3333_3333_3333_3333UL, [framesB[2]] = 0x3333_3333_3333_3333UL,
        };
        imageHasher.ComputeHash(Arg.Any<Stream>()).Returns(callInfo => hashByFrame[callInfo.Arg<Stream>()]);
        imageHasher.HammingDistance(Arg.Any<ulong>(), Arg.Any<ulong>())
            .Returns(callInfo => BitOperations.PopCount(callInfo.ArgAt<ulong>(0) ^ callInfo.ArgAt<ulong>(1)));

        var detector = new VideoSimilarityDetector(frameExtractor, imageHasher);
        var options = new ScanOptions { RootFolders = [@"C:\videos"], VideoSimilarityThreshold = 0.90 };

        var groups = await detector.DetectAsync([videoA, videoB, videoC], options, progress: null, CancellationToken.None);

        groups.Should().HaveCount(1);
        groups[0].Files.Select(f => f.File.FullPath).Should().BeEquivalentTo([videoA.FullPath, videoB.FullPath]);

        await frameExtractor.DidNotReceive().ExtractFramesAsync(videoC.FullPath, Arg.Any<TimeSpan[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetectAsync_DoesNotGroupVideos_WithSimilarDurationButDifferentFrames()
    {
        var frameExtractor = Substitute.For<IVideoFrameExtractor>();
        var imageHasher = Substitute.For<IImageHasher>();

        var videoA = MakeEntry(@"C:\videos\a.mp4");
        var videoB = MakeEntry(@"C:\videos\b.mp4");

        frameExtractor.GetMetadataAsync(videoA.FullPath, Arg.Any<CancellationToken>())
            .Returns((TimeSpan.FromSeconds(60), 1920, 1080));
        frameExtractor.GetMetadataAsync(videoB.FullPath, Arg.Any<CancellationToken>())
            .Returns((TimeSpan.FromSeconds(60), 1920, 1080));

        var framesA = new[] { new MemoryStream(), new MemoryStream(), new MemoryStream() };
        var framesB = new[] { new MemoryStream(), new MemoryStream(), new MemoryStream() };

        frameExtractor.ExtractFramesAsync(videoA.FullPath, Arg.Any<TimeSpan[]>(), Arg.Any<CancellationToken>()).Returns(framesA);
        frameExtractor.ExtractFramesAsync(videoB.FullPath, Arg.Any<TimeSpan[]>(), Arg.Any<CancellationToken>()).Returns(framesB);

        // Hashes de A e B totalmente diferentes (todos os 64 bits opostos) em todos os frames.
        var hashByFrame = new Dictionary<Stream, ulong>();
        foreach (var frame in framesA)
        {
            hashByFrame[frame] = 0x0000_0000_0000_0000UL;
        }

        foreach (var frame in framesB)
        {
            hashByFrame[frame] = 0xFFFF_FFFF_FFFF_FFFFUL;
        }

        imageHasher.ComputeHash(Arg.Any<Stream>()).Returns(callInfo => hashByFrame[callInfo.Arg<Stream>()]);
        imageHasher.HammingDistance(Arg.Any<ulong>(), Arg.Any<ulong>())
            .Returns(callInfo => BitOperations.PopCount(callInfo.ArgAt<ulong>(0) ^ callInfo.ArgAt<ulong>(1)));

        var detector = new VideoSimilarityDetector(frameExtractor, imageHasher);
        var options = new ScanOptions { RootFolders = [@"C:\videos"], VideoSimilarityThreshold = 0.90 };

        var groups = await detector.DetectAsync([videoA, videoB], options, progress: null, CancellationToken.None);

        groups.Should().BeEmpty();
    }

    private static FileEntry MakeEntry(string path) =>
        new(path, SizeBytes: 5_000_000, CreatedUtc: DateTime.UtcNow, ModifiedUtc: DateTime.UtcNow, Extension: ".mp4");
}
