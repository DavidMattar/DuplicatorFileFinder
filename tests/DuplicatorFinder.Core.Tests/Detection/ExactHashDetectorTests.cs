using System.IO.Abstractions.TestingHelpers;
using DuplicatorFinder.Core.Detection;
using DuplicatorFinder.Core.Hashing;
using DuplicatorFinder.Core.Models;
using FluentAssertions;
using Xunit;

namespace DuplicatorFinder.Core.Tests.Detection;

/// <summary>
/// Testa <see cref="ExactHashDetector"/> de ponta a ponta (usando o <see cref="FileHasher"/>
/// real sobre um <see cref="MockFileSystem"/>), para garantir que o pipeline
/// tamanho → hash rápido → hash completo realmente agrupa apenas arquivos com conteúdo idêntico.
/// </summary>
public class ExactHashDetectorTests
{
    [Fact]
    public async Task DetectAsync_GroupsFilesWithIdenticalContent()
    {
        var fileSystem = new MockFileSystem();
        var content = new byte[200_000];
        new Random(42).NextBytes(content);

        fileSystem.AddFile(@"C:\files\original.bin", new MockFileData(content));
        fileSystem.AddFile(@"C:\files\copy.bin", new MockFileData(content));
        fileSystem.AddFile(@"C:\files\different.bin", new MockFileData(new byte[200_000]));

        var detector = new ExactHashDetector(new FileHasher(fileSystem));

        var candidates = new[]
        {
            MakeEntry(fileSystem, @"C:\files\original.bin"),
            MakeEntry(fileSystem, @"C:\files\copy.bin"),
            MakeEntry(fileSystem, @"C:\files\different.bin"),
        };

        var groups = await detector.DetectAsync(
            candidates,
            new ScanOptions { RootFolders = [@"C:\files"], MaxDegreeOfParallelism = 2 },
            progress: null,
            CancellationToken.None);

        groups.Should().HaveCount(1);
        groups[0].Files.Select(f => f.File.FullPath)
            .Should().BeEquivalentTo([@"C:\files\original.bin", @"C:\files\copy.bin"]);
    }

    [Fact]
    public async Task DetectAsync_ReturnsNoGroups_WhenAllFilesAreUnique()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(@"C:\files\a.bin", new MockFileData(new byte[] { 1, 2, 3 }));
        fileSystem.AddFile(@"C:\files\b.bin", new MockFileData(new byte[] { 4, 5, 6 }));

        var detector = new ExactHashDetector(new FileHasher(fileSystem));

        var candidates = new[]
        {
            MakeEntry(fileSystem, @"C:\files\a.bin"),
            MakeEntry(fileSystem, @"C:\files\b.bin"),
        };

        var groups = await detector.DetectAsync(
            candidates,
            new ScanOptions { RootFolders = [@"C:\files"] },
            progress: null,
            CancellationToken.None);

        groups.Should().BeEmpty();
    }

    private static FileEntry MakeEntry(MockFileSystem fileSystem, string path)
    {
        var info = fileSystem.FileInfo.New(path);
        return new FileEntry(info.FullName, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc, info.Extension);
    }
}
