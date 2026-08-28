using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// Uma aba da janela "Abrir locais" (<see cref="Views.OpenLocationsWindow"/>), representando
/// uma única cópia de um grupo de duplicados. Sabe como abrir o Explorer do Windows já com uma
/// busca feita pelo nome deste arquivo, na pasta onde ele está — via o esquema de URI
/// <c>search-ms:</c>, suportado nativamente pelo Explorer desde o Windows 7 para abrir a UI de
/// busca pré-populada sem precisar de nenhuma automação COM.
/// </summary>
public sealed partial class LocationTabViewModel : ObservableObject
{
    /// <summary>Caminho completo do arquivo, exibido na aba para referência.</summary>
    public string FullPath { get; }

    /// <summary>Nome do arquivo, usado como cabeçalho da aba e como termo de busca no Explorer.</summary>
    public string FileName { get; }

    /// <summary>Pasta que contém o arquivo — é o escopo (crumb) da busca aberta no Explorer.</summary>
    public string FolderPath { get; }

    public LocationTabViewModel(string fullPath)
    {
        FullPath = fullPath;
        FileName = Path.GetFileName(fullPath);
        FolderPath = Path.GetDirectoryName(fullPath) ?? fullPath;
    }

    /// <summary>
    /// Abre uma nova janela do Explorer já navegada para <see cref="FolderPath"/> com a busca
    /// preenchida com <see cref="FileName"/>. Chamado automaticamente quando esta aba é
    /// selecionada pela primeira vez (ver <see cref="OpenLocationsViewModel"/>), e também
    /// disponível como botão manual para o caso de o usuário ter fechado a janela do Explorer
    /// e querer abri-la de novo sem trocar de aba.
    /// </summary>
    [RelayCommand]
    public void OpenExplorerSearch()
    {
        try
        {
            var query = Uri.EscapeDataString(FileName);
            var location = Uri.EscapeDataString(FolderPath);

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"search-ms:query={query}&crumb=location:{location}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
            // Amplamente capturado de propósito: se o Explorer não puder ser aberto por algum
            // motivo (ex: pasta removida, política do sistema bloqueando), a pior consequência
            // aceitável aqui é simplesmente nada acontecer — nunca travar a janela de preview.
        }
    }
}
