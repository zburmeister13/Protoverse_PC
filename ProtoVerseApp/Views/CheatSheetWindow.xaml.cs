using System.Windows;
using ProtoVerseApp.Models.Manual;

namespace ProtoVerseApp.Views
{
    public partial class CheatSheetWindow : Window
    {
        public CheatSheetWindow(CheatSheet sheet)
        {
            InitializeComponent();
            DataContext = sheet;
        }
    }
}
