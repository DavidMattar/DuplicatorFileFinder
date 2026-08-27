using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuplicatorFinder.App.Services;
using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// ViewModel da tela de resultados: mostra os grupos de duplicados encontrados, permite ao
/// usuário ajustar manualmente quais arquivos serão excluídos e dispara a exclusão real
/// (sempre para a Lixeira do Windows, nunca permanente) via <see cref="IRecycleBinService"/>.
/// </summary>
public sealed partial class ResultsViewModel : ObservableObject
{
    private readonly IRecycleBinService _recycleBinService;
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

    public ResultsViewModel(ScanResult result, IRecycleBinService recycleBinService, IDialogService dialogService)
    {
        _recycleBinService = recycleBinService;
        _dialogService = dialogService;

        Groups = new ObservableCollection<DuplicateGroupViewModel>(result.Groups.Select(g => new DuplicateGroupViewModel(g)));
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
        var succeededPaths = result.SucceededPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Remove das listas apenas os arquivos que foram de fato excluídos; os que falharam
        // (ex: arquivo em uso) continuam visíveis e marcados, para o usuário tentar de novo.
        foreach (var group in Groups.ToList())
        {
            foreach (var file in group.Files.ToList())
            {
                if (succeededPaths.Contains(file.FullPath))
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

        StatusMessage = result.Failures.Count == 0
            ? $"{result.SucceededPaths.Count} arquivo(s) enviados para a Lixeira."
            : $"{result.SucceededPaths.Count} excluído(s), {result.Failures.Count} falharam (veja os arquivos ainda marcados).";
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
}
