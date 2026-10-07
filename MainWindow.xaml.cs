using System.Windows;
using System.Windows.Controls;
using EventViewerWPF.ViewModels;

namespace EventViewerWPF;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
        {
            _vm.SecurePassword = pb.SecurePassword.Copy();
        }
    }
}