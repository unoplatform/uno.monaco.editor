import * as monaco from 'monaco-editor';
import { ParentAccessor } from './Monaco.Helpers.ParentAccessor';
import { isDesktopHost, getConnection, sendRequestWithTimeout, retainConnection } from './bridge/jsonRpcBridge';
import { Disposable } from 'vscode-jsonrpc/browser';
import { EditorContext, changeTheme } from './otherScriptsToBeOrganized';
import {
    attachMultiDiffObservers,
    createMultiDiffWidget,
    disposeMultiDiffState,
    layoutMultiDiffEditor,
    updateMultiDiffFiles,
    MultiDiffFile
} from './multiDiffEditor';

type MethodWithReturnId = (parameter: string) => void;
type NumberCallback = (parameter: any) => void;

/**
 * Module-level flag: true when running in a WebView2/WKWebView host (desktop),
 * false when running under Uno WASM Bootstrap (browser).
 */
const _isDesktop: boolean = isDesktopHost();

class DebugLoggerImpl {
    private _managedOwner: any;

    constructor(managedOwner: any) {
        this._managedOwner = managedOwner;
    }

    public static async setup() {
    }
}

class KeyboardListenerImpl {
    private _managedOwner: any;

    constructor(managedOwner: any) {
        this._managedOwner = managedOwner;
    }

    public static async setup() {
    }
}

class ThemeListener {
    private _managedOwner: any;
    private static _managedGetCurrentThemeName: (managedOwner: any) => string;
    private static _managedGetIsHighContrast: (managedOwner: any) => boolean;

    constructor(managedOwner: any) {
        this._managedOwner = managedOwner;
    }

    public static async setup() {
        if (_isDesktop) {
            // No JSExport setup needed on desktop -- JSON-RPC bridge handles theme queries
            return;
        }

        let anyModule = (<any>window).Module;

        if (anyModule.getAssemblyExports !== undefined) {
            const browserExports = await anyModule.getAssemblyExports("MonacoEditorComponent");

            ThemeListener._managedGetCurrentThemeName = browserExports.Monaco.Helpers.ThemeListener.ManagedGetCurrentThemeName;
            ThemeListener._managedGetIsHighContrast = browserExports.Monaco.Helpers.ThemeListener.ManagedGetIsHighContrast;
        }
    }

    public getIsHighContrast(): boolean {
        if (_isDesktop) {
            throw new Error('ThemeListener.getIsHighContrast is not available on desktop. Use getIsHighContrastAsync instead.');
        }
        return ThemeListener._managedGetIsHighContrast(this._managedOwner);
    }

    public async getIsHighContrastAsync(): Promise<boolean> {
        if (_isDesktop) {
            const result = await sendRequestWithTimeout<string>(
                getConnection(), 'theme/getProperty', { name: 'isHighContrast' }
            );
            return result === 'true' || result === 'True';
        }
        return ThemeListener._managedGetIsHighContrast(this._managedOwner);
    }

    public getCurrentThemeName(): string {
        if (_isDesktop) {
            throw new Error('ThemeListener.getCurrentThemeName is not available on desktop. Use getCurrentThemeNameAsync instead.');
        }
        return ThemeListener._managedGetCurrentThemeName(this._managedOwner);
    }

    public async getCurrentThemeNameAsync(): Promise<string> {
        if (_isDesktop) {
            return await sendRequestWithTimeout<string>(
                getConnection(), 'theme/getProperty', { name: 'currentThemeName' }
            );
        }
        return ThemeListener._managedGetCurrentThemeName(this._managedOwner);
    }
}

/**
 * Registers C#->JS JSON-RPC handlers on the connection.
 * Only called on desktop. These handlers allow the C# host to invoke
 * editor operations via JSON-RPC instead of InvokeScriptAsync.
 * Returns an array of Disposables for deterministic cleanup.
 */
