using System;
using System.Windows;
using System.Windows.Controls;
using PaperClippy.Core.Mascot;
using PaperClippy.Core.Personality;

namespace PaperClippy.App.Mascot;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();

        // Fill the lists before the data context is attached so the initial selection binds to real items.
        AnimationBox.ItemsSource = Enum.GetValues<AnimationIntensity>();
        PersonalityBox.ItemsSource = Enum.GetValues<PersonalityLevel>();
        ChatterBox.ItemsSource = Enum.GetValues<ChatterFrequency>();
        AccessoryBox.ItemsSource = SettingsViewModel.AccessoryOptions;
        SleepBox.ItemsSource = SettingsViewModel.SleepOptions;

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    public SettingsViewModel ViewModel => _viewModel;

    private void OnTimeLostFocus(object sender, RoutedEventArgs e) => _viewModel.RevertTimes();

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
