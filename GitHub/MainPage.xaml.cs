using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace GitHub
{

    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            MainFrame.Navigate(typeof(Views.Home));
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            SideSplitView.IsPaneOpen = !SideSplitView.IsPaneOpen;
        }

        private void IconItemListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
