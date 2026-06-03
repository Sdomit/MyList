namespace MyList.Views;

public partial class DiagnosticsView : System.Windows.Controls.UserControl
{
    public DiagnosticsView()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as MyList.ViewModels.DiagnosticsViewModel)?.StartAutoRefresh();
        Unloaded += (_, _) => (DataContext as MyList.ViewModels.DiagnosticsViewModel)?.StopAutoRefresh();
    }
}
