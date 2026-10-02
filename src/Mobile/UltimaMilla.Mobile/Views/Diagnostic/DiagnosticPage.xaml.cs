using UltimaMilla.Mobile.ViewModels.Diagnostic;

namespace UltimaMilla.Mobile.Views.Diagnostic;

public partial class DiagnosticPage : ContentPage
{
    private readonly DiagnosticViewModel _viewModel;

    public DiagnosticPage(DiagnosticViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshOfflineCountAsync();
    }
}
