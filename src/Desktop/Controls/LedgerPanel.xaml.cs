using System.Windows.Controls;
using OneClick.Desktop.ViewModels.Ledger;

namespace OneClick.Desktop.Controls;

public partial class LedgerPanel : UserControl
{
    public LedgerPanel()
    {
        InitializeComponent();
        if (DataContext is not LedgerPanelViewModel)
            DataContext = new LedgerPanelViewModel();
    }

    public LedgerPanelViewModel ViewModel =>
        DataContext as LedgerPanelViewModel ?? throw new InvalidOperationException("LedgerPanel DataContext must be a LedgerPanelViewModel.");
}
