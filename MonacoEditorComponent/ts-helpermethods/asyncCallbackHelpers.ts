type MethodWithReturnId = (parameter: string) => void;
type NumberCallback = (parameter: any) => void;

const initializeMonacoEditor = (managedOwner: any, element: any) => {
    {
      //  console.debug("Grabbing Monaco Options");

        var opt = {}
    };

    //console.debug("Getting Host container");
    //console.debug("Creating Editor");
    const editor = monaco.editor.create(element, opt);
    var editorContext = EditorContext.registerEditorForElement(element, editor);

    (<any>editorContext).Debug = new DebugLogger(managedOwner);
    (<any>editorContext).Keyboard = new KeyboardListener(managedOwner);
    (<any>editorContext).Accessor = new ParentAccessor(managedOwner);
    (<any>editorContext).Theme = new ThemeListener(managedOwner);

    //console.debug("Getting Editor model");
    editorContext.model = editor.getModel();

    // Listen for Content Changes
    //console.debug("Listening for changes in the editor model - " + (!editorContext.model));

    editorContext.model.onDidChangeContent((event) => {
        {
            editorContext.Accessor.setValue("Text", stringifyForMarshalling(editorContext.model.getValue()));
        }
    });

    // Listen for Selection Changes
    //console.debug("Listening for changes in the editor selection");
    editor.onDidChangeCursorSelection((event) => {
        {
            if (!editorContext.modifingSelection) {
                {
                    editorContext.Accessor.setValue("SelectedText", stringifyForMarshalling(editorContext.model.getValueInRange(event.selection)));
                    editorContext.Accessor.setValueWithType("SelectedRange", stringifyForMarshalling(JSON.stringify(event.selection)), "Selection");
                }
            }
        }
    });

    // Set theme
    //console.debug("Getting parent theme value");
    let theme = getParentJsonValue(element, "RequestedTheme");
    theme = {
        "0": "Default",
        "1": "Light",
        "2": "Dark"
    }
    [theme];
    //console.debug("Current theme value - " + theme);
    if (theme == "Default") {
        {
    //        console.debug("Loading default theme");

            theme = getThemeCurrentThemeName(element);
        }
    }
  //  console.debug("Changing theme");
    changeTheme(element, theme, getThemeIsHighContrast(element));

    // Update Monaco Size when we receive a window resize event
//    console.debug("Listen for resize events on the window and resize the editor");
    window.addEventListener("resize", () => {
        {
            editor.layout();
        }
    });

    // Disable WebView Scrollbar so Monaco Scrollbar can do heavy lifting
    document.body.style.overflow = 'hidden';

    // Callback to Parent that we're loaded
 //   console.debug("Loaded Monaco");
    editorContext.Accessor.callAction("Loaded");

   // console.debug("Ending Monaco Load");
};

class DebugLogger {
    private _managedOwner: any;

    constructor(managedOwner: any) {
        this._managedOwner = managedOwner;
    }

    public static async setup() {
    }
}

class KeyboardListener {
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

globalThis.createMonacoEditor = async (managedOwner: any, elementId: string, basePath: string) => {
  //  console.debug("Create dynamic style element");
    var head = document.head || document.getElementsByTagName('head')[0];
    var style = document.createElement('style');
    style.id = 'dynamic';
    head.appendChild(style);

    await DebugLogger.setup();
    await KeyboardListener.setup();
    await ParentAccessor.setup();
    await ThemeListener.setup();

//    console.debug("Starting Monaco Load");

    (<any>window).require.config({ paths: { 'vs': `${basePath}/MonacoEditorComponent/monaco-editor/min/vs` } });
    (<any>window).require(['vs/editor/editor.main'], function () {
        initializeMonacoEditor(managedOwner, document.getElementById(elementId));
    });
}

const replaceAll = (str: string, find: string, rep: string): string => {
    if (find == "\\")
    {
        find = "\\\\";
    }
    return (`${str}`).replace(new RegExp(find, "g"), rep);
}

const sanitize = (jsonString: string): string => {
    if (jsonString == null) {
        //console.log('Sanitized is null');
        return null;
    }

    const replacements = "%&\\\"'{}:,";
    for (let i = 0; i < replacements.length; i++) {
        jsonString = replaceAll(jsonString, replacements.charAt(i), `%${replacements.charCodeAt(i)}`);
    }
    //console.log('Sanitized: ' + jsonString);
    return jsonString;
}

const desantize = (parameter: string): string => {
    //System.Diagnostics.Debug.WriteLine($"Encoded String: {parameter}");
    if (parameter == null) return parameter;
    const replacements = "&\\\"'{}:,%";
    //System.Diagnostics.Debug.WriteLine($"Replacements: >{replacements}<");
    for (let i = 0; i < replacements.length; i++)
    {
        //console.log("Replacing: >%" + replacements.charCodeAt(i) + "< with >" + replacements.charAt(i) + "< ");
        parameter = replaceAll(parameter, "%" + replacements.charCodeAt(i), replacements.charAt(i));
    }

    //console.log("Decoded String: " + parameter );
    return parameter;
}

const stringifyForMarshalling = (value: any): string => sanitize(value)

const getParentValue = (element:any, name: string): any => {
    return EditorContext.getEditorForElement(element).Accessor.getJsonValue(name);
}

const getParentJsonValue = (element: any, name: string): string =>
    EditorContext.getEditorForElement(element).Accessor.getJsonValue(name);

const getThemeIsHighContrast = (element: any): boolean =>
    EditorContext.getEditorForElement(element).Theme.getIsHighContrast() == "true";

const getThemeCurrentThemeName = (element: any): string =>
    EditorContext.getEditorForElement(element).Theme.getCurrentThemeName();

const callParentEventAsync = async (element: any, name: string, parameters: string[]): Promise<string> =>
{
    let result = await EditorContext.getEditorForElement(element).Accessor.callEvent(name,
        parameters != null && parameters.length > 0 ? stringifyForMarshalling(parameters[0]) : null,
        parameters != null && parameters.length > 1 ? stringifyForMarshalling(parameters[1]) : null);

    if (result) {
        result = desantize(result);
    } else {
        // console.debug('No Parent event result for ' + name);
    }

    return result;
}

const callParentActionWithParameters = (element: any, name: string, parameters: string[]): boolean =>
    EditorContext.getEditorForElement(element).Accessor.callActionWithParameters(name,
        parameters != null && parameters.length > 0 ? stringifyForMarshalling(parameters[0]) : null,
        parameters != null && parameters.length > 1 ? stringifyForMarshalling(parameters[1]) : null);

globalThis.InvokeJS = (elementId: string, command: string): string => {
    var r = eval(`var element = globalThis.document.getElementById(\"${elementId}\"); ${command}`) || "";
    return JSON.stringify(r);
}

globalThis.refreshLayout = (elementId: string) => {
    EditorContext.getEditorForElement(document.getElementById(elementId)).editor.layout();
}

globalThis.languageIdFromExtension = (extension: string): string => {

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