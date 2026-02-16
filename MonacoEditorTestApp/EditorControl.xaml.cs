using Microsoft.UI;
using Microsoft.UI.Text;

using Monaco;
using Monaco.Editor;
using Monaco.Helpers;

using MonacoEditorTestApp.Actions;
using MonacoEditorTestApp.Helpers;

using System.ComponentModel;
using System.Diagnostics;

using Windows.UI.Popups;
using Windows.UI.Text;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MonacoEditorTestApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class EditorControl : UserControl
    {
        private readonly StandaloneEditorConstructionOptions options;
        public string CodeContent
        {
            get { return (string)GetValue(CodeContentProperty); }
            set { SetValue(CodeContentProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Content.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CodeContentProperty =
            DependencyProperty.Register("CodeContent", typeof(string), typeof(EditorControl), new PropertyMetadata(""));

        private ContextKey? _myCondition;

        #region CSS Style Objects
        private readonly CssLineStyle CssLineDarkRed = new()
        {
            BackgroundColor = Colors.DarkRed,
        };

        private readonly CssLineStyle CssLineAliceBlue = new()
        {
            BackgroundColor = Colors.AliceBlue
        };

        private readonly CssInlineStyle CssInlineWhiteBold = new()
        {
            ForegroundColor = Colors.White,
            FontWeight = FontWeights.Bold,
            FontStyle = FontStyle.Italic
        };

        private readonly CssInlineStyle CssInlineStrikeThrough = new()
        {
            TextDecoration = TextDecoration.LineThrough
        };

        private readonly CssGlyphStyle CssGlyphError = new()
        {
            GlyphImage = new System.Uri("ms-appx-web:///Icons/error.png")
        };

        private readonly CssGlyphStyle CssGlyphWarning = new()
        {
            GlyphImage = new System.Uri("ms-appx-web:///Icons/warning.png")
        };
        #endregion

        public EditorControl()
        {
            InitializeComponent();
            options = Editor.Options;
            Editor.EditorLoading += Editor_Loading;
            Editor.EditorLoaded += Editor_Loaded;
            Editor.Unloaded += Editor_Unloaded;
            Editor.OpenLinkRequested += Editor_OpenLinkRequest;

            Editor.InternalException += Editor_InternalException;
            Editor.PropertyChanged += Editor_PropertyChanged;
        }

        private void Editor_Unloaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Editor_Unloaded");
        }

        private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Debug.WriteLine("Property changed - " + e.PropertyName);
        }

        private void Editor_InternalException(CodeEditor sender, Exception args)
        {
            // This shouldn't happen, if it does, then it's a bug.
        }


        private async void Editor_Loading(object sender, RoutedEventArgs e)
        {
            if (Editor is null)
            {
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(CodeContent))
                {
                    CodeContent = await FileIO.ReadTextAsync(await StorageFile.GetFileFromApplicationUriAsync(new System.Uri("ms-appx:///Content.txt")));


                    ButtonHighlightRange_Click(null, null);
                }

                // Ready for Code

                var available_languages = await Editor.Languages.GetLanguagesAsync();
                //Debugger.Break();

                // Code Lens Action
                var cmdId = await Editor.AddCommandAsync(0, async (args) =>
                {
                    try
                    {
                        var md = new MessageDialog("You hit the CodeLens command " + args[0]?.ToString());
                        WinRT.Interop.InitializeWithWindow.Initialize(md, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
                        await md.ShowAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                });

                if (cmdId is not null)
                {
                    await Editor.Languages.RegisterCodeLensProviderAsync("csharp", new EditorCodeLensProvider(cmdId));
                }

                await Editor.Languages.RegisterColorProviderAsync("csharp", new ColorProvider());

                await Editor.Languages.RegisterCompletionItemProviderAsync("csharp", new LanguageProvider());

                _myCondition = await Editor.CreateContextKeyAsync("MyCondition", false);

                await Editor.AddCommandAsync(KeyCode.F5, async (args) =>
                {
                    var md = new MessageDialog("You Hit F5!");
                    WinRT.Interop.InitializeWithWindow.Initialize(md, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
                    await md.ShowAsync();

                    // Turn off Command again.
                    _myCondition?.Reset();

                    // Refocus on CodeEditor
                    Editor.Focus(FocusState.Programmatic);
                }, _myCondition.Key);

                await Editor.AddCommandAsync(KeyMod.CtrlCmd | KeyCode.KEY_R, async (args) =>
                {
                    if (Editor.GetModel() is { } model)
                    {
                        var range = await model.GetFullModelRangeAsync();

                        var md = new MessageDialog("Document Range: " + range?.ToString());
                        WinRT.Interop.InitializeWithWindow.Initialize(md, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
                        await md.ShowAsync();
                    }

                    Editor.Focus(FocusState.Programmatic);
                });

                await Editor.AddCommandAsync(KeyMod.CtrlCmd | KeyCode.KEY_W, async (args) =>
                {
                    if (Editor.GetModel() is { } model && await Editor.GetPositionAsync() is { } position)
                    {
                        var word = await model.GetWordAtPositionAsync(position);

                        if (word == null)
                        {
                            var md = new MessageDialog("No Word Found.");
                            WinRT.Interop.InitializeWithWindow.Initialize(md, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
                            await md.ShowAsync();
                        }
                        else
                        {
                            var md = new MessageDialog("Word: " + word.Word + "[" + word.StartColumn + ", " + word.EndColumn + "]");
                            WinRT.Interop.InitializeWithWindow.Initialize(md, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
                            await md.ShowAsync();
                        }
                    }

                    Editor.Focus(FocusState.Programmatic);
                });

                await Editor.AddCommandAsync(KeyMod.CtrlCmd | KeyCode.KEY_L, async (args) =>
                {
                    if (Editor.GetModel() is { } model
                        && await Editor.GetPositionAsync() is { } position)
                    {
                        var line = await model.GetLineContentAsync(position.LineNumber);
                        var lines = await model.GetLinesContentAsync();
                        var count = await model.GetLineCountAsync();

                        var md = new MessageDialog("Current Line: " + line + "\nAll Lines [" + count + "]:\n" + string.Join("\n", lines));
                        WinRT.Interop.InitializeWithWindow.Initialize(md, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
                        await md.ShowAsync();
                    }

                    Editor.Focus(FocusState.Programmatic);
                });

                await Editor.AddCommandAsync(KeyMod.CtrlCmd | KeyCode.KEY_U, async (args) =>
                {
                    if (Editor.GetModel() is { } model)
                    {
                        var range = new Monaco.Range(2, 10, 3, 8);
                        var seg = await model.GetValueInRangeAsync(range);

                        var md = new MessageDialog("Segment " + range.ToString() + ": " + seg);
                        WinRT.Interop.InitializeWithWindow.Initialize(md, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
                        await md.ShowAsync();
                    }

                    Editor.Focus(FocusState.Programmatic);
                });

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Editor_Loading failed: {ex}");
            }
        }

        private async void Editor_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Editor.Languages.RegisterHoverProviderAsync("csharp", new EditorHoverProvider());
                await Editor.AddActionAsync(new TestAction());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Editor_Loaded registration failed: {ex}");
                if (Environment.GetEnvironmentVariable("MONACO_DIAGNOSTICS") == "1")
                {
                    Console.WriteLine($"EDITOR_LOADED_REGISTRATION_FAILED:{ex.GetType().Name}:{ex.Message}");
                }
            }

            // Ready for Display

            // Test harness: when MONACO_DIAGNOSTICS=1, set known property values
            // and register test commands/actions so CDP integration tests can verify
            // the C# bridge round-trip without any library modifications.
            // Guard: run only once to prevent duplicate registrations on re-load.
            if (Environment.GetEnvironmentVariable("MONACO_DIAGNOSTICS") != "1"
                || _testHarnessInitialized)
            {
                return;
            }

            _testHarnessInitialized = true;

            try
            {
                // Set known text and language from the C# side to prove the
                // DP -> SendScriptAsync -> JS path works end-to-end.
                Editor.Text = "// test-init-text";
                Editor.CodeLanguage = "javascript";
                Console.WriteLine("TEST_INIT_PROPS:text=// test-init-text,lang=javascript");

                // Register a test command (no keybinding) whose callback logs to stdout.
                // Declare ID before the closure so the lambda captures the variable.
                string? commandId = null;
                commandId = await Editor.AddCommandAsync(0, (args) =>
                {
                    Console.WriteLine($"TEST_CALLBACK:{commandId}:invoked");
                });
                commandId ??= "unknown";

                // Register a test action whose callback logs to stdout.
                const string testActionId = "testCdpAction";
                await Editor.AddActionAsync(new CdpTestAction(testActionId, () =>
                {
                    Console.WriteLine($"TEST_CALLBACK:Action{testActionId}:invoked");
                }));

                // Register a custom language via C# LanguagesHelper API.
                await Editor.Languages.RegisterAsync(new Monaco.Languages.ILanguageExtensionPoint
                {
                    Id = "test-csproj-lang"
                });
                Console.WriteLine("TEST_HARNESS_LANG:registered=test-csproj-lang");

                // Set markers via C# SetModelMarkersAsync to prove the C# -> JS path.
                await Editor.SetModelMarkersAsync("testHarness", [
                    new Monaco.Editor.MarkerData
                    {
                        StartLineNumber = 1, StartColumn = 1,
                        EndLineNumber = 1, EndColumn = 5,
                        Message = "harness-marker",
                        Severity = Monaco.MarkerSeverity.Warning,
                        Source = "testHarness"
                    }
                ]);
                Console.WriteLine("TEST_HARNESS_MARKERS:set=harness-marker");

                // Add a decoration via C# Decorations collection to prove the C# -> JS path.
                Editor.Decorations.Add(new Monaco.Editor.IModelDeltaDecoration(
                    new Monaco.Range(1, 1, 1, 5),
                    new Monaco.Editor.IModelDecorationOptions
                    {
                        InlineClassName = new Monaco.Helpers.CssInlineStyle
                        {
                            ForegroundColor = Microsoft.UI.Colors.Red
                        }
                    }));
                Console.WriteLine("TEST_HARNESS_DECORATIONS:added=1");

                // Switch theme via C# RequestedTheme to prove the DP -> changeTheme path.
                Editor.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Dark;
                Console.WriteLine("TEST_HARNESS_THEME:set=Dark");

                // Register on-demand test actions that tests can trigger from Playwright
                // to invoke C# APIs. This ensures tests drive mutations through the C# bridge
                // path rather than calling JS APIs directly.
                const string setMarkersActionId = "testSetMarkers";
                await Editor.AddActionAsync(new CdpTestAction(setMarkersActionId, () =>
                {
                    // Fire-and-forget: SetModelMarkersAsync goes through the C# bridge
                    // (SendScriptAsync -> JS monaco.editor.setModelMarkers). The stdout
                    // marker confirms the C# API was invoked; the test verifies Monaco state.
                    _ = Editor.SetModelMarkersAsync("cdpTest", [
                        new Monaco.Editor.MarkerData
                        {
                            StartLineNumber = 1, StartColumn = 1,
                            EndLineNumber = 1, EndColumn = 5,
                            Message = "on-demand-marker",
                            Severity = Monaco.MarkerSeverity.Error,
                            Source = "cdpTest"
                        }
                    ]).ContinueWith(_ =>
                        Console.WriteLine("TEST_HARNESS_MARKERS_ONDEMAND:set=on-demand-marker"));
                }));

                const string addDecorationActionId = "testAddDecoration";
                await Editor.AddActionAsync(new CdpTestAction(addDecorationActionId, () =>
                {
                    Editor.Decorations.Add(new Monaco.Editor.IModelDeltaDecoration(
                        new Monaco.Range(1, 1, 1, 7),
                        new Monaco.Editor.IModelDecorationOptions
                        {
                            InlineClassName = new Monaco.Helpers.CssInlineStyle
                            {
                                ForegroundColor = Microsoft.UI.Colors.Blue
                            }
                        }));
                    Console.WriteLine("TEST_HARNESS_DECORATIONS_ONDEMAND:added=1");
                }));

                Console.WriteLine($"TEST_HARNESS:commandId={commandId},actionId={testActionId}");

                // Final readiness marker: all async setup is complete.
                // Tests must wait for this before executing.
                Console.WriteLine("TEST_HARNESS_READY");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TEST_HARNESS_ERROR:{ex.Message}");
            }
        }

        /// <summary>
        /// One-time guard preventing duplicate test harness registration on re-load.
        /// </summary>
        private bool _testHarnessInitialized;

        public bool IsEditorOperational => Editor is { IsEditorLoaded: true };

        public async Task<string> CollectRuntimeProbeAsync(string stage)
        {
            if (Editor is null)
            {
                return BuildProbeErrorJson(stage, "editor-null");
            }

            try
            {
                var stageLiteral = ToJsonStringLiteral(stage);
                var probe = await Editor.InvokeScriptAsync($$"""
                    (() => {
                        try {
                            const context = typeof EditorContext !== 'undefined' && EditorContext.getEditorForElement
                                ? EditorContext.getEditorForElement(element)
                                : null;
                            const editor = context && context.editor
                                ? context.editor
                                : (window.monaco?.editor?.getEditors?.()[0] ?? null);
                            const model = editor && editor.getModel ? editor.getModel() : null;
                            const domNode = editor && editor.getDomNode ? editor.getDomNode() : null;
                            const rect = domNode && domNode.getBoundingClientRect ? domNode.getBoundingClientRect() : null;
                            return JSON.stringify({
                                stage: {{stageLiteral}},
                                hasEditor: !!editor,
                                hasModel: !!model,
                                isConnected: !!(domNode && domNode.isConnected),
                                width: rect ? rect.width : -1,
                                height: rect ? rect.height : -1,
                                valueLength: model && model.getValue ? model.getValue().length : -1
                            });
                        } catch (error) {
                            return JSON.stringify({ stage: {{stageLiteral}}, error: String(error) });
                        }
                    })()
                    """);

                return probe ?? BuildProbeErrorJson(stage, "null-probe");
            }
            catch (Exception ex)
            {
                return BuildProbeErrorJson(stage, ex.Message);
            }
        }

        public async Task<string> CollectFeatureProbeAsync(string stage)
        {
            if (Editor is null)
            {
                return BuildProbeErrorJson(stage, "editor-null");
            }

            try
            {
                var stageLiteral = ToJsonStringLiteral(stage);
                await Editor.InvokeScriptAsync("""
                    (() => {
                        globalThis.__unoHoverProbeResult = null;
                        try {
                            callParentEventAsync(
                                element,
                                "HoverProvidercsharp",
                                [JSON.stringify({ lineNumber: 1, column: 1 })]
                            )
                            .then(result => {
                                globalThis.__unoHoverProbeResult = result ?? "__null__";
                            })
                            .catch(error => {
                                globalThis.__unoHoverProbeResult = "__error__:" + String(error);
                            });
                        } catch (error) {
                            globalThis.__unoHoverProbeResult = "__error__:" + String(error);
                        }
                        return "started";
                    })()
                    """);

                string? lastProbe = null;
                for (var attempt = 0; attempt < 40; attempt++)
                {
                    string? probe = null;
                    try
                    {
                        probe = await Editor.InvokeScriptAsync($$"""
                            (() => {
                                try {
                                    const context = typeof EditorContext !== 'undefined' && EditorContext.getEditorForElement
                                        ? EditorContext.getEditorForElement(element)
                                        : null;
                                    const editor = context && context.editor ? context.editor : null;
                                    const hasTestAction = !!(
                                        editor &&
                                        (
                                            (editor.getAction && editor.getAction('meta-test-action')) ||
                                            (editor.getSupportedActions &&
                                                editor.getSupportedActions().some(action => action.id === 'meta-test-action'))
                                        )
                                    );
                                    const hoverProbeResult = globalThis.__unoHoverProbeResult;
                                    const isReady = !!(hasTestAction
                                        && typeof hoverProbeResult === 'string'
                                        && hoverProbeResult.length > 0
                                        && hoverProbeResult !== "__null__"
                                        && !hoverProbeResult.startsWith("__error__:"));
                                    return JSON.stringify({
                                        stage: {{stageLiteral}},
                                        hasTestAction,
                                        hoverProbeResult,
                                        isReady
                                    });
                                } catch (error) {
                                    return JSON.stringify({ stage: {{stageLiteral}}, error: String(error) });
                                }
                            })()
                            """);
                    }
                    catch
                    {
                        // Transient unload/reload can fail probe script execution.
                        // Keep polling until the editor stabilizes.
                    }

                    if (!string.IsNullOrEmpty(probe))
                    {
                        lastProbe = probe;
                        if (probe.Contains("\"isReady\":true", StringComparison.Ordinal))
                        {
                            return probe;
                        }
                    }

                    if (string.IsNullOrEmpty(probe)
                        || probe.Contains("\"hoverProbeResult\":null", StringComparison.Ordinal))
                    {
                        await Editor.InvokeScriptAsync("""
                            (() => {
                                try {
                                    callParentEventAsync(
                                        element,
                                        "HoverProvidercsharp",
                                        [JSON.stringify({ lineNumber: 1, column: 1 })]
                                    )
                                    .then(result => {
                                        globalThis.__unoHoverProbeResult = result ?? "__null__";
                                    })
                                    .catch(error => {
                                        globalThis.__unoHoverProbeResult = "__error__:" + String(error);
                                    });
                                } catch (error) {
                                    globalThis.__unoHoverProbeResult = "__error__:" + String(error);
                                }
                                return "started";
                            })()
                            """);
                    }

                    await Task.Delay(200);
                }

                return lastProbe ?? BuildProbeErrorJson(stage, "hover-probe-timeout");
            }
            catch (Exception ex)
            {
                return BuildProbeErrorJson(stage, ex.Message);
            }
        }

        private static string BuildProbeErrorJson(string stage, string error)
            => $"{{\"stage\":{ToJsonStringLiteral(stage)},\"error\":{ToJsonStringLiteral(error)}}}";

        private static string ToJsonStringLiteral(string value)
            => $"\"{value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)}\"";

        private void Editor_OpenLinkRequest(CodeEditor sender, OpenLinkRequestedEventArgs args)
        {
            if (this.AllowWeb.IsChecked == false)
            {
                args.Handled = true;
            }
        }

        private void ButtonSetText_Click(object sender, RoutedEventArgs e)
        {
            CodeContent = TextEditor.Text;
        }

        private async void ButtonRevealPositionInCenter_Click(object sender, RoutedEventArgs e)
        {
            await this.Editor.RevealPositionInCenterAsync(new Monaco.Position(10, 5));
        }

        private void ButtonHighlightRange_Click(object? sender, RoutedEventArgs? e)
        {
            Editor.Decorations.Add(
                new IModelDeltaDecoration(new Monaco.Range(3, 1, 3, 10), new IModelDecorationOptions()
                {
                    ClassName = CssLineDarkRed,
                    InlineClassName = CssInlineWhiteBold,
                    HoverMessage = new string[]
                    {
                        "This is a test message.",
                        "*YES*, **it is**."
                    }.ToMarkdownString(),
                    Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges
                }));
        }

        private async void ButtonHighlightLine_Click(object sender, RoutedEventArgs e)
        {
            Editor.Decorations.Add(
                new IModelDeltaDecoration(new Monaco.Range(4, 1, 4, 1), new IModelDecorationOptions()
                {
                    IsWholeLine = true,
                    ClassName = CssLineAliceBlue,
                    InlineClassName = CssInlineWhiteBold,
                    GlyphMarginClassName = CssGlyphError,
                    HoverMessage = (new string[]
                    {
                        "This is *another* \"test\" message about 'thing'."
                    }).ToMarkdownString(),
                    GlyphMarginHoverMessage = (new string[]
                    {
                        "This is some crazy \"Error\" here.",
                        "'Maybe'..."
                    }).ToMarkdownString()
                }));

            if (Editor.GetModel() is { } model)
            {
                Editor.Decorations.Add(
                    new IModelDeltaDecoration(new Monaco.Range(2, 1, 2, await model.GetLineLengthAsync(2)), new IModelDecorationOptions()
                    {
                        IsWholeLine = true,
                        InlineClassName = CssInlineStrikeThrough,
                        GlyphMarginClassName = CssGlyphWarning,
                        HoverMessage = (new string[]
                        {
                        "Deprecated"
                        }).ToMarkdownString()
                    }));
            }
        }

        private void ButtonClearHighlights_Click(object sender, RoutedEventArgs e)
        {
            this.Editor.Decorations.Clear();
        }

        // Note: Can't make this method async as otherwise handled won't be read for intercepts.
        private void Editor_KeyDown(CodeEditor sender, WebKeyEventArgs e)
        {
            Debug.WriteLine("KeyDown: " + e.KeyCode + " " + e.CtrlKey);

            if (e.KeyCode == 112) // F1
            {
                // If we wanted to disable the Command Palette (F1), we set handled to true here.
                //e.Handled = true;
            }
            else if (e.KeyCode == 13 && e.CtrlKey)
            {
                // You can now do this with a Command as well, see above.

                // Skip await, so we can read intercept value.
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, async () =>
                {
                    var md = new MessageDialog("You Hit Ctrl+Enter!");
                    WinRT.Interop.InitializeWithWindow.Initialize(md, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
                    await md.ShowAsync();

                    // Refocus on CodeEditor
                    Editor.Focus(FocusState.Programmatic);
                });

                // Intercept input so we don't add a newline.
                e.Handled = true;

                // We'll show that we can enable the F5 Command once we've performed Ctrl+Enter at least once.
                _myCondition?.Set(true);
            }
        }

        private void ButtonFolding_Click(object sender, RoutedEventArgs e)
        {
            options.Folding = !options.Folding ?? true;
        }

        private void ButtonMinimap_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Need to propagate the INotifyPropertyChanged from the Sub-Option Objects
            options.Minimap = new EditorMinimapOptions()
            {
                Enabled = !options.Minimap?.Enabled ?? false
            };
        }

        private void ButtonChangeLanguage_Click(object sender, RoutedEventArgs e)
        {
            Editor.CodeLanguage = (Editor.CodeLanguage == "csharp") ? "xml" : "csharp";
        }

        private async void ButtonSetMarker_Click(object sender, RoutedEventArgs e)
        {
            if (!(await Editor.GetModelMarkersAsync()).Any())
            {
                Editor.Markers.Add(
                    new MarkerData()
                    {
                        Code = "2344",
                        Message = "This is a \"Warning\" about 'that thing'.",
                        Severity = MarkerSeverity.Warning,
                        Source = "Origin",
                        StartLineNumber = 2,
                        StartColumn = 2,
                        EndLineNumber = 2,
                        EndColumn = 8
                    });

                Editor.Markers.Add(
                    new MarkerData()
                    {
                        Code = "2345",
                        Message = "This is an \"Error\" about 'that thing'.",
                        Severity = MarkerSeverity.Error,
                        Source = "Origin",
                        StartLineNumber = 3,
                        StartColumn = 5,
                        EndLineNumber = 3,
                        EndColumn = 15
                    });
            }
            else
            {
                //Editor.Markers.Clear();
                await Editor.SetModelMarkersAsync("CodeEditor", []);
            }
        }

        //// Example to show toggling visibility and impact on control.
        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Content.ToString() == "Hide")
                {
                    Editor.Visibility = Visibility.Collapsed;

                    btn.Content = "Show";
                }
                else
                {
                    Editor.Visibility = Visibility.Visible;

                    btn.Content = "Hide";
                }
            }
        }

        // TODO: this scenario needs more work.
        //// Example to show keeping a reference to the editor but removing from Visual Tree.
        private void DetachButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Content.ToString() == "Detach")
                {
                    RootGrid.Children.Remove(Editor);

                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    btn.Content = "Attach";
                }
                else
                {
                    RootGrid.Children.Add(Editor);

                    btn.Content = "Detach";
                }
            }
        }

        //// Example to show memory usage when deconstructing and reconstructing editor.
        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Content.ToString() == "Remove")
                {
                    _myCondition = null;
                    Editor.KeyDown -= Editor_KeyDown;

                    Editor.EditorLoaded -= Editor_Loaded;
                    Editor.EditorLoading -= Editor_Loading;
                    Editor.OpenLinkRequested -= Editor_OpenLinkRequest;
                    Editor.InternalException -= Editor_InternalException;

                    RootGrid.Children.Remove(Editor);
                    Editor.Dispose();
                    Editor = null;

                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    btn.Content = "Add";
                }
                else
                {
                    Editor = new CodeEditor()
                    {
                        TabIndex = 0,
                        HasGlyphMargin = true,
                        CodeLanguage = "csharp"
                    };

                    Editor.KeyDown += Editor_KeyDown;

                    Editor.EditorLoading += Editor_Loading;
                    Editor.EditorLoaded += Editor_Loaded;
                    Editor.OpenLinkRequested += Editor_OpenLinkRequest;
                    Editor.InternalException += Editor_InternalException;

                    Grid.SetColumn(Editor, 1);

                    RootGrid.Children.Add(Editor);

                    // TODO: My Condition?

                    btn.Content = "Remove";
                }
            }
        }

        private void ComboBoxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (e.AddedItems?.FirstOrDefault()?.ToString())
            {
                case "System":
                    RequestedTheme = ElementTheme.Default;
                    break;
                case "Light":
                    RequestedTheme = ElementTheme.Light;
                    break;
                case "Dark":
                    RequestedTheme = ElementTheme.Dark;
                    break;
            }

            // Tell Editor about Update.
            Editor.RequestedTheme = RequestedTheme;
        }

        //private async void LoadAndSet_Click(object sender, RoutedEventArgs e)
        //{
        //    // remember current pos
        //    var pos = await Editor.GetPositionAsync();

        //    Editor.Text = "Testing some new content here.\n\tIf you placed your cursor near the start of the text before you hit the button.\nIt should still be in the same spot.";

        //    await Editor.SetPositionAsync(pos);

        //    Editor.Focus(FocusState.Programmatic);
        //}

        private void ButtonSetSelectedText_Click(object sender, RoutedEventArgs e)
        {
            if (Editor is null || !Editor.IsEditorLoaded)
            {
                return;
            }

            Editor.SelectedText = "This is some Selected Text!";
        }

        private void ButtonSetReadonly_Click(object sender, RoutedEventArgs e)
        {
            Editor.ReadOnly = !Editor.ReadOnly;
        }

        private async void ButtonRunScript_Click(object sender, RoutedEventArgs e)
        {
            var result = await Editor.InvokeScriptAsync(@"function test(a, b) { return a + b; }; test(3, 4).toString()");
            Debug.WriteLine(result);
        }

        private void Editor_GotFocus(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Editor Got Focus");
        }

        private void Editor_LostFocus(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Editor Lost Focus");
        }
    }
}
