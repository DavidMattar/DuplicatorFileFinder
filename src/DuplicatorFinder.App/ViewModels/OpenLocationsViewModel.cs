using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// ViewModel da janela "Abrir locais" (<see cref="Views.OpenLocationsWindow"/>): monta uma
/// <see cref="LocationTabViewModel"/> por arquivo do grupo de duplicados e garante que, ao
/// selecionar cada aba pela primeira vez, o Explorer é aberto automaticamente já buscando
/// aquele arquivo — o usuário não precisa clicar em nada além de trocar de aba para ver cada
/// cópia localizada no disco.
/// </summary>
public sealed partial class OpenLocationsViewModel : ObservableObject
{
    /// <summary>Uma aba por arquivo do grupo (a mantida e todas as cópias), na ordem em que o grupo foi exibido.</summary>
    public ObservableCollection<LocationTabViewModel> Tabs { get; }

    [ObservableProperty]
    private LocationTabViewModel? _selectedTab;

    public OpenLocationsViewModel(IReadOnlyList<FileCandidateViewModel> files)
    {
        Tabs = new ObservableCollection<LocationTabViewModel>(
            files.Select(file => new LocationTabViewModel(file.FullPath)));

        SelectedTab = Tabs.FirstOrDefault();
    }

    /// <summary>
    /// Gerado/chamado automaticamente pelo source generator do CommunityToolkit.Mvvm sempre
    /// que <see cref="SelectedTab"/> muda — inclusive na atribuição inicial do construtor, o
    /// que já abre o Explorer para a primeira aba sem precisar de nenhuma ação extra do usuário.
    /// </summary>
    partial void OnSelectedTabChanged(LocationTabViewModel? value)
    {
        value?.OpenExplorerSearch();
    }
}
