import * as monaco from 'monaco-editor';
import { ParentAccessor } from './Monaco.Helpers.ParentAccessor';
import { callParentEventAsync } from './asyncCallbackHelpers';

export class EditorContext {
    static _editors: Map<any, EditorContext> = new Map<any, EditorContext>();

    public static registerEditorForElement(element: any, editor: monaco.editor.IStandaloneCodeEditor): EditorContext {
        var value = EditorContext.getEditorForElement(element);
        value.editor = editor;
        return value;
    }

    public static getEditorForElement(element: any): EditorContext {
        var context = EditorContext._editors.get(element);

        if (!context) {
            context = new EditorContext();
            EditorContext._editors.set(element, context);
        }

        return context;
    }

    /**
     * Non-creating lookup. Returns undefined if no context exists for the element.
     * Use for safe cleanup paths where creating a new context would be incorrect.
     */
    public static tryGetEditorForElement(element: any): EditorContext | undefined {
        return EditorContext._editors.get(element);
    }

    public static getElementFromModel(model: monaco.editor.ITextModel): any {
        for (let [key, value] of EditorContext._editors) {
            if (value.model === model) {
                return key;
            }
        }
        return null;
    }

    /**
     * Remove an editor context from the map on dispose.
     * Clears the _editors map entry for the given element.
     */
    public static removeEditorForElement(element: any): void {
        EditorContext._editors.delete(element);
    }

    constructor() {
        this.modifingSelection = false;
        this.contexts = {};
        this.decorations = [];
    }

    public Accessor: ParentAccessor;
    public Keyboard: any;
    public Theme: any;

    public editor: monaco.editor.IStandaloneCodeEditor;
    public model: monaco.editor.ITextModel;
    public contexts: { [index: string]: monaco.editor.IContextKey<any> };
    public decorations: string[];
    public modifingSelection: boolean;
}

const hoverProviderRegistrations = new Map<string, monaco.IDisposable>();

export const registerHoverProvider = function (unused: any, languageId: string) {
    const existing = hoverProviderRegistrations.get(languageId);
    if (existing) {
        existing.dispose();
        hoverProviderRegistrations.delete(languageId);
    }

    const disposable = monaco.languages.registerHoverProvider(languageId, {
        provideHover: async function (model, position) {
            var element = EditorContext.getElementFromModel(model);
            try {
                const result = await callParentEventAsync(element, "HoverProvider" + languageId, [JSON.stringify(position)]);
                if (result) {
                    return JSON.parse(result);
                }
            } catch (error) {
                console.warn(`[registerHoverProvider] ${languageId} callback failed`, error);
            }
            return undefined;
        }
    });

    hoverProviderRegistrations.set(languageId, disposable);
    return {
        dispose: () => {
            disposable.dispose();
            if (hoverProviderRegistrations.get(languageId) === disposable) {
                hoverProviderRegistrations.delete(languageId);
            }
        }
    };
};

export const addAction = function (element: any, action: monaco.editor.IActionDescriptor) {
    var editorContext = EditorContext.getEditorForElement(element);

    action.run = function (ed) {
        editorContext.Accessor.callAction("Action" + action.id)
    };

    editorContext.editor.addAction(action);
};

export const addCommand = function (element: any, keybindingStr: string, handlerName: string, context: string) {
    var editorContext = EditorContext.getEditorForElement(element);

    return editorContext.editor.addCommand(parseInt(keybindingStr), function () {
        const objs: string[] = [];
        if (arguments) {
            for (let i = 1; i < arguments.length; i++) {
                objs.push(JSON.stringify(arguments[i]));
            }
        }
        editorContext.Accessor.callActionWithParameters2(handlerName, objs);
    }, context);
};

export const createContext = function (element: any, context: any) {
    var editorContext = EditorContext.getEditorForElement(element);

    if (context) {
        editorContext.contexts[context.key] = editorContext.editor.createContextKey(context.key, context.defaultValue);
    }
};

export const updateContext = function (element: any, key: string, value: any) {
    var editorContext = EditorContext.getEditorForElement(element);

    editorContext.contexts[key].set(value);
}

export const updateContent = function (element: any, content: string) {
    var editorContext = EditorContext.getEditorForElement(element);

    if (content !== editorContext.model.getValue()) {
        editorContext.model.setValue(content);
    }
};

