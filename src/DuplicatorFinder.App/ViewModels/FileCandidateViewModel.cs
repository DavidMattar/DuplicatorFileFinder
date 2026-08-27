using CommunityToolkit.Mvvm.ComponentModel;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// ViewModel de um único arquivo dentro de um grupo de duplicados, exibido como uma linha
/// com checkbox na tela de resultados. Envolve o <see cref="DuplicateFile"/> do Core,
/// expondo suas propriedades de forma amigável para binding XAML.
/// </summary>
public sealed partial class FileCandidateViewModel : ObservableObject
{
    /// <summary>Modelo de domínio (Core) que esta linha representa.</summary>
    public DuplicateFile Model { get; }

    public FileCandidateViewModel(DuplicateFile model)
    {
        Model = model;
        _isMarkedForDeletion = model.MarkedForDeletion;
    }

    public string FullPath => Model.File.FullPath;

    public long SizeBytes => Model.File.SizeBytes;

    public DateTime ModifiedUtc => Model.File.ModifiedUtc;

    /// <summary>Verdadeiro se este foi o arquivo escolhido pelo smart-select para ser mantido.</summary>
    public bool IsKept => Model.IsKept;

    /// <summary>Explicação legível da decisão do smart-select para este arquivo.</summary>
    public string? Reason => Model.Reason;

    /// <summary>
    /// Estado do checkbox de exclusão, editável livremente pelo usuário na UI — inclusive
    /// para desmarcar uma cópia sugerida ou marcar o próprio arquivo mantido, se ele preferir.
    /// </summary>
    [ObservableProperty]
    private bool _isMarkedForDeletion;

    /// <summary>
    /// Gerado/chamado automaticamente pelo source generator do CommunityToolkit.Mvvm sempre
    /// que <see cref="IsMarkedForDeletion"/> muda; propaga a decisão de volta para o modelo de
    /// domínio, que é o que a etapa de exclusão (<see cref="ResultsViewModel"/>) realmente lê.
    /// </summary>
    partial void OnIsMarkedForDeletionChanged(bool value)
    {
        Model.MarkedForDeletion = value;
    }
}
