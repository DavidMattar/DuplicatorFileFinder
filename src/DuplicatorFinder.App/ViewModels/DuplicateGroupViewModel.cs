using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuplicatorFinder.App.Services;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// ViewModel de um grupo de duplicados, exibido como um bloco expansível na tela de
/// resultados, contendo a lista de <see cref="FileCandidateViewModel"/> daquele grupo e as
/// ações que operam sobre o grupo inteiro (preview lado a lado, abrir locais no Explorer).
/// É também o guardião da invariante do grupo — exatamente um arquivo mantido, nunca marcado
/// para exclusão (ver <see cref="NormalizeKeptFile"/>).
/// </summary>
public sealed partial class DuplicateGroupViewModel : ObservableObject
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

    /// <summary>
    /// Restaura a invariante do grupo depois de uma mudança em massa de seleção ("Selecionar
    /// recomendados", "Inverter seleção") ou da remoção de arquivos já excluídos/movidos:
    /// exatamente um arquivo é o mantido, e esse arquivo nunca está marcado para exclusão.
    /// <para>
    /// Sem isso, "Inverter seleção" deixava o arquivo mantido marcado para exclusão e ainda
    /// mantido ao mesmo tempo — combinação que o próprio <see cref="DuplicateFile"/> proíbe.
    /// Como as ações seguintes filtram por "marcado <b>e não</b> mantido", o único arquivo
    /// marcado de cada grupo era descartado por esse filtro e "Mover selecionados" respondia
    /// "Nenhum arquivo selecionado para mover" mesmo com itens visivelmente marcados na tela.
    /// </para>
    /// <para>
    /// O novo mantido é escolhido entre os arquivos que <b>não</b> estão marcados (preferindo
    /// o mantido atual, para não trocar o original sem necessidade). Se o usuário marcou todos
    /// os arquivos do grupo, um deles é desmarcado para sobreviver — um grupo de duplicados
    /// sem nenhuma cópia restante não faria sentido, e apagar todas seria justamente o que o
    /// app existe para evitar.
    /// </para>
    /// </summary>
    public void NormalizeKeptFile()
    {
        if (Files.Count == 0)
        {
            return;
        }

        var currentKept = Files.FirstOrDefault(file => file.IsKept);

        var newKept = currentKept is not null && !currentKept.IsMarkedForDeletion
            ? currentKept
            : Files.FirstOrDefault(file => !file.IsMarkedForDeletion) ?? ChooseHighestResolutionFile();

        PromoteToKeptFile(newKept, "Mantido pela sua seleção");
    }

    /// <summary>
    /// Torna <paramref name="file"/> o arquivo mantido do grupo: desmarca-o (o mantido nunca
    /// está marcado para exclusão) e tira o papel de quem o tinha antes. Chamado por
    /// <see cref="NormalizeKeptFile"/> e, direto, quando uma ação já executada define qual
    /// arquivo sobreviveu no grupo — por exemplo o modo
    /// <see cref="DuplicateMoveMode.KeepHighestResolutionInPlace"/>, que deixa no lugar o
    /// arquivo de maior resolução e por isso precisa que ele apareça como o original do grupo
    /// depois da movimentação, e não mais como uma cópia marcada.
    /// Não faz nada se <paramref name="file"/> já é o mantido, para não reescrever o motivo
    /// original (ex: "Maior resolução do grupo") sem necessidade.
    /// </summary>
    public void PromoteToKeptFile(FileCandidateViewModel file, string reason)
    {
        // Um grupo inteiramente marcado só volta a ser válido desmarcando o sobrevivente.
        file.IsMarkedForDeletion = false;

        foreach (var other in Files.Where(other => other.IsKept && !ReferenceEquals(other, file)).ToList())
        {
            other.SetKept(false, "Cópia, pela sua seleção");
        }

        if (!file.IsKept)
        {
            file.SetKept(true, reason);
        }

        OnPropertyChanged(nameof(WastedBytes));
    }

    /// <summary>
    /// Escolhe o arquivo de maior resolução (largura × altura) do grupo — o critério do modo
    /// <see cref="DuplicateMoveMode.KeepHighestResolutionInPlace"/>. Empates (inclusive grupos
    /// de duplicados exatos, onde a resolução é desconhecida e todos valem 0) são desempatados
    /// pelo maior tamanho em bytes e depois pelo caminho completo, para o resultado nunca
    /// depender da ordem "por acaso" em que os arquivos foram enumerados — mesmo raciocínio do
    /// desempate de <see cref="Core.SmartSelect.DefaultSmartSelectStrategy"/>.
    /// </summary>
    public FileCandidateViewModel ChooseHighestResolutionFile() => Files
        .OrderByDescending(file => file.PixelCount)
        .ThenByDescending(file => file.SizeBytes)
        .ThenBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase)
        .First();

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
