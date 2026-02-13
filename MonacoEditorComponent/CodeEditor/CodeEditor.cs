using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Collections.Generic;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Monaco.Editor;
using Monaco.Extensions;
using Monaco.Helpers;

namespace Monaco
{
    /// <summary>
    /// Indicates the rendering backend used by the CodeEditor.
    /// </summary>
    public enum RenderingBackend
    {
        /// <summary>WebAssembly browser rendering via BrowserHtmlElement.</summary>
        Wasm,

        /// <summary>Desktop rendering via WebView2 (Skia).</summary>
        Desktop
    }

    /// <summary>
    /// Provides a cross-platform Uno Platform wrapper around the
    /// <see href="https://microsoft.github.io/monaco-editor/">Monaco Editor</see>.
    /// On WebAssembly the editor runs natively in the browser; on desktop (Skia) it is
    /// hosted inside a WebView2 control with a JSON-RPC bridge for interop.
    /// </summary>
    [TemplatePart(Name = "RootBorder", Type = typeof(Border))]
    public sealed partial class CodeEditor : Control, INotifyPropertyChanged, IDisposable
    {
        private bool _initialized;
        private bool _desktopBootstrapInFlight;
        private DispatcherQueue? _queue;

        private ICodeEditorPresenter? _view;

        private ModelHelper? _model;
        private CssStyleBroker? _cssBroker;

        /// <summary>
        /// Cancellation source for deferred unload teardown. When the control is
        /// unloaded, teardown is deferred behind a short delay. If the control is
        /// reloaded before the delay completes, the CTS is cancelled and teardown
        /// is skipped, preserving editor state across tab switches.
        /// </summary>
        private CancellationTokenSource? _unloadCts;

        // Subscription count diagnostics (gated behind MONACO_DIAGNOSTICS=1).
        private int _sizeChangedSubCount;
        private int _optionsSubCount;
        private int _decorationsSubCount;
        private int _markersSubCount;

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets a value indicating whether the Monaco editor has completed its initialization
        /// lifecycle and is ready to receive commands.
        /// </summary>
        /// <remarks>
        /// This property transitions to <see langword="true"/> after the editor fires
        /// <see cref="CodeEditor.EditorLoaded"/>. It can be used in XAML templates to control
        /// visibility and prevent displaying an empty WebView during loading.
        /// </remarks>
        public bool IsEditorLoaded
        {
            get => (bool)GetValue(IsEditorLoadedProperty);
            private set => SetValue(IsEditorLoadedProperty, value);
        }

        /// <summary>Identifies the <see cref="IsEditorLoaded"/> dependency property.</summary>
        public static DependencyProperty IsEditorLoadedProperty { get; } = DependencyProperty.Register(
            nameof(IsEditorLoaded),
            typeof(bool),
            typeof(CodeEditor),
            new PropertyMetadata(false, OnIsEditorLoadedChanged));

