using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Infrastructure.Settings;

/// <summary>
/// Implementação de <see cref="ISettingsService"/> que persiste as configurações do usuário
/// como um arquivo JSON simples em %AppData%. Não há necessidade de nada mais elaborado
/// (banco de dados, registro do Windows) para um punhado de preferências pequenas.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _settingsFilePath;

    /// <param name="fileSystem">Abstração do sistema de arquivos, para permitir testes sem gravar em disco real.</param>
    /// <param name="settingsFilePath">Caminho do arquivo de configurações; se omitido, usa o padrão em %AppData%\DuplicatorFinder\settings.json.</param>
    public JsonSettingsService(IFileSystem fileSystem, string? settingsFilePath = null)
    {
        _fileSystem = fileSystem;
        _settingsFilePath = settingsFilePath ?? GetDefaultSettingsFilePath();
    }

    private static string GetDefaultSettingsFilePath()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DuplicatorFinder");

        return Path.Combine(appDataFolder, "settings.json");
    }

    /// <inheritdoc />
    public AppSettings Load()
    {
        try
        {
            if (!_fileSystem.File.Exists(_settingsFilePath))
            {
                return new AppSettings();
            }

            var json = _fileSystem.File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (IOException)
        {
            // Arquivo corrompido/inacessível: melhor continuar com os padrões do que travar o app.
            return new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        var directory = _fileSystem.Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory) && !_fileSystem.Directory.Exists(directory))
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        _fileSystem.File.WriteAllText(_settingsFilePath, json);
    }
}
