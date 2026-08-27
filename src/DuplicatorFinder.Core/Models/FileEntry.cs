namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Representa um arquivo encontrado durante o escaneamento, com os metadados mínimos
/// necessários para os detectores de duplicados (tamanho, datas, extensão).
/// É um "record" (tipo imutável) porque esses dados são apenas lidos do disco uma vez
/// e nunca precisam ser alterados depois — só copiados/agrupados.
/// </summary>
/// <param name="FullPath">Caminho completo do arquivo no disco.</param>
/// <param name="SizeBytes">Tamanho do arquivo em bytes.</param>
/// <param name="CreatedUtc">Data de criação do arquivo, em UTC.</param>
/// <param name="ModifiedUtc">Data da última modificação do arquivo, em UTC.</param>
/// <param name="Extension">Extensão do arquivo (incluindo o ponto, ex: ".jpg"), em minúsculas.</param>
public sealed record FileEntry(
    string FullPath,
    long SizeBytes,
    DateTime CreatedUtc,
    DateTime ModifiedUtc,
    string Extension);
