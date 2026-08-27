namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Conjunto de dois ou mais arquivos considerados duplicados (exatos ou similares) entre si.
/// É a unidade principal exibida na tela de resultados: cada grupo tem exatamente um arquivo
/// "mantido" (<see cref="DuplicateFile.IsKept"/>) e um ou mais candidatos à exclusão.
/// </summary>
public sealed class DuplicateGroup
{
    /// <summary>Identificador único do grupo, usado para rastreio/binding na UI.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Tipo de detecção que originou este grupo (exato, imagem ou vídeo similar).</summary>
    public required DuplicateKind Kind { get; init; }

    /// <summary>Arquivos que compõem o grupo (sempre 2 ou mais).</summary>
    public required List<DuplicateFile> Files { get; init; }

    /// <summary>
    /// Grau de similaridade do grupo, de 0.0 a 1.0 (1.0 = idêntico). Para duplicados exatos
    /// é sempre 1.0; para imagens/vídeos similares reflete a distância de Hamming normalizada.
    /// </summary>
    public double SimilarityScore { get; init; } = 1.0;

    /// <summary>
    /// Espaço em disco (em bytes) que seria liberado ao excluir todos os arquivos do grupo
    /// exceto o mantido. Calculado como a soma do tamanho de todos os arquivos marcados para
    /// exclusão — exposto pronto aqui para a UI não precisar recalcular.
    /// </summary>
    public long WastedBytes => Files.Where(f => !f.IsKept).Sum(f => f.File.SizeBytes);
}