function registerDesktopHandlers(editorContext: EditorContext): Disposable[] {
    const connection = getConnection();
    const disposables: Disposable[] = [];

    // editor/getValue -- returns the current editor text.
    // Optional-chained because a multi-file diff context has no single editor: its per-file
    // editors are pooled and recycled, so there is nothing stable to read from.
    disposables.push(
        connection.onRequest('editor/getValue', () => {
            return editorContext.editor?.getValue() ?? '';
        })
    );

    // editor/updateOptions -- push updated editor options to Monaco
    disposables.push(
        connection.onNotification('editor/updateOptions', (params: { options: any }) => {
            if (params && params.options && typeof params.options === 'object') {
                editorContext.editor?.updateOptions(params.options);
            }
        })
    );

    // editor/lifecycleUpdate -- writes lifecycle counts to document.body.dataset for Playwright testability (Task 8)
    disposables.push(
        connection.onNotification('editor/lifecycleUpdate', (params: { loading: number, loaded: number }) => {
            document.body.dataset.lifecycleLoading = String(params.loading);
            document.body.dataset.lifecycleLoaded = String(params.loaded);
        })
    );

    return disposables;
}

function cleanupEditorRuntimeState(editorContext: EditorContext): void {
    const resizeObserver = (editorContext as any)._resizeObserver as ResizeObserver | undefined;
    if (resizeObserver) {
        resizeObserver.disconnect();
        (editorContext as any)._resizeObserver = undefined;
    }

    const disposables = (editorContext as any)._rpcHandlerDisposables as Disposable[] | undefined;
    if (disposables) {
        for (const d of disposables) {
            d.dispose();
        }
        (editorContext as any)._rpcHandlerDisposables = undefined;
    }

    // Dispose the diff widget, not editorContext.editor: on a diff context that field is
    // the *modified sub-editor*, and disposing it would leave the widget leaked. The two
    // models below were created explicitly by initializeMonacoDiffEditor and are not owned
    // by the widget, so they have to go too. monaco.editor.create() does own the model it
    // builds from `value`, so the plain path must not dispose it here.
    const multiDiff = editorContext.multiDiff;
    if (multiDiff) {
        disposeMultiDiffState(multiDiff);
        editorContext.multiDiff = undefined;
        (editorContext as any)._layoutTarget = undefined;
        return;
    }

    const diffEditor = editorContext.diffEditor;
    if (diffEditor) {
        diffEditor.dispose();
        editorContext.originalModel?.dispose();
        editorContext.model?.dispose();
        editorContext.diffEditor = undefined;
        editorContext.originalModel = undefined;
    } else if (editorContext.editor) {
        editorContext.editor.dispose();
    }
}

/**
 * Initial state pushed from C# on desktop to eliminate async RPC round-trips.
 * When provided, JS uses these values directly instead of calling back to C#.
 */
interface InitialState {
    requestedTheme: number;
    themeName: string;
    isHighContrast: boolean;
    text: string;
    language: string;
    readOnly: boolean;
    /** Diff editor only: the original (left-hand) document. */
    originalText?: string;
    /** Diff editor only: language of the original document; falls back to `language`. */
    originalLanguage?: string;
    /** Diff editor only: IDiffEditorOptions to apply at construction. */
    diffOptions?: any;
    /** Multi-file diff only: the per-file documents to render. */
    files?: MultiDiffFile[];
}

/**
 * Build the construction options for monaco.editor.create() from the pushed initial state.
 * Returns an empty object on WASM, where no payload is pushed and properties instead
 * arrive afterwards via ApplyInitialPropertyValues.
 */
