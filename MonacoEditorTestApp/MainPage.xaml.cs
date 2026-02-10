// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MonacoEditorTestApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();

            Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            //var tabItem = new TabViewItem();
            //tabItem.IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource() { Symbol = Symbol.Document };
            //tabItem.Header = "Original item";
            //tabItem.Content = new EditorControl();
            //editors.TabItems.Add(tabItem);
            AddEditorTab();
        }

        private void TabView_AddTabButtonClick(TabView sender, object args)
        {
            AddEditorTab();
        }

        private void AddEditorTab()
        {
            var tabItem = new TabViewItem
            {
                IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource() { Symbol = Symbol.Document },
                Header = "item",
                Content = new EditorControl()
            };
            editors.TabItems.Add(tabItem);
        }


        private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            editors.TabItems.Remove(args.Item);
        }
    }
}