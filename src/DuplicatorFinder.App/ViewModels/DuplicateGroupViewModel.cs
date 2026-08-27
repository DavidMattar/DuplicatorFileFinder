using System.Collections.ObjectModel;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// ViewModel de um grupo de duplicados, exibido como um bloco expansível na tela de
/// resultados, contendo a lista de <see cref="FileCandidateViewModel"/> daquele grupo.
/// </summary>
public sealed class DuplicateGroupViewModel
{
    /// <summary>Modelo de domínio (Core) que este grupo representa.</summary>
    public DuplicateGroup Model { get; }

    /// <summary>Arquivos do grupo, já envolvidos em ViewModels prontos para binding.</summary>
    public ObservableCollection<FileCandidateViewModel> Files { get; }

    public DuplicateGroupViewModel(DuplicateGroup model)
    {
        Model = model;
        Files = new ObservableCollection<FileCandidateViewModel>(
            model.Files.Select(file => new FileCandidateViewModel(file)));
    }

    public DuplicateKind Kind => Model.Kind;

    /// <summary>Espaço em disco recuperável neste grupo (calculado no Core a partir de quem não está marcado como mantido).</summary>
    public long WastedBytes => Model.WastedBytes;
}
