using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuplicatorFinder.Core.Engine;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// ViewModel da tela de progresso: dispara o escaneamento no <see cref="DuplicateScanEngine"/>
/// e expõe seu progresso como propriedades observáveis para a barra de progresso e o texto
/// de status. Uma instância nova é criada para cada escaneamento (não é singleton) — ver
/// <see cref="MainViewModel"/>, que a cria e observa seus eventos de conclusão.
/// </summary>
public sealed partial class ScanProgressViewModel : ObservableObject
{
    private readonly DuplicateScanEngine _engine;
    private CancellationTokenSource? _cancellationTokenSource;

    public ScanProgressViewModel(DuplicateScanEngine engine)
    {
        _engine = engine;
    }

    [ObservableProperty]
    private string _phase = "Iniciando...";

    [ObservableProperty]
    private string? _currentFile;

    [ObservableProperty]
    private double _globalPercent;

    [ObservableProperty]
    private long _filesScanned;

    [ObservableProperty]
    private int _groupsFoundSoFar;

    /// <summary>Disparado quando o escaneamento termina normalmente, com o resultado completo.</summary>
    public event EventHandler<ScanResult>? ScanCompleted;

    /// <summary>Disparado quando o usuário cancela o escaneamento antes de ele terminar.</summary>
    public event EventHandler? ScanCancelled;

    /// <summary>Disparado quando o escaneamento falha com um erro inesperado.</summary>
    public event EventHandler<string>? ScanFailed;

    /// <summary>
    /// Inicia o escaneamento e só retorna quando ele termina (com sucesso, cancelamento ou
    /// falha) — quem chama deve apenas iniciar a Task e não precisa aguardá-la para a UI
    /// continuar respondendo, já que o motor inteiro é assíncrono.
    /// </summary>
    public async Task StartAsync(ScanOptions options, SmartSelectOptions smartSelectOptions)
    {
        _cancellationTokenSource = new CancellationTokenSource();

        // System.Progress<T> captura o SynchronizationContext da thread atual (a UI thread,
        // já que este método é chamado a partir de um comando disparado pela UI) e entrega
        // cada Report() de volta nela automaticamente — por isso as propriedades observáveis
        // abaixo podem ser atualizadas diretamente, sem Dispatcher.Invoke manual.
        var progress = new Progress<ScanProgress>(OnProgress);

        try
        {
            var result = await _engine.RunAsync(options, smartSelectOptions, progress, _cancellationTokenSource.Token);
            ScanCompleted?.Invoke(this, result);
        }
        catch (OperationCanceledException)
        {
            ScanCancelled?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ScanFailed?.Invoke(this, ex.Message);
        }
    }

    private void OnProgress(ScanProgress progress)
    {
        Phase = progress.Phase;
        CurrentFile = progress.CurrentFile;
        FilesScanned = progress.FilesScanned;
        GroupsFoundSoFar = progress.GroupsFoundSoFar;
        GlobalPercent = progress.GlobalFraction * 100.0;
    }

    [RelayCommand]
    private void Cancel()
    {
        // Chamar Cancel() mais de uma vez (ex: usuário clica de novo enquanto já está
        // cancelando) é seguro — CancellationTokenSource.Cancel é idempotente.
        _cancellationTokenSource?.Cancel();
    }
}
