using System.Windows;
using System.Windows.Controls;
using OneClick.Desktop.ViewModels;

namespace OneClick.Desktop.Controls;

public partial class AgentPanel : UserControl
{
    public AgentPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        if (DataContext is AgentPanelViewModel vm)
            WireViewModel(vm);
    }

    /// <summary>
    /// Convenience accessor for shell wiring. The shell may also set/bind DataContext directly.
    /// </summary>
    public AgentPanelViewModel ViewModel
    {
        get
        {
            if (DataContext is not AgentPanelViewModel vm)
            {
                vm = new AgentPanelViewModel();
                DataContext = vm;
            }

            return vm;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is AgentPanelViewModel vm)
            WireViewModel(vm);
    }

    private static void WireViewModel(AgentPanelViewModel vm)
    {
        // No default shell callbacks; shell must wire registry/runner execution explicitly.
        // This method exists so the panel has a single wiring hook if defaults are added later.
        _ = vm;
    }
}
