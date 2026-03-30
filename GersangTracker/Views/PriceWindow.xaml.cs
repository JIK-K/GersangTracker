using GersangTracker.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
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

    public partial class PriceWindow : Window
    {
        private readonly PriceViewModel _viewModel;
        public PriceWindow(PriceViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }
        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            try
            {
                await _viewModel.LoadPreviousPricesAsync();
            }catch(FileNotFoundException ex)
            {
                MessageBox.Show(ex.Message, "OCR 파일 없음",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
            }
        }
        private async void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.CalculateCommand.ExecuteAsync(null);

            var session = await _viewModel.GetSessionAsync();
            var dropLogs = await _viewModel.GetDropLogsAsync();

            var resultViewModel = new ResultViewModel(
                session,
                _viewModel.Monster,
                _viewModel.PriceItems.ToList(),
                dropLogs);
            var resultWindow = new ResultWindow(resultViewModel);
            resultWindow.Owner = this.Owner;
            resultWindow.Show();
              
            Close();
        }
    }
}
