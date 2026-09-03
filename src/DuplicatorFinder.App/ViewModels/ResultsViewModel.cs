using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuplicatorFinder.App.Services;
using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// ViewModel da tela de resultados: mostra os grupos de duplicados encontrados, permite ao
/// usuário ajustar manualmente quais arquivos serão excluídos e dispara a exclusão real
/// (sempre para a Lixeira do Windows, nunca permanente, via <see cref="IRecycleBinService"/>)
/// ou, como alternativa, move os arquivos selecionados para uma pasta escolhida por ele em vez
/// de excluí-los (via <see cref="IDuplicateMoveService"/>), em um dos dois modos de
/// <see cref="DuplicateMoveMode"/>.
/// </summary>
public sealed partial class ResultsViewModel : ObservableObject
{
    private readonly IRecycleBinService _recycleBinService;
    private readonly IDuplicateMoveService _duplicateMoveService;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;

    /// <summary>Grupos de duplicados encontrados, cada um já com o smart-select aplicado pelo Core.</summary>
    public ObservableCollection<DuplicateGroupViewModel> Groups { get; }

    /// <summary>Total de arquivos considerados durante o escaneamento (não só os duplicados).</summary>
    public long TotalFilesScanned { get; }

    /// <summary>Espaço em disco recuperável se todas as cópias sugeridas forem excluídas.</summary>
    public long TotalWastedBytes { get; }

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>Disparado quando o usuário pede para voltar à tela inicial para um novo escaneamento.</summary>
    public event EventHandler? NewScanRequested;

    public ResultsViewModel(
        ScanResult result,
        IRecycleBinService recycleBinService,
        IDuplicateMoveService duplicateMoveService,
        ISettingsService settingsService,
        IDialogService dialogService)
    {
        _recycleBinService = recycleBinService;
        _duplicateMoveService = duplicateMoveService;
        _settingsService = settingsService;
        _dialogService = dialogService;

        Groups = new ObservableCollection<DuplicateGroupViewModel>(result.Groups.Select(g => new DuplicateGroupViewModel(g, dialogService)));
        TotalFilesScanned = result.TotalFilesScanned;
        TotalWastedBytes = result.TotalWastedBytes;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var toDelete = Groups.SelectMany(g => g.Files).Where(f => f.IsMarkedForDeletion).ToList();

        if (toDelete.Count == 0)
        {
            _dialogService.ShowError("Nenhum arquivo selecionado para excluir.");
            return;
        }

        var totalBytes = toDelete.Sum(f => f.SizeBytes);
        if (!_dialogService.ConfirmDeletion(toDelete.Count, totalBytes))
        {
            return;
        }

        var result = await _recycleBinService.SendToRecycleBinAsync(toDelete.Select(f => f.FullPath));

        RemoveFilesFromGroups(result.SucceededPaths);

        StatusMessage = result.Failures.Count == 0
            ? $"{result.SucceededPaths.Count} arquivo(s) enviados para a Lixeira."
            : $"{result.SucceededPaths.Count} excluído(s), {result.Failures.Count} falharam (veja os arquivos ainda marcados).";
    }

