    ///<reference path="../monaco-editor/monaco.d.ts" />

class EditorContext {
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

    constructor() {
        this.modifingSelection = false;
        this.contexts = {};
        this.decorations = [];
    }

    public Accessor: ParentAccessor;
    public Keyboard: KeyboardListener;
    public Theme: ThemeAccessor;

    public editor: monaco.editor.IStandaloneCodeEditor;
    public model: monaco.editor.ITextModel;
    public contexts: { [index: string]: monaco.editor.IContextKey<any> };
    public decorations: string[];
    public modifingSelection: boolean; // Supress updates to selection when making edits.
}

const registerHoverProvider = function (element: any, languageId: string) {
    var editorContext = EditorContext.getEditorForElement(element);

    return monaco.languages.registerHoverProvider(languageId, {
        provideHover: function (model, position) {
            return callParentEventAsync(element, "HoverProvider" + languageId, [JSON.stringify(position)]).then(result => {
                if (result) {
                    return JSON.parse(result);
                }
            });
        }
    });
};

const addAction = function (element: any, action: monaco.editor.IActionDescriptor) {
    var editorContext = EditorContext.getEditorForElement(element);

    action.run = function (ed) {
        editorContext.Accessor.callAction("Action" + action.id)
    };

    editorContext.editor.addAction(action);
};

const addCommand = function (element: any, keybindingStr, handlerName, context) {
    var editorContext = EditorContext.getEditorForElement(element);

    return editorContext.editor.addCommand(parseInt(keybindingStr), function () {
        const objs = [];
        if (arguments) { // Use arguments as Monaco will pass each as it's own parameter, so we don't know how many that may be.
            for (let i = 1; i < arguments.length; i++) { // Skip first one as that's the sender?
                objs.push(JSON.stringify(arguments[i]));
            }
        }
        editorContext.Accessor.callActionWithParameters(handlerName, objs);
    }, context);
};

const createContext = function (element: any, context) {
    var editorContext = EditorContext.getEditorForElement(element);

    if (context) {
        editorContext.contexts[context.key] = editorContext.editor.createContextKey(context.key, context.defaultValue);
    }
};

const updateContext = function (element: any, key, value) {
    var editorContext = EditorContext.getEditorForElement(element);

    editorContext.contexts[key].set(value);
}

// link:CodeEditor.Properties.cs:updateContent
const updateContent = function (element: any, content) {
    var editorContext = EditorContext.getEditorForElement(element);

   // Need to ignore updates from us notifying of a change
    if (content !== editorContext.model.getValue()) {
        editorContext.model.setValue(content);
    }
};

const updateDecorations = function (element: any, newHighlights) {
    var editorContext = EditorContext.getEditorForElement(element);

    if (newHighlights) {
        editorContext.decorations = editorContext.editor.deltaDecorations(editorContext.decorations, newHighlights);
    } else {
        editorContext.decorations = editorContext.editor.deltaDecorations(editorContext.decorations, []);
    }
};

const updateStyle = function (innerStyle) {
    var style = document.getElementById("dynamic");
    style.innerHTML = innerStyle;
};

const getOptions = async function (element: any): Promise<monaco.editor.IEditorOptions> {
    var editorContext = EditorContext.getEditorForElement(element);

    let opt = null;
    try {
        opt = getParentValue(element, "Options");
    } finally {

    }

    if (opt !== null && typeof opt === "object") {
        return opt;
    }

    return {};
};

const updateOptions = function (element: any, opt: monaco.editor.IEditorOptions) {
    var editorContext = EditorContext.getEditorForElement(element);

    if (opt !== null && typeof opt === "object") {
        editorContext.editor.updateOptions(opt);
    }
};

const updateLanguage = function (element: any, language) {
    var editorContext = EditorContext.getEditorForElement(element);
    monaco.editor.setModelLanguage(editorContext.model, language);
};

const changeTheme = function (element: any, theme: string, highcontrast) {
    var editorContext = EditorContext.getEditorForElement(element);
    let newTheme = 'vs';
    if (highcontrast == "True" || highcontrast == "true") {
        newTheme = 'hc-black';
    } else if (theme == "Dark") {
        newTheme = 'vs-dark';
    }

    monaco.editor.setTheme(newTheme);
};



const keyDown = async function (element: any, event) {
    var editorContext = EditorContext.getEditorForElement(element);
    //Debug.log("Key Down:" + event.keyCode + " " + event.ctrlKey);
    const result = await editorContext.Keyboard.keyDown(event.keyCode, event.ctrlKey, event.shiftKey, event.altKey, event.metaKey);
    if (result) {
        event.cancelBubble = true;
        event.preventDefault();
        event.stopPropagation();
        event.stopImmediatePropagation();
        return false;
    }
};
