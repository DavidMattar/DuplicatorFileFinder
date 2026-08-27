using System.IO.Abstractions;
using System.Windows;
using DuplicatorFinder.App.Services;
using DuplicatorFinder.App.ViewModels;
using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Detection;
using DuplicatorFinder.Core.Engine;
using DuplicatorFinder.Core.Hashing;
using DuplicatorFinder.Core.Scanning;
using DuplicatorFinder.Core.SmartSelect;
using DuplicatorFinder.Infrastructure.Recycle;
using DuplicatorFinder.Infrastructure.Settings;
using DuplicatorFinder.Infrastructure.Video;
using Microsoft.Extensions.DependencyInjection;

namespace DuplicatorFinder.App;

/// <summary>
/// Ponto de entrada do aplicativo e "composition root" (padrão Dependency Injection): é o
/// único lugar do projeto onde implementações concretas de interfaces do Core/Infrastructure
/// são associadas às suas interfaces. Nenhuma outra classe deve instanciar essas
/// implementações diretamente — todas recebem suas dependências pelo construtor.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    /// <summary>
    /// Registra todos os serviços do app no contêiner de DI. Serviços sem estado próprio
    /// relevante entre chamadas são registrados como Singleton (uma única instância para
    /// toda a vida do app) — nenhum deles guarda estado específico de um escaneamento, que
    /// fica nos ViewModels criados por escaneamento (<see cref="ScanProgressViewModel"/>,
    /// <see cref="ResultsViewModel"/>).
    /// </summary>
    private static void ConfigureServices(ServiceCollection services)
    {
        // Infraestrutura de baixo nível (Core + Infrastructure).
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IFileHasher, FileHasher>();
        services.AddSingleton<IImageHasher, ImageHasher>();
        services.AddSingleton<IFileScanner, FileScanner>();
        services.AddSingleton<IRecycleBinService, WindowsRecycleBinService>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<ISmartSelectStrategy, DefaultSmartSelectStrategy>();
        services.AddSingleton<FfmpegBootstrap>();
        services.AddSingleton<IVideoFrameExtractor, XabeFfmpegFrameExtractor>();

        // Detectores de duplicados: registrados como implementações de IDuplicateDetector
        // (podem existir vários) para que o DuplicateScanEngine os rode sem precisar saber
        // quantos ou quais tipos concretos existem — ver o padrão Strategy documentado em
        // IDuplicateDetector.
        services.AddSingleton<IDuplicateDetector, ExactHashDetector>();
        services.AddSingleton<IDuplicateDetector, ImageSimilarityDetector>();
        services.AddSingleton<IDuplicateDetector, VideoSimilarityDetector>();

        services.AddSingleton<DuplicateScanEngine>();

        // Camada de UI.
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ScanSetupViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
