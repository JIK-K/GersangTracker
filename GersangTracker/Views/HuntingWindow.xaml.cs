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

        private async void StopHunting_Click(object sender, RoutedEventArgs e)
        {
            int sessionId = await _viewModel.StopAsync();
            Close();
        }
    }
}
