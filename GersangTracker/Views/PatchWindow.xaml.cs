using System.Windows;
using GersangTracker.ViewModels;

namespace GersangTracker.Views
{
    public partial class PatchWindow : Window
    {
        public PatchWindow()
        {
            InitializeComponent();

            if (DataContext is PatchViewModel vm)
            {
                vm.CloseAction = new System.Action(this.Close);
            }
        }
    }
}