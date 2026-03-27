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
    public partial class ResultWindow : Window
    {
        private readonly ResultViewModel _viewModel;
        public ResultWindow(ResultViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

       private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