const resolveInitialOptions = (initialState?: InitialState): any => {
    if (initialState) {
        console.log(`[resolveInitialOptions] Using pushed initial state: theme=${initialState.themeName}, lang=${initialState.language}`);

        let monacoTheme = 'vs';
        if (initialState.isHighContrast) {
            monacoTheme = 'hc-black';
        } else if (initialState.themeName === 'Dark') {
            monacoTheme = 'vs-dark';
        }

        return {
            theme: monacoTheme,
            language: initialState.language || 'plaintext',
            readOnly: initialState.readOnly || false,
            value: initialState.text || '',
        };
    }

    if (_isDesktop) {
        // Desktop fallback when the initial state payload is missing/invalid:
        // avoid async bridge round-trips during init and choose a best-effort theme.
        const prefersDark = typeof window.matchMedia === 'function'
            && window.matchMedia('(prefers-color-scheme: dark)').matches;
        return {
            theme: prefersDark ? 'vs-dark' : 'vs',
            language: 'plaintext',
            readOnly: false,
            value: '',
        };
    }

    return {};
};

/**
 * Same, for createDiffEditor(). `value` and `language` are dropped because they belong to
 * IStandaloneEditorConstructionOptions -- a diff editor takes its content from the two
 * models instead -- and the caller's IDiffEditorOptions are layered on top.
 */
const resolveInitialDiffOptions = (initialState?: InitialState): any => {
    const base = resolveInitialOptions(initialState);
    const opt: any = {};

    if (base.theme !== undefined) {
        opt.theme = base.theme;
    }
    if (base.readOnly !== undefined) {
        opt.readOnly = base.readOnly;
    }
    if (initialState && initialState.diffOptions && typeof initialState.diffOptions === 'object') {
        Object.assign(opt, initialState.diffOptions);
    }

    return opt;
};

/**
 * Wire everything that is identical for both editor flavors: the bridge helpers, the
 * content and selection listeners, theme initialization, the resize observer, the desktop
 * RPC handlers, and the closing "Loaded" handshake.
 *
 * @param layoutTarget the object whose layout() is called on resize -- the diff widget for
 *   a diff editor, the editor itself otherwise.
 * @param textPropertyName the C# dependency property the content listener writes back to:
 *   "Text" on CodeEditor, "ModifiedText" on DiffCodeEditor. Both bridge implementations
 *   resolve the property by name via reflection, so this parameter is the whole mechanism
 *   that lets the two controls avoid sharing one overloaded Text property.
 */
