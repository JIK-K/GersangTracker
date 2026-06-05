using GersangTracker.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;

namespace GersangTracker.Views
{

    public partial class HuntingWindow : Window
    {
        private readonly HuntingViewModel _viewModel;
        public HuntingWindow(HuntingViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            await _viewModel.StartAsync();
        }
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => DragMove();

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();

        private async void StopHunting_Click(object sender, RoutedEventArgs e)
        {
            int sessionId = await _viewModel.StopAsync();

            var dbService = App.ServiceProvider.GetRequiredService<GersangTracker.Services.DatabaseService>();
            var priceViewModel = new PriceViewModel(
                sessionId,
                _viewModel.CurrentMonster,
                _viewModel.ItemSummaries.ToList(),
                dbService);

            var priceWindow = new PriceWindow(priceViewModel);
            priceWindow.Show();

            Close();
        }

    }
}
