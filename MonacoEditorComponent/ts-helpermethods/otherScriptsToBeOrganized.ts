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

    /**
     * Register a diff editor for an element.
     *
     * The modified (right-hand) sub-editor is aliased onto `editor`, which
     * monaco types as IStandaloneCodeEditor -- exactly what the field already
     * declares. Every existing helper (updateContent, updateLanguage, addAction,
     * decorations, selection tracking, the editor/getValue RPC handler) therefore
     * keeps working unchanged, operating on the editable side of the diff.
     */
    public static registerDiffEditorForElement(element: any, diffEditor: monaco.editor.IStandaloneDiffEditor): EditorContext {
        var value = EditorContext.getEditorForElement(element);
        value.diffEditor = diffEditor;
        value.editor = diffEditor.getModifiedEditor();
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

    /**
     * Reverse lookup used by every language-provider bridge to find the element a
     * provider callback fired for.
     *
     * A diff editor owns two models, and providers registered for a language fire for
     * both sides. Matching only `model` would return null for the original side, and
     * the callers pass that null straight into the *creating* getEditorForElement,
     * fabricating a junk context. Match both models.
     */
    public static getElementFromModel(model: monaco.editor.ITextModel): any {
        for (let [key, value] of EditorContext._editors) {
            if (value.model === model || value.originalModel === model) {
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

    /** The modified (right-hand) sub-editor when this context hosts a diff editor. */
    public editor: monaco.editor.IStandaloneCodeEditor;
    /** The modified (right-hand) model when this context hosts a diff editor. */
    public model: monaco.editor.ITextModel;
    /** Set only for a diff editor. Its absence is what marks a context as a plain editor. */
    public diffEditor?: monaco.editor.IStandaloneDiffEditor;
    /** The original (left-hand) model. Set only for a diff editor. */
    public originalModel?: monaco.editor.ITextModel;
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

    action.run = function (ed, ...runArgs) {
        const objs: string[] = [];
        try {
            const selection = ed && ed.getSelection ? ed.getSelection() : null;
            const model = ed && ed.getModel ? ed.getModel() : null;
            const selectedText = selection && model ? model.getValueInRange(selection) : '';
            objs.push(JSON.stringify(selectedText ?? ''));
        } catch {
            objs.push(JSON.stringify(''));
        }

        if (runArgs) {
            for (let i = 0; i < runArgs.length; i++) {
                objs.push(JSON.stringify(runArgs[i]));
            }
        }

        editorContext.Accessor.callActionWithParameters2("Action" + action.id, objs);
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

// ---------------------------------------------------------------------------
// Diff editor helpers
//
// All of these are no-ops on a plain editor context, so a C# caller that reaches
// for them against the wrong control degrades quietly instead of throwing across
// the bridge.
// ---------------------------------------------------------------------------

/**
 * Lay out the editor. Resolves to the diff widget when present -- laying out only
 * the modified sub-editor leaves the original side and the split view stale.
 */
export const layoutEditor = function (element: any) {
    var editorContext = EditorContext.tryGetEditorForElement(element);
    if (!editorContext) {
        return;
    }

    const target = editorContext.diffEditor ?? editorContext.editor;
    if (target) {
        target.layout();
    }
};

/** Replace the original (left-hand) document. */
export const updateOriginalContent = function (element: any, content: string) {
    var editorContext = EditorContext.tryGetEditorForElement(element);
    const model = editorContext?.originalModel;

    if (model && content !== model.getValue()) {
        model.setValue(content);
    }
};

/** Set the syntax language of the original (left-hand) document. */
export const updateOriginalLanguage = function (element: any, language: string) {
    var editorContext = EditorContext.tryGetEditorForElement(element);

    if (editorContext?.originalModel) {
        monaco.editor.setModelLanguage(editorContext.originalModel, language);
    }
};

/**
 * Push diff-specific options (renderSideBySide, ignoreTrimWhitespace, diffAlgorithm,
 * hideUnchangedRegions, ...). Deliberately separate from updateOptions, which targets
 * the modified sub-editor -- the two option sets have different sinks and silently
 * swallow each other's keys.
 */
export const updateDiffOptions = function (element: any, opt: any) {
    var editorContext = EditorContext.tryGetEditorForElement(element);

    if (editorContext?.diffEditor && opt !== null && typeof opt === "object") {
        editorContext.diffEditor.updateOptions(opt);
    }
};

/** Jump to the next or previous diff hunk. */
export const goToDiff = function (element: any, target: string) {
    var editorContext = EditorContext.tryGetEditorForElement(element);

    editorContext?.diffEditor?.goToDiff(target === 'previous' ? 'previous' : 'next');
};

/** Scroll to the first diff hunk, waiting for the diff computation to finish. */
export const revealFirstDiff = function (element: any) {
    var editorContext = EditorContext.tryGetEditorForElement(element);

    editorContext?.diffEditor?.revealFirstDiff();
};

/**
 * The computed diff hunks, or null when there is no diff editor or Monaco has not
 * finished computing yet. Pairs with the DiffUpdated callback, which fires whenever
 * this value changes.
 */
export const getLineChanges = function (element: any) {
    var editorContext = EditorContext.tryGetEditorForElement(element);

    return editorContext?.diffEditor ? editorContext.diffEditor.getLineChanges() : null;
};
