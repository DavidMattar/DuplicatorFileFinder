using System.IO.Abstractions.TestingHelpers;
using DuplicatorFinder.Core.Models;
using DuplicatorFinder.Infrastructure.Settings;
using FluentAssertions;
using Xunit;

namespace DuplicatorFinder.Infrastructure.Tests.Settings;

/// <summary>
/// Testa <see cref="JsonSettingsService"/> usando um <see cref="MockFileSystem"/> em
/// memória — valida o comportamento de persistência sem gravar nada no disco real do usuário.
/// </summary>
public class JsonSettingsServiceTests
{
    [Fact]
    public void Load_ReturnsDefaults_WhenNoFileExistsYet()
    {
        var fileSystem = new MockFileSystem();
        var service = new JsonSettingsService(fileSystem, @"C:\settings\settings.json");

        var settings = service.Load();

        settings.FavoriteFolders.Should().BeEmpty();
        settings.PreferredKeepStrategy.Should().Be(KeepStrategy.OldestFile);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllValues()
    {
        var fileSystem = new MockFileSystem();
        var service = new JsonSettingsService(fileSystem, @"C:\settings\settings.json");

        var original = new AppSettings
        {
            FavoriteFolders = [@"C:\Photos", @"D:\Backup"],
            LastImageSimilarityThreshold = 0.75,
            LastVideoSimilarityThreshold = 0.65,
            PreferredKeepStrategy = KeepStrategy.HighestResolution,
        };

        service.Save(original);
        var loaded = service.Load();

        loaded.FavoriteFolders.Should().BeEquivalentTo(original.FavoriteFolders);
        loaded.LastImageSimilarityThreshold.Should().Be(original.LastImageSimilarityThreshold);
        loaded.LastVideoSimilarityThreshold.Should().Be(original.LastVideoSimilarityThreshold);
        loaded.PreferredKeepStrategy.Should().Be(original.PreferredKeepStrategy);
    }

    [Fact]
    public void Save_CreatesParentDirectory_WhenItDoesNotExistYet()
    {
        var fileSystem = new MockFileSystem();
        var service = new JsonSettingsService(fileSystem, @"C:\brand-new-folder\settings.json");

        service.Save(new AppSettings());

        fileSystem.Directory.Exists(@"C:\brand-new-folder").Should().BeTrue();
        fileSystem.File.Exists(@"C:\brand-new-folder\settings.json").Should().BeTrue();
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileContentIsCorrupted()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(@"C:\settings\settings.json", new MockFileData("{ isso não é json válido"));
        var service = new JsonSettingsService(fileSystem, @"C:\settings\settings.json");

        var settings = service.Load();

        settings.PreferredKeepStrategy.Should().Be(KeepStrategy.OldestFile);
    }
}
