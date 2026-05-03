using System.ComponentModel;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.ViewModels;

using LogicAppTheme = TRViS.DTAC.Logic.Abstractions.AppTheme;
using MauiAppTheme = Microsoft.Maui.ApplicationModel.AppTheme;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Adapter that wraps AppViewModel to implement IAppViewModelProvider.
/// </summary>
internal class AppViewModelAdapter : IAppViewModelProvider
{
    private readonly AppViewModel _viewModel;

    public AppViewModelAdapter(AppViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.CurrentAppThemeChanged += OnCurrentAppThemeChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
    }

    private void OnCurrentAppThemeChanged(object? sender, TRViS.Utils.ValueChangedEventArgs<MauiAppTheme> e)
    {
        CurrentAppThemeChanged?.Invoke(this, ToLogicTheme(e.NewValue));
    }

    public TRViS.IO.Models.WorkGroup? SelectedWorkGroup => _viewModel.SelectedWorkGroup;
    public TRViS.IO.Models.Work? SelectedWork => _viewModel.SelectedWork;
    public TRViS.IO.Models.TrainData? SelectedTrainData => _viewModel.SelectedTrainData;

    public LogicAppTheme CurrentAppTheme
    {
        get => ToLogicTheme(_viewModel.CurrentAppTheme);
        set
        {
            var mauiTheme = ToMauiTheme(value);
            _viewModel.CurrentAppTheme = mauiTheme;
            // Also update Application.Current.UserAppTheme to flip the OS-level theme
            if (Application.Current is not null)
            {
                Application.Current.UserAppTheme = mauiTheme;
            }
        }
    }

    public bool IsBgAppIconVisible
    {
        get => _viewModel.IsBgAppIconVisible;
        set => _viewModel.IsBgAppIconVisible = value;
    }

    public double WindowWidth => _viewModel.WindowWidth;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<LogicAppTheme>? CurrentAppThemeChanged;

    private static LogicAppTheme ToLogicTheme(MauiAppTheme theme) => (LogicAppTheme)(int)theme;
    private static MauiAppTheme ToMauiTheme(LogicAppTheme theme) => (MauiAppTheme)(int)theme;
}
