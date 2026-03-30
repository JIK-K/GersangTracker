using GersangTracker.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace GersangTracker.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) =>
            {
                var vm = (MainViewModel)DataContext;
                await vm.LoadMonstersCommand.ExecuteAsync(null);
            };
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => DragMove();

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}