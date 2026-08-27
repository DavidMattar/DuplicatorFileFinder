using System.IO.Abstractions.TestingHelpers;
using DuplicatorFinder.Core.Models;
using DuplicatorFinder.Core.Scanning;
using FluentAssertions;
using Xunit;

namespace DuplicatorFinder.Core.Tests.Scanning;

/// <summary>
/// Testa <see cref="FileScanner"/> usando um <see cref="MockFileSystem"/> em memória, sem
/// tocar no disco real — permite verificar os filtros e a recursão de subpastas de forma
/// rápida e determinística.
/// </summary>
public class FileScannerTests
{
    [Fact]
    public async Task ScanAsync_ReturnsAllFiles_WhenIncludingSubfolders()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(@"C:\photos\a.jpg", new MockFileData("aaaa"));
        fileSystem.AddFile(@"C:\photos\sub\b.jpg", new MockFileData("bbbb"));

        var scanner = new FileScanner(fileSystem);
        var options = new ScanOptions { RootFolders = [@"C:\photos"], IncludeSubfolders = true };

        var results = await CollectAsync(scanner, options);

        results.Should().HaveCount(2);
        results.Select(r => r.FullPath).Should().BeEquivalentTo([@"C:\photos\a.jpg", @"C:\photos\sub\b.jpg"]);
    }

    [Fact]
    public async Task ScanAsync_ExcludesSubfolders_WhenIncludeSubfoldersIsFalse()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(@"C:\photos\a.jpg", new MockFileData("aaaa"));
        fileSystem.AddFile(@"C:\photos\sub\b.jpg", new MockFileData("bbbb"));

        var scanner = new FileScanner(fileSystem);
        var options = new ScanOptions { RootFolders = [@"C:\photos"], IncludeSubfolders = false };

        var results = await CollectAsync(scanner, options);

        results.Should().ContainSingle(r => r.FullPath == @"C:\photos\a.jpg");
    }

    [Fact]
    public async Task ScanAsync_AppliesMinFileSizeFilter()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(@"C:\photos\small.jpg", new MockFileData(new byte[10]));
        fileSystem.AddFile(@"C:\photos\big.jpg", new MockFileData(new byte[10_000]));

        var scanner = new FileScanner(fileSystem);
        var options = new ScanOptions { RootFolders = [@"C:\photos"], MinFileSizeBytes = 1000 };

        var results = await CollectAsync(scanner, options);

        results.Should().ContainSingle(r => r.FullPath == @"C:\photos\big.jpg");
    }

    [Fact]
    public async Task ScanAsync_AppliesExcludeExtensionsFilter()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(@"C:\photos\a.jpg", new MockFileData("aaaa"));
        fileSystem.AddFile(@"C:\photos\notes.txt", new MockFileData("bbbb"));

        var scanner = new FileScanner(fileSystem);
        var options = new ScanOptions { RootFolders = [@"C:\photos"], ExcludeExtensions = [".txt"] };

        var results = await CollectAsync(scanner, options);

        results.Should().ContainSingle(r => r.FullPath == @"C:\photos\a.jpg");
    }

    private static async Task<List<FileEntry>> CollectAsync(FileScanner scanner, ScanOptions options)
    {
        var results = new List<FileEntry>();
        await foreach (var entry in scanner.ScanAsync(options, progress: null, CancellationToken.None))
        {
            results.Add(entry);
        }

        return results;
    }
}
