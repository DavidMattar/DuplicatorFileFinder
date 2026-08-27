using DuplicatorFinder.Core.Models;
using DuplicatorFinder.Core.SmartSelect;
using FluentAssertions;
using Xunit;

namespace DuplicatorFinder.Core.Tests.SmartSelect;

/// <summary>
/// Testa <see cref="DefaultSmartSelectStrategy"/> isoladamente, sem tocar em disco ou hash —
/// a estratégia só opera sobre os metadados já carregados em <see cref="DuplicateFile"/>.
/// </summary>
public class SmartSelectStrategyTests
{
    [Fact]
    public void Apply_KeepsOldestFile_WhenStrategyIsOldestFile()
    {
        var oldFile = MakeFile(@"C:\a\old.txt", DateTime.UtcNow.AddDays(-10));
        var newFile = MakeFile(@"C:\a\new.txt", DateTime.UtcNow);
        var group = new DuplicateGroup { Kind = DuplicateKind.ExactFile, Files = [oldFile, newFile] };

        new DefaultSmartSelectStrategy().Apply(group, new SmartSelectOptions { Primary = KeepStrategy.OldestFile });

        oldFile.IsKept.Should().BeTrue();
        oldFile.MarkedForDeletion.Should().BeFalse();
        newFile.IsKept.Should().BeFalse();
        newFile.MarkedForDeletion.Should().BeTrue();
    }

    [Fact]
    public void Apply_PrefersConfiguredFolder_OverPrimaryStrategy()
    {
        var oldFileOutsidePreferredFolder = MakeFile(@"C:\backup\old.txt", DateTime.UtcNow.AddDays(-10));
        var newFileInPreferredFolder = MakeFile(@"C:\keep\new.txt", DateTime.UtcNow);
        var group = new DuplicateGroup
        {
            Kind = DuplicateKind.ExactFile,
            Files = [oldFileOutsidePreferredFolder, newFileInPreferredFolder],
        };

        new DefaultSmartSelectStrategy().Apply(group, new SmartSelectOptions
        {
            Primary = KeepStrategy.OldestFile,
            PreferredFolderPath = @"C:\keep",
        });

        newFileInPreferredFolder.IsKept.Should().BeTrue();
        oldFileOutsidePreferredFolder.IsKept.Should().BeFalse();
    }

    [Fact]
    public void Apply_MarksExactlyOneFileAsKept_RegardlessOfGroupSize()
    {
        var files = Enumerable.Range(0, 5)
            .Select(i => MakeFile($@"C:\a\file{i}.txt", DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        var group = new DuplicateGroup { Kind = DuplicateKind.ExactFile, Files = files };

        new DefaultSmartSelectStrategy().Apply(group, new SmartSelectOptions());

        files.Count(f => f.IsKept).Should().Be(1);
        files.Count(f => f.MarkedForDeletion).Should().Be(4);
    }

    [Fact]
    public void Apply_KeepsHighestResolutionFile_WhenStrategyIsHighestResolution()
    {
        var lowRes = MakeFile(@"C:\a\low.jpg", DateTime.UtcNow, width: 640, height: 480);
        var highRes = MakeFile(@"C:\a\high.jpg", DateTime.UtcNow, width: 3840, height: 2160);
        var group = new DuplicateGroup { Kind = DuplicateKind.SimilarImage, Files = [lowRes, highRes] };

        new DefaultSmartSelectStrategy().Apply(group, new SmartSelectOptions { Primary = KeepStrategy.HighestResolution });

        highRes.IsKept.Should().BeTrue();
        lowRes.IsKept.Should().BeFalse();
    }

    private static DuplicateFile MakeFile(string path, DateTime createdUtc, int? width = null, int? height = null) => new()
    {
        File = new FileEntry(path, 100, createdUtc, createdUtc, ".txt"),
        Width = width,
        Height = height,
    };
}
