namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Todas as opções configuráveis de um escaneamento, definidas pelo usuário na tela de
/// configuração (ScanSetupView) antes de iniciar a busca por duplicados.
/// </summary>
public sealed class ScanOptions
{
    /// <summary>Pastas raiz onde a busca deve começar.</summary>
    public required List<string> RootFolders { get; init; }

    /// <summary>Quando verdadeiro, escaneia também as subpastas de cada pasta raiz.</summary>
    public bool IncludeSubfolders { get; init; } = true;

    /// <summary>
    /// Tamanho mínimo (em bytes) para um arquivo ser considerado no escaneamento.
    /// Arquivos muito pequenos (ex: ícones de 1KB) raramente valem a pena revisar e só
    /// aumentam o tempo de escaneamento — por isso o filtro existe.
    /// </summary>
    public long MinFileSizeBytes { get; init; } = 0;

    /// <summary>
    /// Extensões a incluir explicitamente (ex: ".jpg", ".png"). Vazio = incluir todas,
    /// exceto as listadas em <see cref="ExcludeExtensions"/>.
    /// </summary>
    public HashSet<string> IncludeExtensions { get; init; } = [];

    /// <summary>Extensões a ignorar mesmo que estariam dentro do filtro de inclusão.</summary>
    public HashSet<string> ExcludeExtensions { get; init; } = [];

    /// <summary>Habilita a busca por arquivos idênticos byte a byte (qualquer tipo).</summary>
    public bool DetectExact { get; init; } = true;

    /// <summary>Habilita a busca por imagens visualmente similares.</summary>
    public bool DetectSimilarImages { get; init; } = true;

    /// <summary>Habilita a busca por vídeos visualmente similares.</summary>
    public bool DetectSimilarVideos { get; init; } = true;

    /// <summary>
    /// Sensibilidade da comparação de imagens, de 0.0 (só idênticas) a 1.0 (muito tolerante).
    /// Controlado pelo slider da UI; convertido internamente em distância de Hamming máxima
    /// pelo <see cref="Detection.ImageSimilarityDetector"/>.
    /// </summary>
    public double ImageSimilarityThreshold { get; init; } = 0.90;

    /// <summary>Sensibilidade da comparação de vídeos, na mesma escala 0.0–1.0.</summary>
    public double VideoSimilarityThreshold { get; init; } = 0.90;

    /// <summary>
    /// Grau máximo de paralelismo permitido para operações de I/O (hashing, decodificação).
    /// Valores menores evitam saturar discos mecânicos (HDD); valores maiores aproveitam
    /// melhor SSDs/NVMe. Padrão: número de processadores lógicos da máquina.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;
}
