using GersangTracker.ViewModels;
using System.Windows;

namespace GersangTracker.Views
{
    public partial class AccountSettingsWindow : Window
    {
        public AccountSettingsWindow(AccountSettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}