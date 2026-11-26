using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OneClick.Desktop.ViewModels;

namespace OneClick.Desktop.Controls;

public partial class SnapshotPanel : UserControl
{
    public SnapshotPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        if (DataContext is SnapshotPanelViewModel vm)
            WireViewModel(vm);
    }

    /// <summary>
    /// Convenience accessor for shell wiring. The shell may also set/bind DataContext directly.
    /// </summary>
    public SnapshotPanelViewModel ViewModel
    {
        get
        {
            if (DataContext is not SnapshotPanelViewModel vm)
            {
                vm = new SnapshotPanelViewModel();
                DataContext = vm;
            }

            return vm;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is SnapshotPanelViewModel vm)
            WireViewModel(vm);
    }

    private static void WireViewModel(SnapshotPanelViewModel vm)
    {
        // Default save dialog wiring; shell may override by setting PickSaveFile itself.
        vm.PickSaveFile ??= PickSaveFile;
    }

    private static string? PickSaveFile(string suggestedName, string filter, string title)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedName,
            Filter = filter,
            Title = title,
            OverwritePrompt = true,
            AddExtension = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
