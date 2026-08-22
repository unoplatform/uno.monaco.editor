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
            AddEditorTab();

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
                MaybeExit(isHealthy);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SELF_VERIFY_RESULT:ERROR:{ex.Message}");
                MaybeExit(false);
            }
        }

        /// <summary>
        /// Terminates the app with the self-verification outcome as its exit code when
        /// <c>MONACO_SELF_VERIFY_EXIT=1</c>. Off by default so manual validation keeps the
        /// window open for inspection; shell-driven runs (for example
        /// <c>xvfb-run ... dotnet run</c>) opt in to get a pass/fail exit code.
        /// <para>The <c>SELF_VERIFY_RESULT:</c> stdout line remains the authoritative signal:
        /// exiting from the UI thread can race native window teardown, so automated harnesses
        /// parse stdout and treat the exit code as corroborating evidence only.</para>
        /// </summary>
        private static void MaybeExit(bool isHealthy)
        {
            if (Environment.GetEnvironmentVariable("MONACO_SELF_VERIFY_EXIT") != "1")
            {
                return;
            }

            Console.Out.Flush();
            Environment.Exit(isHealthy ? 0 : 1);
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