const attachEditorRuntime = async (
    managedOwner: any,
    element: any,
    editorContext: EditorContext,
    layoutTarget: { layout(): void },
    textPropertyName: string | null,
    initialState?: InitialState) => {

    (<any>editorContext).Debug = new DebugLoggerImpl(managedOwner);
    (<any>editorContext).Keyboard = new KeyboardListenerImpl(managedOwner);
    (<any>editorContext).Accessor = new ParentAccessor(managedOwner);
    (<any>editorContext).Theme = new ThemeListener(managedOwner);

    // The content and selection listeners below are the single-document surface. A multi-file
    // diff passes no textPropertyName and leaves editor/model unset -- its per-file editors are
    // pooled and recycled, so there is no stable one to track, and the write-back channel is a
    // single flat property name that cannot address N files. That control reports through
    // callActionWithParameters instead; see attachMultiDiffObservers.
    if (textPropertyName && editorContext.model) {
        // Listen for Content Changes
        editorContext.model.onDidChangeContent((event) => {
            editorContext.Accessor.setValue(textPropertyName, stringifyForMarshalling(editorContext.model.getValue()));
        });
    }

    if (editorContext.editor && editorContext.model) {
        // Listen for Selection Changes
        editorContext.editor.onDidChangeCursorSelection((event) => {
            if (!editorContext.modifingSelection) {
                editorContext.Accessor.setValue("SelectedText", stringifyForMarshalling(editorContext.model.getValueInRange(event.selection)));
                editorContext.Accessor.setValueWithType("SelectedRange", stringifyForMarshalling(JSON.stringify(event.selection)), "Selection");
            }
        });
    }

    // Apply theme:
    // - Desktop with initial state: theme already applied in the construction options.
    // - Desktop without initial state: use local fallback theme (already applied in options).
    // - WASM without initial state: use existing async property path.
    if (!initialState) {
        if (_isDesktop) {
            console.warn('[attachEditorRuntime] Missing pushed initial state on desktop; using local fallback theme');
        }
        else {
        // Set theme -- async on desktop (JSON-RPC with timeout), sync on WASM (JSExport)
        // Wrapped in try-catch so editor init completes even if theme RPC stalls (CI cold-start).
        try {
            const themeInitStart = performance.now();

            const t0 = performance.now();
            let theme: any = await editorContext.Accessor.getJsonValueAsync("RequestedTheme");
            const t1 = performance.now();
            if (_isDesktop) {
                console.log(`[attachEditorRuntime] getJsonValueAsync("RequestedTheme"): ${(t1 - t0).toFixed(1)}ms, result=${theme}`);
            }

            theme = {
                "0": "Default",
                "1": "Light",
                "2": "Dark"
            }[theme];

            if (theme == "Default") {
                const t2 = performance.now();
                theme = await (editorContext as any).Theme.getCurrentThemeNameAsync();
                const t3 = performance.now();
                if (_isDesktop) {
                    console.log(`[attachEditorRuntime] getCurrentThemeNameAsync: ${(t3 - t2).toFixed(1)}ms, result=${theme}`);
                }
            }

            const t4 = performance.now();
            const isHighContrast = await (editorContext as any).Theme.getIsHighContrastAsync();
            const t5 = performance.now();
            if (_isDesktop) {
                console.log(`[attachEditorRuntime] getIsHighContrastAsync: ${(t5 - t4).toFixed(1)}ms, result=${isHighContrast}`);
            }

            changeTheme(element, theme, isHighContrast as any);

            const themeInitEnd = performance.now();
            const cumulativeMs = themeInitEnd - themeInitStart;
            if (_isDesktop) {
                console.log(`[attachEditorRuntime] Theme init cumulative: ${cumulativeMs.toFixed(1)}ms` +
                    (cumulativeMs > 16 ? ' (EXCEEDS 16ms frame budget)' : ''));
            }
        } catch (err) {
            console.warn('[attachEditorRuntime] Theme initialization failed, using defaults:', err);
            changeTheme(element, 'Light', 'false');
        }
        }
    } else {
        console.log(`[attachEditorRuntime] Skipped async theme init -- using pushed initial state`);
    }

    // Track parent element size changes via ResizeObserver for deterministic cleanup.
    // This replaces the old window "resize" listener that fired on every window resize
    // even when the editor's container didn't change size.
    const resizeObserver = new ResizeObserver(() => { layoutTarget.layout(); });
    resizeObserver.observe(element);
    (editorContext as any)._resizeObserver = resizeObserver;
    // layoutEditor() prefers this over diffEditor/editor, so an explicit layout request reaches
    // the same target the ResizeObserver drives -- the only handle a multi-file diff has.
    (editorContext as any)._layoutTarget = layoutTarget;

    // Disable WebView Scrollbar so Monaco Scrollbar can do heavy lifting
    document.body.style.overflow = 'hidden';

    // Register C#->JS JSON-RPC handlers on desktop; track disposables for cleanup
    if (_isDesktop) {
        if (!(editorContext as any)._connectionRetained) {
            retainConnection();
            (editorContext as any)._connectionRetained = true;
        }
        const handlerDisposables = registerDesktopHandlers(editorContext);
        // Store disposables on context for deterministic teardown
        (editorContext as any)._rpcHandlerDisposables = handlerDisposables;
    }

    // Callback to Parent that we're loaded
    editorContext.Accessor.callAction("Loaded");
};

/**
 * Initialize a plain Monaco editor instance.
 * On desktop with initialState provided, theme/text/language are applied synchronously
 * from the pushed values -- no async RPC round-trips needed.
 * On WASM (or desktop without initialState), property reads use the existing paths.
 */
