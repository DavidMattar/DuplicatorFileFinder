using System.IO;
using DuplicatorFinder.App.Converters;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// Dados exibidos na janela de preview (<see cref="Views.PreviewWindow"/>): a lista de
/// imagens de um grupo de duplicados, para exibição lado a lado. É um POCO simples porque a
/// lista é montada uma única vez, na abertura da janela, e nunca muda depois disso.
/// </summary>
public sealed class PreviewViewModel
{
    /// <summary>Imagens do grupo a exibir, uma ao lado da outra.</summary>
    public IReadOnlyList<PreviewItemViewModel> Items { get; }

    public PreviewViewModel(IReadOnlyList<FileCandidateViewModel> files)
    {
        Items = files
            .Select(file => new PreviewItemViewModel(file.FullPath, file.SizeBytes))
            .ToList();
    }
}

/// <summary>Uma única imagem exibida na janela de preview, já com o tamanho formatado para exibição.</summary>
public sealed class PreviewItemViewModel
{
    /// <summary>Caminho completo do arquivo, usado pelo <see cref="Converters.FilePathToThumbnailConverter"/> para decodificar a imagem.</summary>
    public string FullPath { get; }

    /// <summary>Nome do arquivo (sem o caminho da pasta), exibido como legenda.</summary>
    public string FileName { get; }

    /// <summary>Tamanho do arquivo já formatado (ex: "1,25 MB"), exibido como legenda.</summary>
    public string SizeDisplay { get; }

    public PreviewItemViewModel(string fullPath, long sizeBytes)
    {
        FullPath = fullPath;
        FileName = Path.GetFileName(fullPath);
        SizeDisplay = BytesToHumanReadableConverter.Format(sizeBytes);
    }
}