    /// <summary>
    /// Move os arquivos selecionados para uma pasta escolhida pelo usuário, em vez de
    /// excluí-los. O fluxo é: (1) perguntar o modo de movimentação
    /// (<see cref="DuplicateMoveMode"/>); (2) perguntar a pasta de destino, pré-preenchida com
    /// a última escolha; (3) confirmar quantidade/tamanho; (4) criar uma única subpasta
    /// numerada "copias(x)" para toda a operação e mover cada grupo para dentro dela, com as
    /// cópias de cada grupo em uma subpasta própria nomeada a partir do arquivo que sobrevive
    /// naquele grupo. A diferença entre os dois modos está apenas em qual arquivo sobrevive e
    /// se ele sai ou não do lugar — ver <see cref="PlanMove"/>.
    /// </summary>
    [RelayCommand]
    private async Task MoveSelectedAsync()
    {
        if (Groups.All(group => group.Files.All(file => !file.IsMarkedForDeletion)))
        {
            _dialogService.ShowError("Nenhum arquivo selecionado para mover.");
            return;
        }

        var mode = _dialogService.PickMoveMode();
        if (mode is null)
        {
            // Usuário fechou/cancelou a escolha do modo — nada é movido.
            return;
        }

        var plans = PlanMove(mode.Value);
        if (plans.Count == 0)
        {
            _dialogService.ShowError(
                "Nada a mover com esse modo: em cada grupo selecionado, o único arquivo marcado é justamente o que sobreviveria.");
            return;
        }

        var settings = _settingsService.Load();
        var destinationRoot = _dialogService.PickFolder(
            "Onde você quer criar a pasta de cópias movidas?",
            settings.LastCopiesMoveDestinationFolder);

        if (destinationRoot is null)
        {
            // Usuário cancelou a escolha da pasta de destino — nada é movido.
            return;
        }

        _settingsService.Save(new AppSettings
        {
            FavoriteFolders = settings.FavoriteFolders,
            LastImageSimilarityThreshold = settings.LastImageSimilarityThreshold,
            LastVideoSimilarityThreshold = settings.LastVideoSimilarityThreshold,
            PreferredKeepStrategy = settings.PreferredKeepStrategy,
            LastCopiesMoveDestinationFolder = destinationRoot,
        });

        var filesToMove = plans.SelectMany(plan => plan.FilesToMove).ToList();

        if (!_dialogService.ConfirmMove(filesToMove.Count, filesToMove.Sum(f => f.SizeBytes), destinationRoot, mode.Value))
        {
            return;
        }

        var batchFolder = _duplicateMoveService.CreateBatchFolder(destinationRoot);

        var succeededPaths = new List<string>();
        var failureCount = 0;

        foreach (var plan in plans)
        {
            var result = await _duplicateMoveService.MoveGroupAsync(
                batchFolder,
                plan.SurvivingFile.FullPath,
                plan.FilesToMove.Where(file => !ReferenceEquals(file, plan.SurvivingFile)).Select(file => file.FullPath),
                plan.FilesToMove.Contains(plan.SurvivingFile),
                CancellationToken.None);

            succeededPaths.AddRange(result.SucceededPaths);
            failureCount += result.Failures.Count;
        }

        RemoveFilesFromGroups(succeededPaths);

        // No modo por resolução o sobrevivente não foi movido, então continua na lista — e
        // continuaria marcado para exclusão, com o papel de "mantido" ainda em outro arquivo.
        // Promovê-lo aqui deixa a tela coerente com o que acabou de acontecer no disco (e
        // evita que um "Excluir selecionados" logo depois apague justamente a melhor versão).
        if (mode.Value == DuplicateMoveMode.KeepHighestResolutionInPlace)
        {
            foreach (var plan in plans.Where(plan => Groups.Contains(plan.Group) && plan.Group.Files.Contains(plan.SurvivingFile)))
            {
                plan.Group.PromoteToKeptFile(plan.SurvivingFile, "Maior resolução do grupo — mantido no lugar");
            }
        }

        var batchFolderName = Path.GetFileName(batchFolder);
        StatusMessage = failureCount == 0
            ? $"{succeededPaths.Count} arquivo(s) movidos para \"{batchFolderName}\"."
            : $"{succeededPaths.Count} movido(s) para \"{batchFolderName}\", {failureCount} falharam (veja os arquivos ainda marcados).";
    }

    /// <summary>
    /// Decide, para cada grupo com arquivos marcados, qual arquivo sobrevive e quais são
    /// movidos — a única diferença real entre os dois <see cref="DuplicateMoveMode"/>:
    /// <list type="bullet">
    /// <item><see cref="DuplicateMoveMode.MoveEntireGroup"/>: sobrevive o arquivo mantido do
    /// grupo (a escolha do smart-select, ajustada pelo usuário), e ele <b>também</b> é movido —
    /// vai para a raiz da pasta numerada. Nada do grupo continua na pasta de origem.</item>
    /// <item><see cref="DuplicateMoveMode.KeepHighestResolutionInPlace"/>: sobrevive o arquivo
    /// de maior resolução do grupo e ele <b>não</b> é movido, fica exatamente onde está;
    /// <b>todas</b> as outras cópias do grupo vão para a subpasta daquele grupo.</item>
    /// </list>
    /// A diferença de escopo entre os dois modos é intencional. No modo "grupo inteiro" a
    /// marcação de cada linha decide o que sai do lugar. No modo por resolução a própria regra
    /// é "fica um de cada, o de maior resolução" — parar em quem está marcado deixaria para
    /// trás justamente o arquivo que o smart-select desmarcou, contrariando o propósito do
    /// modo; por isso ele move todas as outras cópias, e o diálogo de escolha diz isso com
    /// todas as letras antes de o usuário confirmar. Em ambos os modos a marcação continua
    /// decidindo <b>quais grupos</b> participam: um grupo sem nenhum arquivo marcado é ignorado.
    /// </summary>
    private List<MoveGroupPlan> PlanMove(DuplicateMoveMode mode)
    {
        var plans = new List<MoveGroupPlan>();

        foreach (var group in Groups)
        {
            if (!group.Files.Any(file => file.IsMarkedForDeletion))
            {
                continue;
            }

            var survivingFile = mode == DuplicateMoveMode.KeepHighestResolutionInPlace
                ? group.ChooseHighestResolutionFile()
                : group.Files.FirstOrDefault(file => file.IsKept);

            if (survivingFile is null)
            {
                // Grupo sem nenhum mantido definido: sem ele não há como nomear a subpasta de
                // cópias, então normalizar (que sempre elege um) é mais útil que pular o grupo.
                group.NormalizeKeptFile();
                survivingFile = group.Files.First(file => file.IsKept);
            }

            var filesToMove = group.Files
                .Where(file => !ReferenceEquals(file, survivingFile)
                    && (mode == DuplicateMoveMode.KeepHighestResolutionInPlace || file.IsMarkedForDeletion))
                .ToList();

            if (filesToMove.Count == 0)
            {
                continue;
            }

            if (mode == DuplicateMoveMode.MoveEntireGroup)
            {
                // Neste modo o sobrevivente acompanha suas cópias para o destino.
                filesToMove.Insert(0, survivingFile);
            }

            plans.Add(new MoveGroupPlan(group, survivingFile, filesToMove));
        }

        return plans;
    }

