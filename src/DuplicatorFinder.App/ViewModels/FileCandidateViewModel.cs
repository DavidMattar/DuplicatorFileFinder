using CommunityToolkit.Mvvm.ComponentModel;
using DuplicatorFinder.Core.Models;
using DuplicatorFinder.Core.Support;

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

    /// <summary>
    /// Verdadeiro se este é o arquivo escolhido para ser mantido no grupo — inicialmente pelo
    /// smart-select do Core, depois possivelmente por uma mudança de seleção do usuário
    /// (ver <see cref="DuplicateGroupViewModel.NormalizeKeptFile"/>).
    /// </summary>
    public bool IsKept => Model.IsKept;

    /// <summary>Explicação legível da decisão do smart-select (ou da seleção do usuário) para este arquivo.</summary>
    public string? Reason => Model.Reason;

    /// <summary>Largura em pixels, quando conhecida (imagens e vídeos); null para os outros tipos de arquivo.</summary>
    public int? Width => Model.Width;

    /// <summary>Altura em pixels — ver <see cref="Width"/>.</summary>
    public int? Height => Model.Height;

    /// <summary>
    /// Quantidade de pixels da imagem/vídeo (largura × altura), ou 0 quando a resolução é
    /// desconhecida. É a chave de ordenação usada por
    /// <see cref="DuplicateGroupViewModel.ChooseHighestResolutionFile"/> — existe aqui, e não
    /// inline no chamador, para a multiplicação já sair como <see cref="long"/> e não estourar
    /// em imagens muito grandes.
    /// </summary>
    public long PixelCount => (long)(Width ?? 0) * (Height ?? 0);

    /// <summary>
    /// Verdadeiro se este arquivo é de um formato de imagem suportado — usado para decidir se
    /// ele participa do preview lado a lado (<see cref="ResultsViewModel"/> via <see cref="Services.IDialogService.ShowPreview"/>).
    /// </summary>
    public bool IsImage => FileTypeClassifier.IsImageExtension(Model.File.Extension);

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

    /// <summary>
    /// Redefine se este arquivo é o "mantido" do seu grupo, propagando a decisão para o modelo
    /// de domínio e notificando a UI (a coluna de motivo muda junto). Não é chamado pela View:
    /// existe para o <see cref="DuplicateGroupViewModel"/> poder transferir o papel de "mantido"
    /// para outro arquivo do grupo quando a seleção do usuário torna o mantido atual inválido
    /// (ver <see cref="DuplicateGroupViewModel.NormalizeKeptFile"/>).
    /// </summary>
    public void SetKept(bool isKept, string? reason)
    {
        Model.IsKept = isKept;
        Model.Reason = reason;

        OnPropertyChanged(nameof(IsKept));
        OnPropertyChanged(nameof(Reason));
    }
}
