using System.IO.Abstractions.TestingHelpers;
using DuplicatorFinder.Infrastructure.Move;
using FluentAssertions;
using Xunit;

namespace DuplicatorFinder.Infrastructure.Tests.Move;

/// <summary>
/// Testa <see cref="DuplicateMoveService"/> usando um <see cref="MockFileSystem"/> em
/// memória — valida a numeração da pasta de lote, a estrutura "mantido + subpasta de
/// cópias" dentro dela, a resolução de colisões de nome e a tolerância a falhas parciais,
/// sem tocar em disco real.
/// </summary>
public class DuplicateMoveServiceTests
{
    [Fact]
    public void CreateBatchFolder_CreatesCopias1_WhenDestinationIsEmpty()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(@"C:\destino");
        var service = new DuplicateMoveService(fileSystem);

        var batchFolder = service.CreateBatchFolder(@"C:\destino");

        batchFolder.Should().Be(@"C:\destino\copias(1)");
        fileSystem.Directory.Exists(batchFolder).Should().BeTrue();
    }

    [Fact]
    public void CreateBatchFolder_SkipsToNextFreeNumber_WhenEarlierOnesAlreadyExist()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(@"C:\destino\copias(1)");
        fileSystem.AddDirectory(@"C:\destino\copias(2)");
        var service = new DuplicateMoveService(fileSystem);

        var batchFolder = service.CreateBatchFolder(@"C:\destino");

        batchFolder.Should().Be(@"C:\destino\copias(3)");
    }

    [Fact]
    public async Task MoveGroupAsync_MovesKeptFileIntoBatchFolder_AndCopiesIntoSiblingSubfolder()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(@"C:\photos\original.jpg", new MockFileData("original"));
        fileSystem.AddFile(@"C:\photos\copy1.jpg", new MockFileData("copy1"));
        fileSystem.AddFile(@"C:\other\copy2.jpg", new MockFileData("copy2"));
        fileSystem.AddDirectory(@"C:\destino\copias(1)");

        var service = new DuplicateMoveService(fileSystem);

        var result = await service.MoveGroupAsync(
            @"C:\destino\copias(1)",
            @"C:\photos\original.jpg",
            [@"C:\photos\copy1.jpg", @"C:\other\copy2.jpg"],
            CancellationToken.None);

        result.Failures.Should().BeEmpty();
        result.SucceededPaths.Should().BeEquivalentTo(
            [@"C:\photos\original.jpg", @"C:\photos\copy1.jpg", @"C:\other\copy2.jpg"]);

        fileSystem.File.Exists(@"C:\photos\original.jpg").Should().BeFalse("o arquivo mantido também deve ser movido");
        fileSystem.File.Exists(@"C:\destino\copias(1)\original.jpg").Should().BeTrue();
        fileSystem.File.Exists(@"C:\destino\copias(1)\original copies moved\copy1.jpg").Should().BeTrue();
        fileSystem.File.Exists(@"C:\destino\copias(1)\original copies moved\copy2.jpg").Should().BeTrue();
    }

    [Fact]
    public async Task MoveGroupAsync_ResolvesNameCollisions_WithNumberedSuffix()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(@"C:\photos\original.jpg", new MockFileData("original"));
        fileSystem.AddFile(@"C:\albumA\photo.jpg", new MockFileData("a"));
        fileSystem.AddFile(@"C:\albumB\photo.jpg", new MockFileData("b"));
        fileSystem.AddDirectory(@"C:\destino\copias(1)");

        var service = new DuplicateMoveService(fileSystem);

        var result = await service.MoveGroupAsync(
            @"C:\destino\copias(1)",
            @"C:\photos\original.jpg",
            [@"C:\albumA\photo.jpg", @"C:\albumB\photo.jpg"],
            CancellationToken.None);

        result.Failures.Should().BeEmpty();
        fileSystem.File.Exists(@"C:\destino\copias(1)\original copies moved\photo.jpg").Should().BeTrue();
        fileSystem.File.Exists(@"C:\destino\copias(1)\original copies moved\photo (1).jpg").Should().BeTrue();
    }

    [Fact]
    public async Task MoveGroupAsync_ReportsFailure_WhenSourceFileDoesNotExist_WithoutAbortingOthers()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(@"C:\photos\original.jpg", new MockFileData("original"));
        fileSystem.AddFile(@"C:\photos\copy1.jpg", new MockFileData("copy1"));
        fileSystem.AddDirectory(@"C:\destino\copias(1)");

        var service = new DuplicateMoveService(fileSystem);

        var result = await service.MoveGroupAsync(
            @"C:\destino\copias(1)",
            @"C:\photos\original.jpg",
            [@"C:\photos\missing.jpg", @"C:\photos\copy1.jpg"],
            CancellationToken.None);

        result.SucceededPaths.Should().BeEquivalentTo([@"C:\photos\original.jpg", @"C:\photos\copy1.jpg"]);
        result.Failures.Should().ContainSingle(f => f.Path == @"C:\photos\missing.jpg");
    }
}
