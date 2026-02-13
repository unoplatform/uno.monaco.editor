import * as monaco from 'monaco-editor';
import { ParentAccessor } from './Monaco.Helpers.ParentAccessor';
import { isDesktopHost, getConnection, sendRequestWithTimeout, retainConnection } from './bridge/jsonRpcBridge';
import { Disposable } from 'vscode-jsonrpc/browser';
import { EditorContext, changeTheme } from './otherScriptsToBeOrganized';

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

    // editor/getValue -- returns the current editor text
    disposables.push(
        connection.onRequest('editor/getValue', () => {
            return editorContext.editor.getValue();
        })
    );

    // editor/updateOptions -- push updated editor options to Monaco
    disposables.push(
        connection.onNotification('editor/updateOptions', (params: { options: any }) => {
            if (params && params.options && typeof params.options === 'object') {
                editorContext.editor.updateOptions(params.options);
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
}

/**
 * Initialize the Monaco editor instance.
 * On desktop with initialState provided, theme/text/language are applied synchronously
 * from the pushed values -- no async RPC round-trips needed.
 * On WASM (or desktop without initialState), property reads use the existing paths.
 */
export const initializeMonacoEditor = async (managedOwner: any, element: any, initialState?: InitialState) => {
    // When initial state is provided, pass theme + language + readOnly to monaco.editor.create()
    // so the editor renders correctly from the first frame.
    var opt: any = {};
    let initialThemeName: string | null = null;
    let initialIsHighContrast = false;

    if (initialState) {
        console.log(`[initializeMonacoEditor] Using pushed initial state: theme=${initialState.themeName}, lang=${initialState.language}`);

        // Determine Monaco theme ID from initial state
        initialIsHighContrast = initialState.isHighContrast;
        initialThemeName = initialState.themeName;

        let monacoTheme = 'vs';
        if (initialIsHighContrast) {
            monacoTheme = 'hc-black';
        } else if (initialThemeName === 'Dark') {
            monacoTheme = 'vs-dark';
        }

        opt = {
            theme: monacoTheme,
            language: initialState.language || 'plaintext',
            readOnly: initialState.readOnly || false,
            value: initialState.text || '',
        };
    }

    const editor = monaco.editor.create(element, opt);
    var editorContext = EditorContext.registerEditorForElement(element, editor);

    (<any>editorContext).Debug = new DebugLoggerImpl(managedOwner);
    (<any>editorContext).Keyboard = new KeyboardListenerImpl(managedOwner);
    (<any>editorContext).Accessor = new ParentAccessor(managedOwner);
    (<any>editorContext).Theme = new ThemeListener(managedOwner);

    editorContext.model = editor.getModel()!;

    // Listen for Content Changes
    editorContext.model.onDidChangeContent((event) => {
        editorContext.Accessor.setValue("Text", stringifyForMarshalling(editorContext.model.getValue()));
    });

    // Listen for Selection Changes
    editor.onDidChangeCursorSelection((event) => {
        if (!editorContext.modifingSelection) {
            editorContext.Accessor.setValue("SelectedText", stringifyForMarshalling(editorContext.model.getValueInRange(event.selection)));
            editorContext.Accessor.setValueWithType("SelectedRange", stringifyForMarshalling(JSON.stringify(event.selection)), "Selection");
        }
    });

    // Apply theme: if initial state was provided (desktop), theme is already applied via
    // monaco.editor.create options -- skip async RPC round-trips entirely.
    // Otherwise (WASM or fallback), use the existing async path.
    if (!initialState) {
        // Set theme -- async on desktop (JSON-RPC with timeout), sync on WASM (JSExport)
        // Wrapped in try-catch so editor init completes even if theme RPC stalls (CI cold-start).
        try {
            const themeInitStart = performance.now();

            const t0 = performance.now();
            let theme: any = await editorContext.Accessor.getJsonValueAsync("RequestedTheme");
            const t1 = performance.now();
            if (_isDesktop) {
                console.log(`[initializeMonacoEditor] getJsonValueAsync("RequestedTheme"): ${(t1 - t0).toFixed(1)}ms, result=${theme}`);
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
                    console.log(`[initializeMonacoEditor] getCurrentThemeNameAsync: ${(t3 - t2).toFixed(1)}ms, result=${theme}`);
                }
            }

            const t4 = performance.now();
            const isHighContrast = await (editorContext as any).Theme.getIsHighContrastAsync();
            const t5 = performance.now();
            if (_isDesktop) {
                console.log(`[initializeMonacoEditor] getIsHighContrastAsync: ${(t5 - t4).toFixed(1)}ms, result=${isHighContrast}`);
            }

            changeTheme(element, theme, isHighContrast as any);

            const themeInitEnd = performance.now();
            const cumulativeMs = themeInitEnd - themeInitStart;
            if (_isDesktop) {
                console.log(`[initializeMonacoEditor] Theme init cumulative: ${cumulativeMs.toFixed(1)}ms` +
                    (cumulativeMs > 16 ? ' (EXCEEDS 16ms frame budget)' : ''));
            }
        } catch (err) {
            console.warn('[initializeMonacoEditor] Theme initialization failed, using defaults:', err);
            changeTheme(element, 'Light', 'false');
        }
    } else {
        console.log(`[initializeMonacoEditor] Skipped async theme init -- using pushed initial state`);
    }

    // Track parent element size changes via ResizeObserver for deterministic cleanup.
    // This replaces the old window "resize" listener that fired on every window resize
    // even when the editor's container didn't change size.
    const resizeObserver = new ResizeObserver(() => { editor.layout(); });
    resizeObserver.observe(element);
    (editorContext as any)._resizeObserver = resizeObserver;

    // Disable WebView Scrollbar so Monaco Scrollbar can do heavy lifting
    document.body.style.overflow = 'hidden';

    // Register C#->JS JSON-RPC handlers on desktop; track disposables for cleanup
    if (_isDesktop) {
        retainConnection();
        const handlerDisposables = registerDesktopHandlers(editorContext);
        // Store disposables on context for deterministic teardown
        (editorContext as any)._rpcHandlerDisposables = handlerDisposables;
    }

    // Callback to Parent that we're loaded
    editorContext.Accessor.callAction("Loaded");
};

/**
 * Dispose an editor context: disconnects ResizeObserver, unregisters RPC handlers,
 * disposes Monaco editor, removes context map entry, and releases the connection reference.
 */
export const disposeEditor = (element: any) => {
    const editorContext = EditorContext.tryGetEditorForElement(element);
    if (!editorContext) return;

    // Disconnect the ResizeObserver
    const resizeObserver = (editorContext as any)._resizeObserver as ResizeObserver | undefined;
    if (resizeObserver) {
        resizeObserver.disconnect();
        (editorContext as any)._resizeObserver = undefined;
    }

    // Dispose tracked RPC handler registrations
    const disposables = (editorContext as any)._rpcHandlerDisposables as Disposable[] | undefined;
    if (disposables) {
        for (const d of disposables) {
            d.dispose();
        }
        (editorContext as any)._rpcHandlerDisposables = undefined;
    }

    // Dispose the Monaco editor instance (releases DOM, workers, etc.)
    if (editorContext.editor) {
        editorContext.editor.dispose();
    }

    // Remove from context map
    EditorContext.removeEditorForElement(element);

    // Release the connection reference on desktop (disposes only when last editor releases)
    if (_isDesktop && editorContext.Accessor) {
        editorContext.Accessor.close();
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

export const createMonacoEditor = async (managedOwner: any, elementId: string, basePath: string, initialStateJson?: string) => {
    // Ensure a single <style id="dynamic"> element exists (editor.html already provides one on desktop)
    if (!document.getElementById('dynamic')) {
        var head = document.head || document.getElementsByTagName('head')[0];
        var style = document.createElement('style');
        style.id = 'dynamic';
        head.appendChild(style);
    }

    // Parse initial state if provided (pushed from C# on desktop).
    let initialState: InitialState | undefined;
    if (initialStateJson) {
        try {
            initialState = JSON.parse(initialStateJson) as InitialState;
            console.log(`[createMonacoEditor] Parsed initial state: theme=${initialState.themeName}`);
        } catch (err) {
            console.warn('[createMonacoEditor] Failed to parse initial state JSON:', err);
        }
    }

    await DebugLoggerImpl.setup();
    await KeyboardListenerImpl.setup();
    await ParentAccessor.setup();
    await ThemeListener.setup();

    try {
        await initializeMonacoEditor(managedOwner, document.getElementById(elementId), initialState);
    } catch (err) {
        console.error('[createMonacoEditor] initializeMonacoEditor failed:', err);
    }

    // Emit editor/ready notification on desktop after Monaco init completes.
    // Sent even on partial init failure so the C# side can proceed.
    if (_isDesktop) {
        getConnection().sendNotification('editor/ready', { protocolVersion: 1 });
    }
}

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
