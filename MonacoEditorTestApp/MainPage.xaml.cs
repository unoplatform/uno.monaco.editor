using System.Text.Json;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MonacoEditorTestApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private bool _loaded;

        public MainPage()
        {
            InitializeComponent();

            Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            //var tabItem = new TabViewItem();
            //tabItem.IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource() { Symbol = Symbol.Document };
            //tabItem.Header = "Original item";
            //tabItem.Content = new EditorControl();
            //editors.TabItems.Add(tabItem);
            // Tab 0 must stay a plain CodeEditor. monaco.editor.getEditors() does enumerate a
            // diff editor's two sub-editors, so it is not a discriminator on its own: the desktop
            // fixture finds the plain page with "getEditors().length > 0 &&
            // getDiffEditors().length === 0", which needs a page carrying a standalone editor and
            // no diff widget, and the WASM tests reach the plain editor by subtracting the
            // sub-editors out of getEditors(), which needs at least one left over.
            AddEditorTab();

            // The diff sample goes in an always-realized panel rather than a tab whenever a
            // test harness needs to reach it, because TabView virtualizes non-selected tab
            // content and neither harness can select a tab: CDP sees WebView contents rather
            // than the XAML tree, and the WASM app renders its UI to a Skia canvas, so there
            // is no DOM tab header for Playwright to click either.
            //
            // On WASM that is unconditional -- an extra editor in the same document is cheap,
            // and there is no env var to read in the browser. On desktop each control costs a
            // whole WebView2, so it stays behind the flag the fixture sets and is a tab
            // otherwise.
            if (OperatingSystem.IsBrowser() || Environment.GetEnvironmentVariable("MONACO_DIFF_TAB") == "1")
            {
                ShowDiffPanel();
            }
            else
            {
                AddDiffEditorTab();
            }

            if (OperatingSystem.IsBrowser() || Environment.GetEnvironmentVariable("MONACO_MULTIDIFF_TAB") == "1")
            {
                ShowMultiDiffPanel();
            }
            else
            {
                AddMultiDiffEditorTab();
            }

            if (Environment.GetEnvironmentVariable("MONACO_SELF_VERIFY") == "1")
            {
                await RunSelfVerifyScenarioAsync();
            }
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

        private void ShowDiffPanel()
        {
            DiffPanelRow.Height = new GridLength(1, GridUnitType.Star);
            DiffPanelHost.Visibility = Visibility.Visible;
            DiffPanelHost.Content = new DiffEditorControl();
        }

        private void ShowMultiDiffPanel()
        {
            MultiDiffPanelRow.Height = new GridLength(1, GridUnitType.Star);
            MultiDiffPanelHost.Visibility = Visibility.Visible;
            MultiDiffPanelHost.Content = new MultiDiffEditorControl();
        }

        private void AddMultiDiffEditorTab()
        {
            var tabItem = new TabViewItem
            {
                IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource() { Symbol = Symbol.List },
                Header = "multi-diff",
                Content = new MultiDiffEditorControl()
            };
            editors.TabItems.Add(tabItem);
        }

        private void AddDiffEditorTab()
        {
            var tabItem = new TabViewItem
            {
                IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource() { Symbol = Symbol.Sync },
                Header = "diff",
                Content = new DiffEditorControl()
            };
            editors.TabItems.Add(tabItem);
        }

        private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            editors.TabItems.Remove(args.Item);
        }

        private async Task RunSelfVerifyScenarioAsync()
        {
            try
            {
                var readinessTimeout = TimeSpan.FromSeconds(75);

                var firstEditor = await WaitForEditorControlReadyAsync(0, readinessTimeout);
                var firstProbe = await firstEditor.CollectRuntimeProbeAsync("tab0-initial");
                Console.WriteLine($"SELF_VERIFY_PROBE:{firstProbe}");
                var firstFeatureProbe = await firstEditor.CollectFeatureProbeAsync("tab0-initial");
                Console.WriteLine($"SELF_VERIFY_FEATURE_PROBE:{firstFeatureProbe}");

                AddEditorTab();
                editors.SelectedIndex = editors.TabItems.Count - 1;

                var secondEditor = await WaitForEditorControlReadyAsync(editors.SelectedIndex, readinessTimeout);
                var secondProbe = await secondEditor.CollectRuntimeProbeAsync("tab1-selected");
                Console.WriteLine($"SELF_VERIFY_PROBE:{secondProbe}");
                var secondFeatureProbe = await secondEditor.CollectFeatureProbeAsync("tab1-selected");
                Console.WriteLine($"SELF_VERIFY_FEATURE_PROBE:{secondFeatureProbe}");

                editors.SelectedIndex = 0;
                var firstEditorAgain = await WaitForEditorControlReadyAsync(0, readinessTimeout);
                var firstProbeAgain = await firstEditorAgain.CollectRuntimeProbeAsync("tab0-returned");
                Console.WriteLine($"SELF_VERIFY_PROBE:{firstProbeAgain}");
                var firstFeatureProbeAgain = await firstEditorAgain.CollectFeatureProbeAsync("tab0-returned");
                Console.WriteLine($"SELF_VERIFY_FEATURE_PROBE:{firstFeatureProbeAgain}");

                var isHealthy = IsHealthyProbe(firstProbe)
                    && IsHealthyProbe(secondProbe)
                    && IsHealthyProbe(firstProbeAgain)
                    && IsHealthyFeatureProbe(firstFeatureProbe)
                    && IsHealthyFeatureProbe(secondFeatureProbe)
                    && IsHealthyFeatureProbe(firstFeatureProbeAgain);
                Console.WriteLine(isHealthy
                    ? "SELF_VERIFY_RESULT:PASS"
                    : "SELF_VERIFY_RESULT:FAIL");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SELF_VERIFY_RESULT:ERROR:{ex.Message}");
            }
        }

        private async Task<EditorControl> WaitForEditorControlReadyAsync(int tabIndex, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (TryGetEditorControl(tabIndex, out var editorControl)
                    && editorControl.IsEditorOperational)
                {
                    return editorControl;
                }

                await Task.Delay(100);
            }

            throw new TimeoutException($"Timed out waiting for editor control at tab index {tabIndex}.");
        }

        private bool TryGetEditorControl(int tabIndex, out EditorControl editorControl)
        {
            editorControl = null!;
            if (tabIndex < 0 || tabIndex >= editors.TabItems.Count)
            {
                return false;
            }

            if (editors.TabItems[tabIndex] is not TabViewItem tabViewItem
                || tabViewItem.Content is not EditorControl control)
            {
                return false;
            }

            editorControl = control;
            return true;
        }

        private static bool IsHealthyProbe(string probeJson)
        {
            if (string.IsNullOrWhiteSpace(probeJson))
            {
                return false;
            }

            try
            {
                using var json = JsonDocument.Parse(probeJson);
                var root = json.RootElement;
                if (root.TryGetProperty("error", out _))
                {
                    return false;
                }

                return root.GetProperty("hasEditor").GetBoolean()
                    && root.GetProperty("hasModel").GetBoolean()
                    && root.GetProperty("isConnected").GetBoolean()
                    && root.GetProperty("width").GetDouble() > 0
                    && root.GetProperty("height").GetDouble() > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsHealthyFeatureProbe(string probeJson)
        {
            if (string.IsNullOrWhiteSpace(probeJson))
            {
                return false;
            }

            try
            {
                using var json = JsonDocument.Parse(probeJson);
                var root = json.RootElement;
                if (root.TryGetProperty("error", out _))
                {
                    return false;
                }

                if (!root.TryGetProperty("hasTestAction", out var hasTestAction)
                    || !hasTestAction.GetBoolean())
                {
                    return false;
                }

                if (!root.TryGetProperty("hoverProbeResult", out var hoverProbeResult)
                    || hoverProbeResult.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                var hoverResultValue = hoverProbeResult.GetString();
                return !string.IsNullOrWhiteSpace(hoverResultValue)
                    && !string.Equals(hoverResultValue, "__null__", StringComparison.Ordinal)
                    && !hoverResultValue.StartsWith("__error__:", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }
    }
}
