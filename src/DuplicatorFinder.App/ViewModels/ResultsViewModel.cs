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
/// ou, como alternativa, move os grupos inteiros (arquivo mantido + cópias) para uma pasta
/// escolhida pelo usuário em vez de excluí-los (via <see cref="IDuplicateMoveService"/>).
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
    /// Move os grupos com cópias selecionadas para uma pasta escolhida pelo usuário, em vez de
    /// excluí-las: pergunta a pasta de destino (pré-preenchida com a última escolha), cria uma
    /// única subpasta numerada "copias(x)" para toda a operação, e move cada grupo para dentro
    /// dela — o arquivo mantido fica direto na pasta numerada, com uma subpasta ao lado
    /// contendo as cópias daquele grupo. Diferente da exclusão, aqui o arquivo mantido também é
    /// movido (nunca fica "onde estava"), porque o propósito do botão é consolidar o grupo
    /// inteiro no destino escolhido.
    /// </summary>
    [RelayCommand]
    private async Task MoveSelectedAsync()
    {
        var groupsWithCopiesToMove = Groups
            .Select(group => (Group: group, Copies: group.Files.Where(f => f.IsMarkedForDeletion && !f.IsKept).ToList()))
            .Where(x => x.Copies.Count > 0)
            .ToList();

        if (groupsWithCopiesToMove.Count == 0)
        {
            _dialogService.ShowError("Nenhum arquivo selecionado para mover.");
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

        // +1 por grupo: o arquivo mantido também é movido, junto com suas cópias.
        var totalCount = groupsWithCopiesToMove.Sum(x => x.Copies.Count + 1);
        var totalBytes = groupsWithCopiesToMove.Sum(x =>
            x.Copies.Sum(f => f.SizeBytes) + (x.Group.Files.FirstOrDefault(f => f.IsKept)?.SizeBytes ?? 0));

        if (!_dialogService.ConfirmMove(totalCount, totalBytes, destinationRoot))
        {
            return;
        }

        var batchFolder = _duplicateMoveService.CreateBatchFolder(destinationRoot);

        var succeededPaths = new List<string>();
        var failureCount = 0;

        foreach (var (group, copies) in groupsWithCopiesToMove)
        {
            var keptFile = group.Files.FirstOrDefault(f => f.IsKept);
            if (keptFile is null)
            {
                // Nunca deveria acontecer — o smart-select do Core sempre marca exatamente um
                // arquivo como mantido em todo grupo — mas sem ele não há como nomear/localizar
                // a subpasta de cópias deste grupo, então pulá-lo é a única opção segura.
                continue;
            }

            var result = await _duplicateMoveService.MoveGroupAsync(
                batchFolder,
                keptFile.FullPath,
                copies.Select(f => f.FullPath),
                CancellationToken.None);

            succeededPaths.AddRange(result.SucceededPaths);
            failureCount += result.Failures.Count;
        }

        RemoveFilesFromGroups(succeededPaths);

        var batchFolderName = Path.GetFileName(batchFolder);
        StatusMessage = failureCount == 0
            ? $"{succeededPaths.Count} arquivo(s) movidos para \"{batchFolderName}\"."
            : $"{succeededPaths.Count} movido(s) para \"{batchFolderName}\", {failureCount} falharam (veja os arquivos ainda marcados).";
    }

    [RelayCommand]
    private void SelectRecommended()
    {
        foreach (var file in Groups.SelectMany(g => g.Files))
        {
            file.IsMarkedForDeletion = !file.IsKept;
        }
    }

    [RelayCommand]
    private void InvertSelection()
    {
        foreach (var file in Groups.SelectMany(g => g.Files))
        {
            file.IsMarkedForDeletion = !file.IsMarkedForDeletion;
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
                }
            }

            // Um grupo com 1 ou 0 arquivos restantes não é mais um "duplicado" de nada.
            if (group.Files.Count <= 1)
            {
                Groups.Remove(group);
            }
        }
    }
}
