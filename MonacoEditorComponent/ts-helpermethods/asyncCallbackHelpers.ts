import * as monaco from 'monaco-editor';
import { ParentAccessor } from './Monaco.Helpers.ParentAccessor';
import { EditorContext, getParentJsonValue, changeTheme, getThemeCurrentThemeName, getThemeIsHighContrast } from './otherScriptsToBeOrganized';

type MethodWithReturnId = (parameter: string) => void;
type NumberCallback = (parameter: any) => void;

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
        let anyModule = (<any>window).Module;

        if (anyModule.getAssemblyExports !== undefined) {
            const browserExports = await anyModule.getAssemblyExports("MonacoEditorComponent");

            ThemeListener._managedGetCurrentThemeName = browserExports.Monaco.Helpers.ThemeListener.ManagedGetCurrentThemeName;
            ThemeListener._managedGetIsHighContrast = browserExports.Monaco.Helpers.ThemeListener.ManagedGetIsHighContrast;
        }
    }

    public getIsHighContrast(): boolean {
        return ThemeListener._managedGetIsHighContrast(this._managedOwner);
    }

    public getCurrentThemeName(): string {
        return ThemeListener._managedGetCurrentThemeName(this._managedOwner);
    }
}

export const initializeMonacoEditor = (managedOwner: any, element: any) => {
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

    // Set theme
    let theme: any = getParentJsonValue(element, "RequestedTheme");
    theme = {
        "0": "Default",
        "1": "Light",
        "2": "Dark"
    }[theme];

    if (theme == "Default") {
        theme = getThemeCurrentThemeName(element);
    }

    changeTheme(element, theme, getThemeIsHighContrast(element) as any);

    // Update Monaco Size when we receive a window resize event
    window.addEventListener("resize", () => {
        editor.layout();
    });

    // Disable WebView Scrollbar so Monaco Scrollbar can do heavy lifting
    document.body.style.overflow = 'hidden';

    // Callback to Parent that we're loaded
    editorContext.Accessor.callAction("Loaded");
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

export const stringifyForMarshalling = (value: any): string => sanitize(value)

export const callParentEventAsync = async (element: any, name: string, parameters: string[]): Promise<string> => {
    let result = await EditorContext.getEditorForElement(element).Accessor.callEvent(name,
        parameters != null && parameters.length > 0 ? stringifyForMarshalling(parameters[0]) : null as any,
        parameters != null && parameters.length > 1 ? stringifyForMarshalling(parameters[1]) : null as any);

    if (result) {
        result = desanitize(result);
    }

    return result;
}

export const callParentActionWithParameters = (element: any, name: string, parameters: string[]): boolean =>
    EditorContext.getEditorForElement(element).Accessor.callActionWithParameters(name,
        parameters != null && parameters.length > 0 ? stringifyForMarshalling(parameters[0]) : null as any,
        parameters != null && parameters.length > 1 ? stringifyForMarshalling(parameters[1]) : null as any);

export const createMonacoEditor = async (managedOwner: any, elementId: string, basePath: string) => {
    var head = document.head || document.getElementsByTagName('head')[0];
    var style = document.createElement('style');
    style.id = 'dynamic';
    head.appendChild(style);

    await DebugLoggerImpl.setup();
    await KeyboardListenerImpl.setup();
    await ParentAccessor.setup();
    await ThemeListener.setup();

    initializeMonacoEditor(managedOwner, document.getElementById(elementId));
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
