using CommunityToolkit.Mvvm.Input;

namespace Danish.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private int _selectedTabIndex = 0;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                OnPropertyChanged(nameof(IsFlashcardsTab));
                OnPropertyChanged(nameof(IsQuizTab));
                OnPropertyChanged(nameof(IsWritingTab));
                OnPropertyChanged(nameof(IsSpeakingTab));
                OnPropertyChanged(nameof(IsMbspTab));
            }
        }
    }

    public bool IsFlashcardsTab => SelectedTabIndex == 0;
    public bool IsQuizTab      => SelectedTabIndex == 1;
    public bool IsWritingTab   => SelectedTabIndex == 2;
    public bool IsSpeakingTab  => SelectedTabIndex == 3;
    public bool IsMbspTab      => SelectedTabIndex == 4;

    public IRelayCommand<string?> SelectTabCommand { get; }

    public MainWindowViewModel()
    {
        SelectTabCommand = new RelayCommand<string?>(tab =>
        {
            if (int.TryParse(tab, out var idx))
                SelectedTabIndex = idx;
        });
    }
}
