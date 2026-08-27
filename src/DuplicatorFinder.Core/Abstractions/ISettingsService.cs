using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Core.Abstractions;

/// <summary>
/// Contrato para persistência das configurações do usuário (padrão Repository: isola o
/// resto do app de saber *onde* ou *como* as configurações são guardadas em disco).
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Carrega as configurações salvas. Se nenhuma configuração existir ainda (primeira
    /// execução do app), retorna uma instância de <see cref="AppSettings"/> com os valores padrão.
    /// </summary>
    AppSettings Load();

    /// <summary>Salva as configurações informadas, sobrescrevendo o que estava salvo antes.</summary>
    void Save(AppSettings settings);
}
