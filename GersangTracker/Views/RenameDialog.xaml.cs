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
    /// <summary>
    /// RenameDialog.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class RenameDialog : Window
    {
        public string NewName => NameInput.Text.Trim();
        public RenameDialog(string currentName)
        {
            InitializeComponent();
            NameInput.Text = currentName;
            NameInput.SelectAll();
            NameInput.Focus();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameInput.Text)) return;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

    }
}
