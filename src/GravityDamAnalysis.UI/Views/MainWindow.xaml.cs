using System.Windows;
using GravityDamAnalysis.UI.ViewModels;

namespace GravityDamAnalysis.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
} 