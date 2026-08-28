using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using DuplicatorFinder.App.Services;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// ViewModel de um grupo de duplicados, exibido como um bloco expansível na tela de
/// resultados, contendo a lista de <see cref="FileCandidateViewModel"/> daquele grupo e as
/// ações que operam sobre o grupo inteiro (preview lado a lado, abrir locais no Explorer).
/// </summary>
public sealed partial class DuplicateGroupViewModel
{
    private readonly IDialogService _dialogService;

    /// <summary>Modelo de domínio (Core) que este grupo representa.</summary>
    public DuplicateGroup Model { get; }

    /// <summary>Arquivos do grupo, já envolvidos em ViewModels prontos para binding.</summary>
    public ObservableCollection<FileCandidateViewModel> Files { get; }

    public DuplicateGroupViewModel(DuplicateGroup model, IDialogService dialogService)
    {
        Model = model;
        _dialogService = dialogService;
        Files = new ObservableCollection<FileCandidateViewModel>(
            model.Files.Select(file => new FileCandidateViewModel(file)));
    }

    public DuplicateKind Kind => Model.Kind;

    /// <summary>Espaço em disco recuperável neste grupo (calculado no Core a partir de quem não está marcado como mantido).</summary>
    public long WastedBytes => Model.WastedBytes;

    /// <summary>
    /// Verdadeiro se ao menos um arquivo do grupo é uma imagem — usado para exibir o botão de
    /// preview só quando ele teria algo relevante para mostrar (ex: grupos de vídeos ou de
    /// duplicados exatos de arquivos não-imagem não ganham o botão).
    /// </summary>
    public bool HasPreviewableImages => Files.Any(file => file.IsImage);

    /// <summary>Abre a janela de preview lado a lado com as imagens deste grupo.</summary>
    [RelayCommand]
    private void Preview()
    {
        _dialogService.ShowPreview(Files.Where(file => file.IsImage).ToList());
    }

    /// <summary>Abre a janela "Abrir locais", com uma aba por cópia deste grupo.</summary>
    [RelayCommand]
    private void OpenLocations()
    {
        _dialogService.OpenLocations(Files.ToList());
    }
}