export const initializeMonacoEditor = async (managedOwner: any, element: any, initialState?: InitialState) => {
    // Re-init guard: when createMonacoEditor is invoked repeatedly for the same element,
    // tear down previous editor/runtime hooks first to avoid duplicated handlers and
    // leaked editor instances during async lifecycle races.
    const existingContext = EditorContext.tryGetEditorForElement(element);
    if (existingContext?.editor) {
        cleanupEditorRuntimeState(existingContext);
    }

    const editor = monaco.editor.create(element, resolveInitialOptions(initialState));
    var editorContext = EditorContext.registerEditorForElement(element, editor);
    editorContext.model = editor.getModel()!;

    await attachEditorRuntime(managedOwner, element, editorContext, editor, "Text", initialState);
};

/**
 * Initialize a Monaco diff editor instance, over the same element and the same lifecycle
 * handshake the plain editor uses.
 */
export const initializeMonacoDiffEditor = async (managedOwner: any, element: any, initialState?: InitialState) => {
    const existingContext = EditorContext.tryGetEditorForElement(element);
    if (existingContext?.editor) {
        cleanupEditorRuntimeState(existingContext);
    }

    const diffEditor = monaco.editor.createDiffEditor(element, resolveInitialDiffOptions(initialState));

    // createDiffEditor does not build models for you the way create() does, so both sides
    // are created here -- and disposed in cleanupEditorRuntimeState, since the widget does
    // not own them.
    const language = (initialState && initialState.language) || 'plaintext';
    const originalLanguage = (initialState && initialState.originalLanguage) || language;
    const originalModel = monaco.editor.createModel((initialState && initialState.originalText) || '', originalLanguage);
    const modifiedModel = monaco.editor.createModel((initialState && initialState.text) || '', language);
    diffEditor.setModel({ original: originalModel, modified: modifiedModel });

    var editorContext = EditorContext.registerDiffEditorForElement(element, diffEditor);
    editorContext.model = modifiedModel;
    editorContext.originalModel = originalModel;

    // Registered before attachEditorRuntime so the first computation is not missed.
    // Accessor is assigned synchronously at the top of that call and Monaco computes the
    // diff asynchronously, so the guard below is belt-and-braces.
    diffEditor.onDidUpdateDiff(() => {
        if (editorContext.Accessor) {
            editorContext.Accessor.callAction("DiffUpdated");
        }
    });

    await attachEditorRuntime(managedOwner, element, editorContext, diffEditor, "ModifiedText", initialState);
};

/**
 * Initialize a Monaco multi-file diff instance, over the same element and the same lifecycle
 * handshake the other two flavors use.
 *
 * Unlike those, this leaves `editorContext.editor` and `.model` unset: the widget's per-file
 * editors are pooled and recycled by an ObjectPool, so there is no stable single editor to
 * alias, and every inherited single-document member is inert here by design.
 */
export const initializeMonacoMultiDiffEditor = async (managedOwner: any, element: any, initialState?: InitialState) => {
    const existingContext = EditorContext.tryGetEditorForElement(element);
    if (existingContext?.editor || existingContext?.multiDiff) {
        cleanupEditorRuntimeState(existingContext);
    }

    const editorContext = EditorContext.getEditorForElement(element);
    const state = createMultiDiffWidget(element, initialState?.diffOptions);
    editorContext.multiDiff = state;

    if (initialState?.files?.length) {
        updateMultiDiffFiles(element, initialState.files);
    }

    // No textPropertyName: read-only in v1, and there is no single property to write back to.
    await attachEditorRuntime(
        managedOwner,
        element,
        editorContext,
        { layout: () => layoutMultiDiffEditor(state) },
        null,
        initialState);

    // After attachEditorRuntime, because the observers report through editorContext.Accessor,
    // which that call is what creates.
    attachMultiDiffObservers(state, editorContext);
};

