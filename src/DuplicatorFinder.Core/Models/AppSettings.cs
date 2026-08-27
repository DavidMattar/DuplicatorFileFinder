namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Configurações do aplicativo que persistem entre sessões (salvas em disco pelo
/// <see cref="Abstractions.ISettingsService"/>, tipicamente em %AppData%).
/// Separado de <see cref="ScanOptions"/> porque isto guarda preferências de longo prazo
/// do usuário, não os parâmetros de um escaneamento específico.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Pastas marcadas como favoritas pelo usuário, sugeridas na tela de configuração.</summary>
    public List<string> FavoriteFolders { get; init; } = [];

    /// <summary>Última configuração de escaneamento usada, para pré-popular a tela na próxima abertura.</summary>
    public double LastImageSimilarityThreshold { get; init; } = 0.90;

    /// <summary>Último threshold de similaridade de vídeo usado.</summary>
    public double LastVideoSimilarityThreshold { get; init; } = 0.90;

    /// <summary>Estratégia de smart-select preferida pelo usuário.</summary>
    public KeepStrategy PreferredKeepStrategy { get; init; } = KeepStrategy.OldestFile;
}