        private static void OnIsEditorLoadedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CodeEditor editor)
            {
                editor.UpdatePresenterVisibility();
            }
        }

        private void UpdatePresenterVisibility()
        {
            var isVisible = IsEditorLoaded;

            if (_view is DesktopCodeEditorPresenter desktopPresenter)
            {
                desktopPresenter.SetHostVisible(isVisible);
            }

            if (_view is UIElement presenterElement)
            {
                presenterElement.Opacity = isVisible ? 1d : 0d;
                presenterElement.IsHitTestVisible = isVisible;
            }
        }

        /// <summary>
        /// Gets the rendering backend used by the editor (Wasm or Desktop).
        /// </summary>
        public RenderingBackend RenderingBackend
        {
            get => (RenderingBackend)GetValue(RenderingBackendProperty);
            private set => SetValue(RenderingBackendProperty, value);
        }

        /// <summary>Identifies the <see cref="RenderingBackend"/> dependency property.</summary>
        public static DependencyProperty RenderingBackendProperty { get; } = DependencyProperty.Register(
            nameof(RenderingBackend),
            typeof(RenderingBackend),
            typeof(CodeEditor),
            new PropertyMetadata(OperatingSystem.IsBrowser() ? RenderingBackend.Wasm : RenderingBackend.Desktop));

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeEditor"/> class on the current UI thread.
        /// </summary>
        public CodeEditor() : this(null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeEditor"/> class with an explicit dispatcher.
        /// </summary>
        /// <param name="queue">
        /// The <see cref="DispatcherQueue"/> for the UI thread. When <see langword="null"/>, the
        /// current thread's dispatcher is used.
        /// </param>
        public CodeEditor(DispatcherQueue? queue)
        {
            _queue = queue ?? DispatcherQueue.GetForCurrentThread();

            DefaultStyleKey = typeof(CodeEditor);
            if (ReadLocalValue(OptionsProperty) == DependencyProperty.UnsetValue)
            {
                Options = new StandaloneEditorConstructionOptions
                {
                    // Set Pass-Thru Properties
                    GlyphMargin = HasGlyphMargin,
                    Language = CodeLanguage,
                    ReadOnly = ReadOnly,
                    AutomaticLayout = true
                };
            }

            // Initialize this here so property changed event will fire and register collection changed event.
            Decorations = new ObservableVector<IModelDeltaDecoration>();
            Markers = new ObservableVector<IMarkerData>();
            //_model = new ModelHelper(this);
#pragma warning disable CS0618 // Type or member is obsolete
            Languages = new LanguagesHelper(this);
#pragma warning restore CS0618 // Type or member is obsolete
            _cssBroker = new CssStyleBroker(this);

            Loaded += CodeEditor_Loaded;
            SizeChanged += CodeEditor_SizeChanged;
            Unloaded += CodeEditor_Unloaded;

            // <WebView
            //     HorizontalAlignment="Stretch"
            //     VerticalAlignment="Stretch"
            //_view = new WebView(WebViewExecutionMode.SeparateProcess)
            //{
            //    Margin = Padding,
            //    HorizontalAlignment = HorizontalAlignment.Stretch,
            //    VerticalAlignment = VerticalAlignment.Stretch,
            //    Visibility = IsEditorLoaded ? Visibility.Visible : Visibility.Collapsed
            //};

            //     Margin="{TemplateBinding Padding}"
            RegisterPropertyChangedCallback(PaddingProperty, (s, e) =>
            {
                // _view.Margin = Padding;
            });
        }

        private async void Options_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_initialized || _view == null) return;

            if (sender is not StandaloneEditorConstructionOptions options) return;

            switch (e.PropertyName)
            {
                case nameof(StandaloneEditorConstructionOptions.Language):
                    if (options.Language is not null)
                    {
                        await InvokeScriptAsync("updateLanguage", options.Language);
                        if (CodeLanguage != options.Language) CodeLanguage = options.Language;
                    }
                    break;
                case nameof(StandaloneEditorConstructionOptions.GlyphMargin):
                    if (HasGlyphMargin != options.GlyphMargin) options.GlyphMargin = HasGlyphMargin;
                    break;
                case nameof(StandaloneEditorConstructionOptions.ReadOnly):
                    if (ReadOnly != options.ReadOnly) options.ReadOnly = ReadOnly;
                    break;
            }
            await InvokeScriptAsync("updateOptions", options);
        }

        private void CodeEditor_SizeChanged(object sender, RoutedEventArgs e)
        {
            SizeChangedPartial();
        }

        partial void SizeChangedPartial();

        private void CodeEditor_Loaded(object sender, RoutedEventArgs e)
        {
            // If a deferred teardown is pending from a previous Unloaded event,
            // cancel it and skip teardown -- the control was reloaded (e.g., tab switch).
            if (_unloadCts is not null)
            {
                _unloadCts.Cancel();
                _unloadCts.Dispose();
                _unloadCts = null;

                // Soft reload: re-subscribe only Window.SizeChanged (the only handler
                // removed in the soft unload path). All other subscriptions survive.
                Unloaded -= CodeEditor_Unloaded;
                Unloaded += CodeEditor_Unloaded;

                if (Window.Current is not null)
                {
                    Window.Current.SizeChanged += OnWindowSizeChanged;
                    _sizeChangedSubCount++;
                }

                EmitSubscriptionDiagnostics("Loaded(soft)");
                return;
            }

            // Soft reload without pending CTS: the presenter is healthy, the
            // lifecycle is not Unloaded, and subscriptions are intact (_model != null).
            // This happens when OnApplyTemplate reused the existing presenter and
            // CodeEditor_Loaded fires afterward during a normal tab switch.
            // Skip full init — subscriptions and bridge objects are still alive.
            if (_view != null
                && _model != null
                && _lifecycleState != EditorLifecycleState.Unloaded
                && IsPresenterHealthy())
            {
                DesktopCodeEditorPresenter.DiagnosticLog(
                    $"CodeEditor_Loaded: soft reload (lifecycle={_lifecycleState}, presenter={_view.GetHashCode():x8})");

                Unloaded -= CodeEditor_Unloaded;
                Unloaded += CodeEditor_Unloaded;

                if (Window.Current is not null)
                {
                    Window.Current.SizeChanged += OnWindowSizeChanged;
                    _sizeChangedSubCount++;
                }

                EmitSubscriptionDiagnostics("Loaded(soft-reuse)");
                return;
            }

            if (OperatingSystem.IsBrowser())
            {
                LoadedPartial();
            }

            // Sync initial pass-thru properties
            if (ReadLocalValue(HasGlyphMarginProperty) == DependencyProperty.UnsetValue && Options.GlyphMargin.HasValue)
            {
                HasGlyphMargin = Options.GlyphMargin.Value;
            }

            if (ReadLocalValue(CodeLanguageProperty) == DependencyProperty.UnsetValue && Options.Language != null)
            {
                CodeLanguage = Options.Language;
            }

            if (ReadLocalValue(ReadOnlyProperty) == DependencyProperty.UnsetValue && Options.ReadOnly.HasValue)
            {
                ReadOnly = Options.ReadOnly.Value;
            }

            DesktopCodeEditorPresenter.DiagnosticLog($"CodeEditor_Loaded [{_model}] [{_view}] ({GetHashCode():x8})");

            // Do this the 2nd time around.
            if (_model == null && _view != null)
            {
                _model = new ModelHelper(this);

                Options.PropertyChanged -= Options_PropertyChanged;
                Options.PropertyChanged += Options_PropertyChanged;
                _optionsSubCount = 1;

                Decorations.VectorChanged -= Decorations_VectorChanged;
                Decorations.VectorChanged += Decorations_VectorChanged;
                _decorationsSubCount = 1;

                Markers.VectorChanged -= Markers_VectorChanged;
                Markers.VectorChanged += Markers_VectorChanged;
                _markersSubCount = 1;

                // Note: _initialized is NOT set here. It is set only when the
                // lifecycle reaches Loaded (in CodeEditorLoaded or WebView_NavigationCompleted)
                // to prevent premature script execution before Monaco is ready.

                Unloaded -= CodeEditor_Unloaded;
                Unloaded += CodeEditor_Unloaded;

                if (Window.Current is not null)
                {
                    Window.Current.SizeChanged += OnWindowSizeChanged;
                    _sizeChangedSubCount = 1;
                }

                EmitSubscriptionDiagnostics("Loaded(init)");
            }
        }

        private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            SizeChangedPartial();
        }

        partial void LoadedPartial();

        private void CodeEditor_Unloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= CodeEditor_Unloaded;

            // Note: Presenter event handlers (NavigationStarting, NavigationCompleted,
            // NewWindowRequested, Loaded) are NOT detached here. The presenter survives
            // across unload/load cycles and is only replaced in OnApplyTemplate().
            // Detaching here without reattaching in CodeEditor_Loaded would leave
            // the editor non-functional after reload.

            // Soft unload: only unsubscribe Window.SizeChanged (prevents accumulation).
            // Do NOT unsubscribe Options.PropertyChanged, Decorations.VectorChanged,
            // Markers.VectorChanged -- these must survive soft cycles.
            if (Window.Current is not null)
            {
                Window.Current.SizeChanged -= OnWindowSizeChanged;
                _sizeChangedSubCount--;
            }

            EmitSubscriptionDiagnostics("Unloaded");

            // Defer teardown behind a short delay. If CodeEditor_Loaded fires
            // before the delay completes, cancel and skip teardown entirely.
            _unloadCts?.Cancel();
            _unloadCts?.Dispose();
            _unloadCts = new CancellationTokenSource();
            var cts = _unloadCts;

            _ = DeferredTeardownAsync(cts.Token);
        }

        /// <summary>
        /// Performs a deferred hard teardown after a short delay. If the control is
        /// reloaded before the delay completes, the cancellation token is triggered
        /// and teardown is skipped.
        /// </summary>
        private async Task DeferredTeardownAsync(CancellationToken ct)
        {
            const int deferredTeardownDelayMs = 1200;
            try
            {
                await Task.Delay(deferredTeardownDelayMs, ct);
            }
            catch (OperationCanceledException)
            {
                // Control was reloaded -- skip teardown.
                DesktopCodeEditorPresenter.DiagnosticLog("DeferredTeardown: cancelled (control reloaded)");
                return;
            }

            // Race guard: verify the control is still unloaded before tearing down.
            // If CodeEditor_Loaded already fired (but did not cancel the CTS due to
            // a timing race), skip teardown to avoid destroying a re-initializing presenter.
            if (IsLoaded)
            {
                DesktopCodeEditorPresenter.DiagnosticLog("DeferredTeardown: control is loaded, skipping teardown");
                _unloadCts?.Dispose();
                _unloadCts = null;
                return;
            }

            var hasHealthyDesktopPresenter = _view is DesktopCodeEditorPresenter desktopPresenter
                && desktopPresenter.IsCoreWebView2Initialized;
            if (ShouldPreserveDesktopPresenterOnDeferredUnload(IsLoaded, hasHealthyDesktopPresenter))
            {
                // Preserve healthy desktop presenters across temporary unloads (tab switches).
                // Recreating WebView2/Monaco on every switch causes visible flicker and focus churn.
                DesktopCodeEditorPresenter.DiagnosticLog("DeferredTeardown: preserving healthy desktop presenter across unload");
                _unloadCts?.Dispose();
                _unloadCts = null;
                return;
            }

            // Hard teardown: control was not reloaded within the grace period.
            DesktopCodeEditorPresenter.DiagnosticLog("DeferredTeardown: executing hard teardown");

            // Clear _unloadCts BEFORE teardown so that a subsequent CodeEditor_Loaded
            // does not mistakenly enter the soft-reload early-return path.
            _unloadCts?.Dispose();
            _unloadCts = null;

            _initialized = false;

            Decorations.VectorChanged -= Decorations_VectorChanged;
            _decorationsSubCount--;
            Markers.VectorChanged -= Markers_VectorChanged;
            _markersSubCount--;
            Options.PropertyChanged -= Options_PropertyChanged;
            _optionsSubCount--;

            TeardownWebObjects();
            _model = null;
        }

        internal static bool ShouldPreserveDesktopPresenterOnDeferredUnload(
            bool isLoaded,
            bool hasHealthyDesktopPresenter)
            => !isLoaded && hasHealthyDesktopPresenter;

        /// <summary>
        /// Returns true when the existing desktop presenter is healthy and can be
        /// reused across unload/load cycles (e.g., tab switches). A healthy presenter
        /// has CoreWebView2 initialized and is not disposed.
        /// WASM presenters are always considered non-reusable (they are lightweight
        /// and stateless).
        /// </summary>
        private bool IsPresenterHealthy()
        {
            if (_view is DesktopCodeEditorPresenter desktop)
            {
                return desktop.IsCoreWebView2Initialized;
            }

            return false;
        }

        /// <inheritdoc />
        protected override void OnApplyTemplate()
        {
            DesktopCodeEditorPresenter.DiagnosticLog(
                $"OnApplyTemplate() _view={_view?.GetHashCode():x8} healthy={IsPresenterHealthy()} lifecycle={_lifecycleState}");

            // Cancel any pending deferred teardown — we are being re-applied.
            _unloadCts?.Cancel();
            _unloadCts?.Dispose();
            _unloadCts = null;

            var viewHost = GetTemplateChild("View") as ContentPresenter
                ?? throw new InvalidOperationException(
                    "CodeEditor template must contain a ContentPresenter named 'View'. " +
                    "Ensure Generic.xaml defines <ContentPresenter x:Name=\"View\" />.");

            // ---- Reuse path: presenter is healthy, just reassign to ContentPresenter ----
            if (_view != null && IsPresenterHealthy())
            {
                DesktopCodeEditorPresenter.DiagnosticLog(
                    $"OnApplyTemplate: reusing presenter {_view.GetHashCode():x8} lifecycle={_lifecycleState}");

                // The ContentPresenter may have lost its Content reference during the
                // unload/re-template cycle. Re-assign the same instance — this does
                // not trigger a new WebView2 creation.
                if (viewHost.Content != _view)
                {
                    viewHost.Content = _view;
                }
                UpdatePresenterVisibility();

                // If hard teardown already ran (lifecycle is Unloaded), the bridge
                // objects were torn down. Restore them by re-running InitialiseWebObjects
                // which will set up the JsonRpc bridge on the existing presenter.
                if (_lifecycleState == EditorLifecycleState.Unloaded && _initializedPresenter == null)
                {
                    DesktopCodeEditorPresenter.DiagnosticLog(
                        "OnApplyTemplate: restoring bridge after hard teardown");

                    if (!InitialiseWebObjects())
                    {
                        // Bridge restoration failed — fall through to full teardown/create path
                        // by NOT returning. The reuse attempt failed.
                        DesktopCodeEditorPresenter.DiagnosticLog(
                            "OnApplyTemplate: bridge restoration failed, will recreate presenter");
                    }
                    else
                    {
                        // Bridge restored successfully. The presenter's WebView2 is already
                        // at editor.html, so NavigationCompleted won't fire again.
                        // Manually re-bootstrap Monaco by invoking createMonacoEditor.
                        // InitialiseWebObjects transitioned lifecycle to Loading, so
                        // CodeEditorLoaded (the "Loaded" callback) will handle the rest.
                        RebootstrapMonacoAsync();
                        UpdatePresenterVisibility();

                        base.OnApplyTemplate();
                        return;
                    }
                }
                else
                {
                    UpdatePresenterVisibility();
                    base.OnApplyTemplate();
                    return;
                }
            }

            // ---- Full teardown path: first init or presenter is unhealthy ----
            if (_view != null)
            {
                DesktopCodeEditorPresenter.DiagnosticLog(
                    $"OnApplyTemplate: tearing down unhealthy presenter {_view.GetHashCode():x8}");

                _view.NavigationStarting -= WebView_NavigationStarting;
                _view.NavigationCompleted -= WebView_NavigationCompleted;
                _view.NewWindowRequested -= WebView_NewWindowRequested;
                _view.Loaded -= WebView_DOMContentLoaded;

                // Hard teardown: template replacement creates a new presenter.
                _initialized = false;

                if (Window.Current is not null)
                {
                    Window.Current.SizeChanged -= OnWindowSizeChanged;
                    _sizeChangedSubCount = 0;
                }

                Decorations.VectorChanged -= Decorations_VectorChanged;
                _decorationsSubCount = 0;
                Markers.VectorChanged -= Markers_VectorChanged;
                _markersSubCount = 0;
                Options.PropertyChanged -= Options_PropertyChanged;
                _optionsSubCount = 0;

                TeardownWebObjects();
                _model = null;
            }

            // Create the correct presenter at runtime via OperatingSystem.IsBrowser()
            ICodeEditorPresenter presenter;
            if (OperatingSystem.IsBrowser())
            {
                presenter = new WasmCodeEditorPresenter();
            }
            else
            {
                presenter = new DesktopCodeEditorPresenter();
            }

            viewHost.Content = presenter;
            _view = presenter;
            _view.ParentCodeEditor = this;
            UpdatePresenterVisibility();

            _view.NavigationStarting -= WebView_NavigationStarting;
            _view.NavigationStarting += WebView_NavigationStarting;
            _view.NavigationCompleted += WebView_NavigationCompleted;
            _view.NewWindowRequested += WebView_NewWindowRequested;

            if (_view.IsLoaded)
            {
                WebView_DOMContentLoaded(_view, new());
            }
            else
            {
                _view.Loaded += WebView_DOMContentLoaded;
            }

            base.OnApplyTemplate();
        }

        internal async Task SendScriptAsync(string script,
            [CallerMemberName] string? member = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            await SendScriptAsync<object>(script, member, file, line);
        }

        internal async Task<T?> SendScriptAsync<T>(string script,
            [CallerMemberName] string? member = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            if (_initialized && _view is not null)
            {
                try
                {
                    return await _view.RunScriptAsync<T>(script, member, file, line);
                }
                catch (Exception e)
                {
                    InternalException?.Invoke(this, e);
                }
            }
            else
            {
                DesktopCodeEditorPresenter.DiagnosticLog("WARNING: Tried to call '" + script + "' before initialized.");
            }

            return default;
        }

        internal async Task InvokeScriptAsync(
            string method,
            object? arg,
            bool serialize = true,
            [CallerMemberName] string? member = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            await InvokeScriptAsync<object>(method, [arg], serialize, member, file, line);
        }

        internal async Task InvokeScriptAsync(
            string method,
            object[] args,
            bool serialize = true,
            [CallerMemberName] string? member = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            await InvokeScriptAsync<object>(method, args, serialize, member, file, line);
        }

        internal async Task<T?> InvokeScriptAsync<T>(
            string method,
            object arg,
            bool serialize = true,
            [CallerMemberName] string? member = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            return await InvokeScriptAsync<T>(method, [arg], serialize, member, file, line);
        }

        internal async Task<T?> InvokeScriptAsync<T>(
            string method,
            object?[] args,
            bool serialize = true,
            [CallerMemberName] string? member = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            if (_initialized && _view is not null)
            {
                try
                {
                    return await _view.InvokeScriptAsync<T>(method, args, serialize, member, file, line);
                }
                catch (Exception e)
                {
                    InternalException?.Invoke(this, e);
                }
            }
            else
            {
                DesktopCodeEditorPresenter.DiagnosticLog("WARNING: Tried to call " + method + " before initialized.");
            }

            return default;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Emits subscription count diagnostics to stdout when <c>MONACO_DIAGNOSTICS=1</c>.
        /// Format: <c>DIAG_SUB_COUNTS:{sizeChanged},{options},{decorations},{markers}</c>
        /// </summary>
        private void EmitSubscriptionDiagnostics(string context)
        {
            DesktopCodeEditorPresenter.DiagnosticLog(
                $"DIAG_SUB_COUNTS:{_sizeChangedSubCount},{_optionsSubCount},{_decorationsSubCount},{_markersSubCount} [{context}]");
        }

        /// <summary>
        /// Releases managed resources held by the editor, including the CSS style broker
        /// and the parent accessor bridge.
        /// </summary>
        public new void Dispose()
        {
            // Cancel any pending deferred teardown.
            _unloadCts?.Cancel();
            _unloadCts?.Dispose();
            _unloadCts = null;

            // Hard teardown: unsubscribe ALL handlers.
            _initialized = false;

            if (Window.Current is not null)
            {
                Window.Current.SizeChanged -= OnWindowSizeChanged;
                _sizeChangedSubCount = 0;
            }

            Decorations.VectorChanged -= Decorations_VectorChanged;
            _decorationsSubCount = 0;
            Markers.VectorChanged -= Markers_VectorChanged;
            _markersSubCount = 0;
            Options.PropertyChanged -= Options_PropertyChanged;
            _optionsSubCount = 0;

            TeardownWebObjects();
            _model = null;

            _cssBroker?.Dispose();
            _cssBroker = null;
            if (_parentAccessor is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _parentAccessor = null;
        }
    }

    /// <summary>
    /// Provides extension methods for <see cref="System.Uri"/> to resolve absolute URI strings
    /// in the Uno Platform WASM bootstrap environment.
    /// </summary>
    public static class UriHelper
    {
        private static readonly string UNO_BOOTSTRAP_APP_BASE = global::System.Environment.GetEnvironmentVariable(nameof(UNO_BOOTSTRAP_APP_BASE)) ?? "";
        private static readonly string UNO_BOOTSTRAP_WEBAPP_BASE_PATH = Environment.GetEnvironmentVariable(nameof(UNO_BOOTSTRAP_WEBAPP_BASE_PATH)) ?? "";

        /// <summary>
        /// Returns the absolute URI string resolved against the Uno WASM bootstrap base paths.
        /// </summary>
        /// <param name="uri">The URI to resolve.</param>
        /// <returns>The resolved absolute URI string suitable for use in the WASM host.</returns>
        public static string AbsoluteUriString(this System.Uri uri)
        {
            string target;
            if (uri.IsAbsoluteUri)
            {
                if (OperatingSystem.IsBrowser() && (uri.Scheme == "file" || uri.Scheme == "ms-appx-web"))
                {
                    // Local files are assumed as coming from the remote server
                    target = UNO_BOOTSTRAP_APP_BASE == null ? uri.PathAndQuery : UNO_BOOTSTRAP_WEBAPP_BASE_PATH + UNO_BOOTSTRAP_APP_BASE + uri.PathAndQuery;
                }
                else
                {
                    target = uri.AbsoluteUri;
                }
            }
            else
            {
                target = UNO_BOOTSTRAP_APP_BASE == null
                    ? uri.OriginalString
                    : UNO_BOOTSTRAP_WEBAPP_BASE_PATH + UNO_BOOTSTRAP_APP_BASE + "/" + uri.OriginalString;
            }
            return target;
        }
    }
}
