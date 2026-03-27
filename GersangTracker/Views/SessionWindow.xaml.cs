using GersangTracker.Models;
using GersangTracker.Services;
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
    public partial class SessionWindow : Window
    {
        private readonly SessionViewModel _viewModel;
        private readonly DatabaseService _databaseService;
        public SessionWindow(SessionViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _databaseService = new DatabaseService();
            DataContext = _viewModel;
        }
        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            await _viewModel.LoadSessionsAsync();
        }
        private async void Session_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.DataContext is Session session)
            {
                var monster = new Monster { Id = session.MonsterId, Name = _viewModel.MonsterName };
                var dropLogs = await _databaseService.GetDropLogsBySessionAsync(session.Id);

                // PriceItem 목록 생성
                var priceItems = dropLogs
                    .GroupBy(d => d.ItemName)
                    .Select(g => new PriceItem
                    {
                        ItemName = g.Key,
                        TotalQuantity = g.Sum(d => d.Quantity),
                        UnitPriceInput = g.First().UnitPrice.ToString("N0")
                    }).ToList();

                var resultViewModel = new ResultViewModel(session, monster, priceItems, dropLogs);
                var resultWindow = new ResultWindow(resultViewModel);
                resultWindow.Owner = this;
                resultWindow.Show();
            }
        }
    }
}
