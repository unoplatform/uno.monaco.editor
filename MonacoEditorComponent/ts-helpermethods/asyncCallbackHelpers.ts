import * as monaco from 'monaco-editor';
import { ParentAccessor } from './Monaco.Helpers.ParentAccessor';
import { isDesktopHost, getConnection, sendRequestWithTimeout } from './bridge/jsonRpcBridge';
import { Disposable } from 'vscode-jsonrpc/browser';
import { EditorContext, getParentJsonValueAsync, changeTheme, getThemeCurrentThemeNameAsync, getThemeIsHighContrastAsync } from './otherScriptsToBeOrganized';

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
 * Initialize the Monaco editor instance.
 * On desktop, all property reads and theme queries are async (JSON-RPC with timeouts).
 * On WASM, they remain synchronous (JSExport).
 */
export const initializeMonacoEditor = async (managedOwner: any, element: any) => {
    var opt = {};

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

    // Set theme -- async on desktop (JSON-RPC with timeout), sync on WASM (JSExport)
    let theme: any = await getParentJsonValueAsync(element, "RequestedTheme");
    theme = {
        "0": "Default",
        "1": "Light",
        "2": "Dark"
    }[theme];

    if (theme == "Default") {
        theme = await getThemeCurrentThemeNameAsync(element);
    }

    const isHighContrast = await getThemeIsHighContrastAsync(element);
    changeTheme(element, theme, isHighContrast as any);

    // Update Monaco Size when we receive a window resize event
    window.addEventListener("resize", () => {
        editor.layout();
    });

    // Disable WebView Scrollbar so Monaco Scrollbar can do heavy lifting
    document.body.style.overflow = 'hidden';

    // Register C#->JS JSON-RPC handlers on desktop; track disposables for cleanup
    if (_isDesktop) {
        const handlerDisposables = registerDesktopHandlers(editorContext);
        // Store disposables on context for deterministic teardown
        (editorContext as any)._rpcHandlerDisposables = handlerDisposables;
    }

    // Callback to Parent that we're loaded
    editorContext.Accessor.callAction("Loaded");
};

/**
 * Dispose an editor context: unregisters RPC handlers, removes context map entry,
 * and optionally disposes the JSON-RPC connection.
 */
export const disposeEditor = (element: any) => {
    const editorContext = EditorContext.getEditorForElement(element);
    if (!editorContext) return;

    // Dispose tracked RPC handler registrations
    const disposables = (editorContext as any)._rpcHandlerDisposables as Disposable[] | undefined;
    if (disposables) {
        for (const d of disposables) {
            d.dispose();
        }
        (editorContext as any)._rpcHandlerDisposables = undefined;
    }

    // Remove from context map
    EditorContext.removeEditorForElement(element);

    // Dispose the JSON-RPC connection on desktop (rejects pending, removes listeners)
    if (_isDesktop) {
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

export const createMonacoEditor = async (managedOwner: any, elementId: string, basePath: string) => {
    // Ensure a single <style id="dynamic"> element exists (editor.html already provides one on desktop)
    if (!document.getElementById('dynamic')) {
        var head = document.head || document.getElementsByTagName('head')[0];
        var style = document.createElement('style');
        style.id = 'dynamic';
        head.appendChild(style);
    }

    await DebugLoggerImpl.setup();
    await KeyboardListenerImpl.setup();
    await ParentAccessor.setup();
    await ThemeListener.setup();

    await initializeMonacoEditor(managedOwner, document.getElementById(elementId));

    // Emit editor/ready notification on desktop after Monaco init completes
    if (_isDesktop) {
        getConnection().sendNotification('editor/ready', { protocolVersion: 1 });
    }
}

export const InvokeJS = (elementId: string, command: string): string => {
    var r = eval(`var element = globalThis.document.getElementById("${elementId}"); ${command}`) || "";
    return JSON.stringify(r);
}

export const refreshLayout = (elementId: string) => {
    EditorContext.getEditorForElement(document.getElementById(elementId)).editor.layout();
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
