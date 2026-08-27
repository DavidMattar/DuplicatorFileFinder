using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuplicatorFinder.App.Services;
using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// ViewModel da tela inicial: onde o usuário escolhe as pastas, os filtros e os tipos de
/// detecção desejados antes de iniciar um escaneamento. Quando o usuário confirma, este
/// ViewModel monta um <see cref="ScanOptions"/>/<see cref="SmartSelectOptions"/> e dispara o
/// evento <see cref="ScanRequested"/> — quem decide o que fazer com esse pedido (neste app,
/// o <see cref="MainViewModel"/>) é responsabilidade de quem está ouvindo o evento, não desta classe.
/// Também é responsável por carregar/salvar as preferências do usuário via
/// <see cref="ISettingsService"/>, para que pastas, sensibilidade e estratégia de manter
/// sejam lembradas entre uma sessão do app e a próxima.
/// </summary>
public sealed partial class ScanSetupViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;

    public ScanSetupViewModel(IDialogService dialogService, ISettingsService settingsService)
    {
        _dialogService = dialogService;
        _settingsService = settingsService;

        LoadSavedSettings();
    }

    /// <summary>
    /// Preenche a tela com a última configuração salva (se houver). Chamado uma única vez,
    /// na construção do ViewModel — na primeira execução do app, <see cref="ISettingsService.Load"/>
    /// retorna os valores padrão de <see cref="AppSettings"/>, então este método não tem
    /// nenhum efeito visível diferente dos valores já atribuídos nos campos abaixo.
    /// </summary>
    private void LoadSavedSettings()
    {
        var settings = _settingsService.Load();

        foreach (var folder in settings.FavoriteFolders)
        {
            SelectedFolders.Add(folder);
        }

        ImageSimilarityPercent = settings.LastImageSimilarityThreshold * 100.0;
        VideoSimilarityPercent = settings.LastVideoSimilarityThreshold * 100.0;
        KeepStrategy = settings.PreferredKeepStrategy;
    }

    /// <summary>
    /// Salva a configuração atual da tela para ser restaurada na próxima abertura do app.
    /// Chamado a cada início de escaneamento — reflete sempre a última configuração que o
    /// usuário efetivamente usou, não qualquer valor temporário que ele tenha digitado e desfeito.
    /// </summary>
    private void SaveCurrentSettings()
    {
        _settingsService.Save(new AppSettings
        {
            FavoriteFolders = [..SelectedFolders],
            LastImageSimilarityThreshold = ImageSimilarityPercent / 100.0,
            LastVideoSimilarityThreshold = VideoSimilarityPercent / 100.0,
            PreferredKeepStrategy = KeepStrategy,
        });
    }

    /// <summary>Pastas escolhidas pelo usuário para o escaneamento.</summary>
    public ObservableCollection<string> SelectedFolders { get; } = [];

    /// <summary>Estratégias de smart-select disponíveis, para popular o ComboBox da UI.</summary>
    public IReadOnlyList<KeepStrategy> KeepStrategies { get; } = Enum.GetValues<KeepStrategy>();

    [ObservableProperty]
    private string? _selectedFolder;

    [ObservableProperty]
    private bool _includeSubfolders = true;

    [ObservableProperty]
    private double _minFileSizeKb;

    [ObservableProperty]
    private bool _detectExact = true;

    [ObservableProperty]
    private bool _detectSimilarImages = true;

    // Desmarcado por padrão (diferente dos outros dois): a primeira busca por vídeos baixa
    // os executáveis ffmpeg/ffprobe (~70MB) automaticamente — melhor deixar isso como uma
    // escolha explícita do usuário do que disparar rede sem aviso no primeiro uso do app.
    [ObservableProperty]
    private bool _detectSimilarVideos;

    [ObservableProperty]
    private double _imageSimilarityPercent = 90;

    [ObservableProperty]
    private double _videoSimilarityPercent = 90;

    [ObservableProperty]
    private KeepStrategy _keepStrategy = KeepStrategy.OldestFile;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Disparado quando o usuário confirma o início de um escaneamento com os parâmetros já validados.</summary>
    public event EventHandler<ScanRequestedEventArgs>? ScanRequested;

    [RelayCommand]
    private void AddFolder()
    {
        var folder = _dialogService.PickFolder("Selecione uma pasta para escanear");
        if (folder is not null && !SelectedFolders.Contains(folder))
        {
            SelectedFolders.Add(folder);
        }
    }

    [RelayCommand]
    private void RemoveFolder()
    {
        if (SelectedFolder is not null)
        {
            SelectedFolders.Remove(SelectedFolder);
            SelectedFolder = null;
        }
    }

    [RelayCommand]
    private void StartScan()
    {
        ErrorMessage = null;

        if (SelectedFolders.Count == 0)
        {
            ErrorMessage = "Selecione ao menos uma pasta para escanear.";
            return;
        }

        if (!DetectExact && !DetectSimilarImages && !DetectSimilarVideos)
        {
            ErrorMessage = "Habilite ao menos um tipo de detecção.";
            return;
        }

        var options = new ScanOptions
        {
            RootFolders = [..SelectedFolders],
            IncludeSubfolders = IncludeSubfolders,
            MinFileSizeBytes = (long)(MinFileSizeKb * 1024),
            DetectExact = DetectExact,
            DetectSimilarImages = DetectSimilarImages,
            DetectSimilarVideos = DetectSimilarVideos,
            ImageSimilarityThreshold = ImageSimilarityPercent / 100.0,
            VideoSimilarityThreshold = VideoSimilarityPercent / 100.0,
        };

        var smartSelectOptions = new SmartSelectOptions { Primary = KeepStrategy };

        SaveCurrentSettings();
        ScanRequested?.Invoke(this, new ScanRequestedEventArgs(options, smartSelectOptions));
    }
}

/// <summary>Dados carregados pelo evento <see cref="ScanSetupViewModel.ScanRequested"/>.</summary>
public sealed class ScanRequestedEventArgs(ScanOptions options, SmartSelectOptions smartSelectOptions) : EventArgs
{
    public ScanOptions Options { get; } = options;

    public SmartSelectOptions SmartSelectOptions { get; } = smartSelectOptions;
}
