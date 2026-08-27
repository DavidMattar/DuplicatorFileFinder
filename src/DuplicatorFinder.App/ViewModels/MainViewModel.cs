using CommunityToolkit.Mvvm.ComponentModel;
using DuplicatorFinder.App.Services;
using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Engine;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// ViewModel raiz da janela principal. Age como "navegador" simples entre as três telas do
/// fluxo (Configuração → Progresso → Resultados), trocando o valor de
/// <see cref="CurrentViewModel"/> — a <see cref="MainWindow"/> observa essa propriedade
/// através de DataTemplates (um por tipo de ViewModel) para decidir qual View exibir.
/// A navegação é feita ouvindo eventos simples dos ViewModels de cada tela (em vez de um
/// serviço de navegação genérico) porque o fluxo é linear e pequeno — um Messenger ou
/// NavigationService adicionariam indireção sem benefício real neste tamanho de app.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly DuplicateScanEngine _engine;
    private readonly IRecycleBinService _recycleBinService;
    private readonly IDialogService _dialogService;
    private readonly ScanSetupViewModel _setupViewModel;

    [ObservableProperty]
    private ObservableObject _currentViewModel;

    public MainViewModel(
        ScanSetupViewModel setupViewModel,
        DuplicateScanEngine engine,
        IRecycleBinService recycleBinService,
        IDialogService dialogService)
    {
        _engine = engine;
        _recycleBinService = recycleBinService;
        _dialogService = dialogService;

        _setupViewModel = setupViewModel;
        _setupViewModel.ScanRequested += OnScanRequested;

        _currentViewModel = setupViewModel;
    }

    /// <summary>
    /// Reage ao pedido de escaneamento da tela inicial: cria uma nova tela de progresso,
    /// navega para ela e inicia o motor. É "async void" porque é um manipulador de evento
    /// (não pode retornar Task) — toda exceção do escaneamento é tratada dentro de
    /// <see cref="ScanProgressViewModel.StartAsync"/>, então nada escapa sem tratamento aqui.
    /// </summary>
    private async void OnScanRequested(object? sender, ScanRequestedEventArgs e)
    {
        var progressViewModel = new ScanProgressViewModel(_engine);
        progressViewModel.ScanCompleted += OnScanCompleted;
        progressViewModel.ScanCancelled += (_, _) => CurrentViewModel = _setupViewModel;
        progressViewModel.ScanFailed += OnScanFailed;

        CurrentViewModel = progressViewModel;

        await progressViewModel.StartAsync(e.Options, e.SmartSelectOptions);
    }

    private void OnScanCompleted(object? sender, ScanResult result)
    {
        var resultsViewModel = new ResultsViewModel(result, _recycleBinService, _dialogService);
        resultsViewModel.NewScanRequested += (_, _) => CurrentViewModel = _setupViewModel;

        CurrentViewModel = resultsViewModel;
    }

    private void OnScanFailed(object? sender, string errorMessage)
    {
        _dialogService.ShowError($"Falha durante o escaneamento: {errorMessage}");
        CurrentViewModel = _setupViewModel;
    }
}