export const updateDecorations = function (element: any, newHighlights: any) {
    var editorContext = EditorContext.getEditorForElement(element);

    if (newHighlights) {
        editorContext.decorations = editorContext.editor.deltaDecorations(editorContext.decorations, newHighlights);
    } else {
        editorContext.decorations = editorContext.editor.deltaDecorations(editorContext.decorations, []);
    }
};

export const updateStyle = function (innerStyle: string) {
    var style = document.getElementById("dynamic");
    if (style) {
        style.innerHTML = innerStyle;
    }
};

/**
 * getOptions -- async to support desktop JSON-RPC path.
 * Uses getParentValueAsync for async property reads.
 */
export const getOptions = async function (element: any): Promise<monaco.editor.IEditorOptions> {
    var editorContext = EditorContext.getEditorForElement(element);

    let opt = null;
    try {
        opt = await getParentValueAsync(element, "Options");
    } finally {
        // no-op
    }

    if (opt !== null && typeof opt === "object") {
        return opt;
    }

    return {};
};

export const updateOptions = function (element: any, opt: monaco.editor.IEditorOptions) {
    var editorContext = EditorContext.getEditorForElement(element);

    if (opt !== null && typeof opt === "object") {
        editorContext.editor.updateOptions(opt);
    }
};

export const updateLanguage = function (element: any, language: string) {
    var editorContext = EditorContext.getEditorForElement(element);

    monaco.editor.setModelLanguage(editorContext.model, language);
};

export const changeTheme = function (element: any, theme: string, highcontrast: string) {
    var editorContext = EditorContext.getEditorForElement(element);
    let newTheme = 'vs';
    if (highcontrast == "True" || highcontrast == "true") {
        newTheme = 'hc-black';
    } else if (theme == "Dark") {
        newTheme = 'vs-dark';
    }

    monaco.editor.setTheme(newTheme);
};

export const keyDown = async function (element: any, event: any) {
    var editorContext = EditorContext.getEditorForElement(element);
    const result = await editorContext.Keyboard.keyDown(event.keyCode, event.ctrlKey, event.shiftKey, event.altKey, event.metaKey);
    if (result) {
        event.cancelBubble = true;
        event.preventDefault();
        event.stopPropagation();
        event.stopImmediatePropagation();
        return false;
    }
};

/**
 * Sync getParentValue -- WASM only. Throws on desktop.
 * Kept for backward compatibility with eval-style InvokeScriptAsync calls on WASM.
 */
export const getParentValue = (element: any, name: string): any => {
    return EditorContext.getEditorForElement(element).Accessor.getJsonValue(name);
};

/**
 * Async getParentValueAsync -- works on both WASM and desktop.
 * On desktop, routes through JSON-RPC request.
 * On WASM, delegates to the sync JSExport path.
 */
export const getParentValueAsync = async (element: any, name: string): Promise<any> => {
    return await EditorContext.getEditorForElement(element).Accessor.getJsonValueAsync(name);
};

/**
 * Sync getParentJsonValue -- WASM only. Throws on desktop.
 */
export const getParentJsonValue = (element: any, name: string): string =>
    EditorContext.getEditorForElement(element).Accessor.getJsonValue(name);

/**
 * Async getParentJsonValueAsync -- works on both WASM and desktop.
 */
export const getParentJsonValueAsync = async (element: any, name: string): Promise<string> =>
    await EditorContext.getEditorForElement(element).Accessor.getJsonValueAsync(name);

/**
 * Sync getThemeIsHighContrast -- WASM only.
 */
export const getThemeIsHighContrast = (element: any): boolean =>
    EditorContext.getEditorForElement(element).Theme.getIsHighContrast() == "true";

/**
 * Async getThemeIsHighContrastAsync -- works on both WASM and desktop.
 */
export const getThemeIsHighContrastAsync = async (element: any): Promise<boolean> => {
    return await EditorContext.getEditorForElement(element).Theme.getIsHighContrastAsync();
};

/**
 * Sync getThemeCurrentThemeName -- WASM only.
 */
export const getThemeCurrentThemeName = (element: any): string =>
    EditorContext.getEditorForElement(element).Theme.getCurrentThemeName();

/**
 * Async getThemeCurrentThemeNameAsync -- works on both WASM and desktop.
 */
export const getThemeCurrentThemeNameAsync = async (element: any): Promise<string> => {
    return await EditorContext.getEditorForElement(element).Theme.getCurrentThemeNameAsync();
};
