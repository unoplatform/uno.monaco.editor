using Collections.Generic;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Monaco.Editor;
using Monaco.Extensions;
using Monaco.Helpers;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Monaco
{
    /// <summary>
    /// UWP Windows Runtime Component wrapper for the Monaco CodeEditor
    /// https://microsoft.github.io/monaco-editor/
    /// </summary>
    [TemplatePart(Name = "RootBorder", Type = typeof(Border))]
    public sealed partial class CodeEditor : Control, INotifyPropertyChanged, IDisposable
    {
        private bool _initialized;
        private DispatcherQueue? _queue;

        private ICodeEditorPresenter? _view;

        private ModelHelper? _model;
        private CssStyleBroker? _cssBroker;

        // Queue for property changes that occur before initialization  
        // We use a priority queue to ensure certain properties (like CodeLanguage) are set before others (like Text)
        private readonly List<(int Priority, Func<Task> Action)> _propertyChangeQueue = new();
        private readonly object _queueLock = new();
        
        // Priority constants - lower numbers execute first
        internal const int PRIORITY_OPTIONS = 0;      // Options, CodeLanguage, ReadOnly, HasGlyphMargin
        internal const int PRIORITY_CONTENT = 10;     // Text, SelectedText
        internal const int PRIORITY_DECORATIONS = 20; // Decorations, Markers

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Template Property used during loading to prevent blank control visibility when it's still loading WebView.
        /// </summary>
        public bool IsEditorLoaded
        {
            get => (bool)GetValue(IsEditorLoadedProperty);
            private set => SetValue(IsEditorLoadedProperty, value);
        }

        public static DependencyProperty IsEditorLoadedProperty { get; } = DependencyProperty.Register(
            nameof(IsEditorLoaded),
            typeof(string),
            typeof(CodeEditor),
            new PropertyMetadata(false, OnIsEditorLoadedChanged));

        private static void OnIsEditorLoadedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        /// <summary>
        /// Construct a new Stand Alone Code Editor, assumes being constructed on UI Thread.
        /// </summary>
        public CodeEditor() : this(null) { }

        /// <summary>
        /// Construct a new IStandAloneCodeEditor.
        /// </summary>
        /// <param name="queue"><see cref="DispatcherQueue"/> for the UI Thread, if none pass assumes the current thread is the UI thread.</param>
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
            if (_view == null) return;
            
            if (sender is not StandaloneEditorConstructionOptions options) return;

            QueueOrExecutePropertyChange(async () =>
            {
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
            });
        }

        private void CodeEditor_SizeChanged(object sender, RoutedEventArgs e)
        {
            SizeChangedPartial();
        }

        partial void SizeChangedPartial();

        private void CodeEditor_Loaded(object sender, RoutedEventArgs e)
        {
#if __WASM__
            LoadedPartial();
#endif

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

            Debug.WriteLine($"CodeEditor_Loaded [{_model}] [{_view}] ({GetHashCode():x8})");

            // Do this the 2nd time around.
            if (_model == null && _view != null)
            {
                _model = new ModelHelper(this);

                Options.PropertyChanged -= Options_PropertyChanged;
                Options.PropertyChanged += Options_PropertyChanged;

                Decorations.VectorChanged -= Decorations_VectorChanged;
                Decorations.VectorChanged += Decorations_VectorChanged;
                Markers.VectorChanged -= Markers_VectorChanged;
                Markers.VectorChanged += Markers_VectorChanged;

                Debug.WriteLine("Setting initialized - true (non-WASM path)");
                // Don't set _initialized here for WASM - let ReplayQueuedPropertyChanges do it atomically
                // For non-WASM platforms, we may need to set it here since CodeEditorLoaded might not be called
#if !__WASM__
                _initialized = true;
#endif

                Unloaded -= CodeEditor_Unloaded;
                Unloaded += CodeEditor_Unloaded;

                if (Window.Current is not null)
                {
                    Window.Current.SizeChanged += OnWindowSizeChanged;
                }
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

            if (_view != null)
            {
                _view.NavigationStarting -= WebView_NavigationStarting;
                _view.NavigationCompleted -= WebView_NavigationCompleted;
                _view.NewWindowRequested -= WebView_NewWindowRequested;
                _view.Loaded -= WebView_DOMContentLoaded;
                Debug.WriteLine("Setting initialized - false");
                _initialized = false;
            }

            Decorations.VectorChanged -= Decorations_VectorChanged;
            Markers.VectorChanged -= Markers_VectorChanged;

            Options.PropertyChanged -= Options_PropertyChanged;

            if (_themeListener != null)
            {
                _themeListener.ThemeChanged -= ThemeListener_ThemeChanged;
            }
            _themeListener = null;

            UnregisterPropertyChangedCallback(RequestedThemeProperty, _themeToken);
            _keyboardListener = null;
            _model = null;
        }

        protected override void OnApplyTemplate()
        {
            Console.WriteLine("OnApplyTemplate()");

            if (_view != null)
            {
                _view.NavigationStarting -= WebView_NavigationStarting;
                _view.NavigationCompleted -= WebView_NavigationCompleted;
                _view.NewWindowRequested -= WebView_NewWindowRequested;
                _view.Loaded -= WebView_DOMContentLoaded;
                Debug.WriteLine("Setting initialized - false");
                _initialized = false;
            }

            _view = (ICodeEditorPresenter)GetTemplateChild("View");

            if (_view != null)
            {
                _view.ParentCodeEditor = this;

                _view.NavigationStarting -= WebView_NavigationStarting;
                _view.NavigationStarting += WebView_NavigationStarting;
                _view.NavigationCompleted += WebView_NavigationCompleted;
                _view.NewWindowRequested += WebView_NewWindowRequested;

                if (_view.IsLoaded)
                {
                    WebView_DOMContentLoaded();
                }
                else
                {
                    _view.Loaded += WebView_DOMContentLoaded;
                }

#if __WASM__
                //_view.Source = new System.Uri("ms-appx-web:///Monaco/CodeEditor/CodeEditor.html");
#else
                _view.Source = new System.Uri("ms-appx-web:///Monaco/CodeEditor/CodeEditor.html");
#endif
                //_view.Source = new System.Uri("file:///MonacoCodeEditor.html", UriKind.RelativeOrAbsolute);
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
#if DEBUG
                Debug.WriteLine("WARNING: Tried to call '" + script + "' before initialized.");
#endif
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
            // Check both _initialized flag AND IsEditorLoaded to ensure DOM is ready
            // This prevents calling JavaScript before Monaco editor is fully ready in the DOM
            if (_initialized && _view is not null && IsEditorLoaded)
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
#if DEBUG
                Debug.WriteLine($"WARNING: Tried to call '{method}' before editor fully loaded. _initialized={_initialized}, IsEditorLoaded={IsEditorLoaded}");
#endif
            }

            return default;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Queues a property change action to be executed after initialization, or executes immediately if already initialized.
        /// Priority only matters during the initial queue replay to ensure Monaco is configured correctly (language before content).
        /// After initialization, properties execute immediately (fire and forget).
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <param name="priority">Priority for queue ordering during replay (lower values execute first). Ignored after initialization.</param>
        private void QueueOrExecutePropertyChange(Func<Task> action, int priority = PRIORITY_CONTENT)
        {
            lock (_queueLock)
            {
                if (_initialized)
                {
                    // Already initialized, execute immediately (fire and forget)
                    // We don't await here as this is called from property change callbacks
                    // Exceptions are handled in ExecutePropertyChangeAsync to prevent unobserved task exceptions
                    _ = ExecutePropertyChangeAsync(action);
                }
                else
                {
                    // Not yet initialized, queue for later with priority
                    // Priority ensures correct initialization order (e.g., language before content)
                    _propertyChangeQueue.Add((priority, action));
                }
            }
        }

        /// <summary>
        /// Executes a property change action with proper error handling (fire and forget helper).
        /// </summary>
        private async Task ExecutePropertyChangeAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex) when (!IsCriticalException(ex))
            {
                // Exception filter preserves call stack for critical exceptions
                Debug.WriteLine($"Error executing property change: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines if the exception is critical and should not be caught.
        /// </summary>
        private static bool IsCriticalException(Exception ex)
        {
            return ex is OutOfMemoryException
                || ex is StackOverflowException
                || ex is AccessViolationException
                || ex is AppDomainUnloadedException
                || ex is ThreadAbortException;
        }

        /// <summary>
        /// Replays all queued property changes. Called after initialization is complete.
        /// This method sets _initialized to true atomically with copying the queue to prevent race conditions.
        /// Actions are replayed in priority order (lower priority values first) to ensure correct initialization sequence.
        /// </summary>
        private async Task ReplayQueuedPropertyChanges()
        {
            List<(int Priority, Func<Task> Action)> actionsToReplay;
            
            lock (_queueLock)
            {
                // Set initialized flag here to ensure atomicity with queue copy
                _initialized = true;

                if (_propertyChangeQueue.Count == 0)
                {
                    return;
                }

                // Copy and sort the queue by priority (lower values first)
                actionsToReplay = [.. _propertyChangeQueue.OrderBy(x => x.Priority)];
                _propertyChangeQueue.Clear();
            }

            // Execute all queued actions in priority order
            foreach (var (priority, action) in actionsToReplay)
            {
                try
                {
                    Debug.WriteLine($"Replaying property change with priority {priority}");
                    await action();
                }
                catch (Exception ex) when (!IsCriticalException(ex))
                {
                    // Exception filter preserves call stack for critical exceptions
                    Debug.WriteLine($"Error replaying property change: {ex.Message}");
                }
            }
        }

        public new void Dispose()
        {
            _cssBroker?.Dispose();
            _cssBroker = null;
            _parentAccessor?.Dispose();
            _parentAccessor = null;
        }
    }

    public static class UriHelper
    {
        private static readonly string UNO_BOOTSTRAP_APP_BASE = global::System.Environment.GetEnvironmentVariable(nameof(UNO_BOOTSTRAP_APP_BASE)) ?? "";
        private static readonly string UNO_BOOTSTRAP_WEBAPP_BASE_PATH = Environment.GetEnvironmentVariable(nameof(UNO_BOOTSTRAP_WEBAPP_BASE_PATH)) ?? "";

        public static string AbsoluteUriString(this System.Uri uri)
        {
            string target;
            if (uri.IsAbsoluteUri)
            {
#if __WASM__
                if (uri.Scheme == "file" || uri.Scheme == "ms-appx-web")
                {
                    // Local files are assumed as coming from the remoter server
                    target = UNO_BOOTSTRAP_APP_BASE == null ? uri.PathAndQuery : UNO_BOOTSTRAP_WEBAPP_BASE_PATH + UNO_BOOTSTRAP_APP_BASE + uri.PathAndQuery;
                }
                else
                {
                    target = uri.AbsoluteUri;
                }
#else
                target = uri.AbsoluteUri;
#endif
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