/**
 * Dispose an editor context: disconnects ResizeObserver, unregisters RPC handlers,
 * disposes Monaco editor, removes context map entry, and releases the connection reference.
 */
export const disposeEditor = (element: any) => {
    const editorContext = EditorContext.tryGetEditorForElement(element);
    if (!editorContext) return;

    cleanupEditorRuntimeState(editorContext);

    // Remove from context map
    EditorContext.removeEditorForElement(element);

    // Release the connection reference on desktop (disposes only when last editor releases)
    if (_isDesktop && (editorContext as any)._connectionRetained && editorContext.Accessor) {
        editorContext.Accessor.close();
        (editorContext as any)._connectionRetained = false;
    }
};

export const replaceAll = (str: string, find: string, rep: string): string => {
    if (find == "\\") {
        find = "\\\\";
    }
    return (`${str}`).replace(new RegExp(find, "g"), rep);
}

export const sanitize = (jsonString: string): string => {
    if (jsonString == null) {
        return null as any;
    }

    const replacements = "%&\\\"'{}:,";
    for (let i = 0; i < replacements.length; i++) {
        jsonString = replaceAll(jsonString, replacements.charAt(i), `%${replacements.charCodeAt(i)}`);
    }
    return jsonString;
}

export const desanitize = (parameter: string): string => {
    if (parameter == null) return parameter;
    const replacements = "&\\\"'{}:,%";
    for (let i = 0; i < replacements.length; i++) {
        parameter = replaceAll(parameter, "%" + replacements.charCodeAt(i), replacements.charAt(i));
    }
    return parameter;
}

/**
 * On desktop, values arrive as clean JSON via JSON-RPC -- no sanitize encoding needed.
 * On WASM, apply sanitize encoding for the JSExport marshalling path.
 */
export const stringifyForMarshalling = (value: string): string => {
    if (_isDesktop) {
        return value;
    }
    return sanitize(value);
}

/**
 * callParentEventAsync -- invoke a named event handler and return the result.
 * On desktop, parameters are sent as clean JSON (no sanitize/desanitize).
 * On WASM, parameters are sanitized for JSExport marshalling.
 */
export const callParentEventAsync = async (element: any, name: string, parameters: string[]): Promise<string | null> => {
    if (_isDesktop) {
        // Desktop: send parameters as clean JSON array via JSON-RPC
        const result = await EditorContext.getEditorForElement(element).Accessor.callEvent(
            name,
            parameters != null && parameters.length > 0 ? parameters[0] : null as any,
            parameters != null && parameters.length > 1 ? parameters[1] : null as any);
        return result;
    }

    // WASM: sanitize parameters for JSExport marshalling
    let result = await EditorContext.getEditorForElement(element).Accessor.callEvent(name,
        parameters != null && parameters.length > 0 ? stringifyForMarshalling(parameters[0]) : null as any,
        parameters != null && parameters.length > 1 ? stringifyForMarshalling(parameters[1]) : null as any);

    if (result) {
        result = desanitize(result);
    }

    return result;
}

export const callParentActionWithParameters = (element: any, name: string, parameters: string[]): boolean | void =>
    EditorContext.getEditorForElement(element).Accessor.callActionWithParameters(name,
        parameters != null && parameters.length > 0 ? stringifyForMarshalling(parameters[0]) : null as any,
        parameters != null && parameters.length > 1 ? stringifyForMarshalling(parameters[1]) : null as any);

/**
 * Shared bootstrap for both editor flavors. Prepares the dynamic style element,
 * normalizes the pushed initial state, sets up the bridge helpers, runs the supplied
 * initializer, and reports readiness.
 *
 * Sharing this is what gives the diff editor every recovery signal the C# host probes
 * for -- __unoMonacoInitError, __unoMonacoInitComplete, and the editor/ready
 * notification -- without adding a second set of lifecycle paths.
 */