    /// <summary>
    /// O que fazer com um grupo em uma operação de movimentação: qual arquivo é o sobrevivente
    /// (usado para nomear a subpasta de cópias daquele grupo) e quais arquivos saem do lugar.
    /// O sobrevivente aparece em <paramref name="FilesToMove"/> apenas no modo
    /// <see cref="DuplicateMoveMode.MoveEntireGroup"/>.
    /// </summary>
    /// <param name="Group">Grupo de origem, guardado para ajustar seu estado na tela depois da movimentação.</param>
    /// <param name="SurvivingFile">Arquivo que sobrevive no grupo, escolhido conforme o modo.</param>
    /// <param name="FilesToMove">Arquivos que efetivamente saem do lugar nesta operação.</param>
    private sealed record MoveGroupPlan(
        DuplicateGroupViewModel Group,
        FileCandidateViewModel SurvivingFile,
        List<FileCandidateViewModel> FilesToMove);

    /// <summary>
    /// Volta à sugestão automática: em cada grupo, marca para exclusão tudo que não é o
    /// arquivo mantido. Normaliza o grupo antes, porque depois de uma exclusão/movimentação o
    /// grupo pode ter ficado sem nenhum mantido (o mantido pode ter sido justamente o arquivo
    /// removido) — e aí "marcar tudo que não é o mantido" marcaria o grupo inteiro.
    /// </summary>
    [RelayCommand]
    private void SelectRecommended()
    {
        foreach (var group in Groups)
        {
            group.NormalizeKeptFile();

            foreach (var file in group.Files)
            {
                file.IsMarkedForDeletion = !file.IsKept;
            }
        }
    }

    /// <summary>
    /// Inverte a marcação de exclusão de todos os arquivos e, em seguida, restaura a
    /// invariante de cada grupo (<see cref="DuplicateGroupViewModel.NormalizeKeptFile"/>): o
    /// papel de "mantido" passa para um arquivo que ficou desmarcado, e o antigo original
    /// continua marcado como o usuário pediu. Sem essa normalização o arquivo mantido ficava
    /// marcado para exclusão <i>e</i> mantido ao mesmo tempo, o que fazia as ações seguintes
    /// (que consideram só o que está "marcado e não é o mantido") não encontrarem nada para
    /// fazer depois de uma inversão.
    /// </summary>
    [RelayCommand]
    private void InvertSelection()
    {
        foreach (var group in Groups)
        {
            foreach (var file in group.Files)
            {
                file.IsMarkedForDeletion = !file.IsMarkedForDeletion;
            }

            group.NormalizeKeptFile();
        }
    }

    [RelayCommand]
    private void NewScan()
    {
        NewScanRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Remove das listas apenas os arquivos cujo caminho está em <paramref name="succeededPaths"/>
    /// (excluídos ou movidos com sucesso); os que falharam continuam visíveis e marcados, para
    /// o usuário tentar de novo. Compartilhado por <see cref="DeleteSelectedAsync"/> e
    /// <see cref="MoveSelectedAsync"/> porque a limpeza pós-ação é idêntica nos dois casos.
    /// </summary>
    private void RemoveFilesFromGroups(IEnumerable<string> succeededPaths)
    {
        var succeededSet = succeededPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var group in Groups.ToList())
        {
            foreach (var file in group.Files.ToList())
            {
                if (succeededSet.Contains(file.FullPath))
                {
                    group.Files.Remove(file);

                    // Também sai da lista do modelo de domínio, senão WastedBytes (calculado
                    // no Core sobre group.Model.Files) continuaria contando arquivos que já
                    // não estão mais no disco.
                    group.Model.Files.Remove(file.Model);
                }
            }

            // Um grupo com 1 ou 0 arquivos restantes não é mais um "duplicado" de nada.
            if (group.Files.Count <= 1)
            {
                Groups.Remove(group);
                continue;
            }

            // O arquivo removido pode ter sido justamente o mantido do grupo; sem isto o grupo
            // seguiria sem nenhum original definido e as ações seguintes se perderiam nele.
            group.NormalizeKeptFile();
        }
    }
}