const bootstrapMonaco = async (
    managedOwner: any,
    elementId: string,
    initialStatePayload: InitialState | string | undefined,
    label: string,
    initializer: (managedOwner: any, element: any, initialState?: InitialState) => Promise<void>) => {
    (globalThis as any).__unoMonacoInitError = null;
    (globalThis as any).__unoMonacoInitComplete = false;

    // Ensure a single <style id="dynamic"> element exists (editor.html already provides one on desktop)
    if (!document.getElementById('dynamic')) {
        var head = document.head || document.getElementsByTagName('head')[0];
        var style = document.createElement('style');
        style.id = 'dynamic';
        head.appendChild(style);
    }

    // Parse/normalize initial state if provided (pushed from C# on desktop).
    // Supports both JSON object payload (preferred) and JSON string payload (legacy).
    let initialState: InitialState | undefined;
    if (typeof initialStatePayload === 'string') {
        if (initialStatePayload.length > 0) {
            try {
                initialState = JSON.parse(initialStatePayload) as InitialState;
                console.log(`[${label}] Parsed initial state (string): theme=${initialState.themeName}`);
            } catch (err) {
                console.warn(`[${label}] Failed to parse initial state JSON string:`, err);
            }
        }
    } else if (initialStatePayload && typeof initialStatePayload === 'object') {
        initialState = initialStatePayload as InitialState;
        console.log(`[${label}] Parsed initial state (object): theme=${initialState.themeName}`);
    }

    await DebugLoggerImpl.setup();
    await KeyboardListenerImpl.setup();
    await ParentAccessor.setup();
    await ThemeListener.setup();

    try {
        await initializer(managedOwner, document.getElementById(elementId), initialState);
        (globalThis as any).__unoMonacoInitComplete = true;
    } catch (err) {
        (globalThis as any).__unoMonacoInitError = String(err);
        console.error(`[${label}] initialization failed:`, err);
    }

    // Emit editor/ready notification on desktop after Monaco init completes.
    // Sent even on partial init failure so the C# side can proceed.
    if (_isDesktop) {
        getConnection().sendNotification('editor/ready', { protocolVersion: 1 });
    }
}

/** Bootstrap entry point for CodeEditor. Invoked from C# by name. */
export const createMonacoEditor = async (managedOwner: any, elementId: string, basePath: string, initialStatePayload?: InitialState | string) =>
    await bootstrapMonaco(managedOwner, elementId, initialStatePayload, 'createMonacoEditor', initializeMonacoEditor);

/** Bootstrap entry point for DiffCodeEditor. Invoked from C# by name. */
export const createMonacoDiffEditor = async (managedOwner: any, elementId: string, basePath: string, initialStatePayload?: InitialState | string) =>
    await bootstrapMonaco(managedOwner, elementId, initialStatePayload, 'createMonacoDiffEditor', initializeMonacoDiffEditor);

/** Bootstrap entry point for MultiDiffCodeEditor. Invoked from C# by name. */
export const createMonacoMultiDiffEditor = async (managedOwner: any, elementId: string, basePath: string, initialStatePayload?: InitialState | string) =>
    await bootstrapMonaco(managedOwner, elementId, initialStatePayload, 'createMonacoMultiDiffEditor', initializeMonacoMultiDiffEditor);

export const InvokeJS = (elementId: string, command: string): string => {
    var r = eval(`var element = globalThis.document.getElementById("${elementId}"); ${command}`) || "";
    return JSON.stringify(r);
}

export const languageIdFromExtension = (extension: string): string => {
    if (extension != null) {
        const lower = extension.toLowerCase();
        const langs = monaco.languages.getLanguages();
        for (const l of langs) {
            if (!l.extensions) continue;
            if (l.extensions.some(ext => lower.endsWith(ext))) return l.id;
        }
    }

    return 'plaintext';
}
