type MethodWithReturnId = (parameter: string) => void;
type NumberCallback = (parameter: any) => void;
declare var asyncCallbackMap: { [promiseId: string]: NumberCallback };
declare var nextAsync: number;

nextAsync = 1;
asyncCallbackMap = {};

declare var returnValueCallbackMap: { [returnId: string]: string };
declare var nextReturn: number;

nextReturn = 1;
returnValueCallbackMap = {};

const initializeMonacoEditor = (handle: string, element: any) => {
    {
        console.debug("Grabbing Monaco Options");

        var opt = {}
    };
    try {
        {
            opt = getOptions(element);
        }
    }
    catch (err) {
        {
            console.debug("Unable to read options - " + err);
        }
    }

    console.debug("Getting Parent Text value");
    opt["value"] = getParentValue(element, "Text");

    console.debug("Getting Host container");
    console.debug("Creating Editor");
    const editor = monaco.editor.create(element, opt);
    var editorContext = EditorContext.registerEditorForElement(element, editor);

    console.debug("Getting Editor model");
    editorContext.model = editor.getModel();

    // Listen for Content Changes
    console.debug("Listening for changes in the editor model - " + (!editorContext.model));

    editorContext.model.onDidChangeContent((event) => {
        {
            editorContext.Accessor.setValue("Text", stringifyForMarshalling(editorContext.model.getValue()));
            //console.log("buffers: " + JSON.stringify(model._buffer._pieceTree._buffers));
            //console.log("commandMgr: " + JSON.stringify(model._commandManager));
            //console.log("viewState:" + JSON.stringify(editor.saveViewState()));
        }
    });

    // Listen for Selection Changes
    console.debug("Listening for changes in the editor selection");
    editor.onDidChangeCursorSelection((event) => {
        {
            if (!editorContext.modifingSelection) {
                {
                    console.log(event.source);
                    editorContext.Accessor.setValue("SelectedText", stringifyForMarshalling(editorContext.model.getValueInRange(event.selection)));
                    editorContext.Accessor.setValueWithType("SelectedRange", stringifyForMarshalling(JSON.stringify(event.selection)), "Selection");
                }
            }
        }
    });

    // Set theme
    console.debug("Getting parent theme value");
    let theme = getParentJsonValue(element, "RequestedTheme");
    theme = {
        "0": "Default",
        "1": "Light",
        "2": "Dark"
    }
    [theme];
    console.debug("Current theme value - " + theme);
    if (theme == "Default") {
        {
            console.debug("Loading default theme");

            theme = getThemeCurrentThemeName(element);
        }
    }
    console.debug("Changing theme");
    changeTheme(element, theme, getThemeIsHighContrast(element));

    // Update Monaco Size when we receive a window resize event
    console.debug("Listen for resize events on the window and resize the editor");
    window.addEventListener("resize", () => {
        {
            editor.layout();
        }
    });

    // Disable WebView Scrollbar so Monaco Scrollbar can do heavy lifting
    document.body.style.overflow = 'hidden';

    // Callback to Parent that we're loaded
    console.debug("Loaded Monaco");
    editorContext.Accessor.callAction("Loaded");

    console.debug("Ending Monaco Load");
};

class DebugLogger {

    public static async setup() {
    }
}

class KeyboardListener {

    public static async setup() {
    }
}

class ParentAccessor {

    public static async setup() {
    }
}

class ThemeListener {

    public static async setup() {
    }
}

globalThis.createMonacoEditor = async (managedOwner: any, elementId: string, basePath: string) => {
    console.debug("Create dynamic style element");
    var head = document.head || document.getElementsByTagName('head')[0];
    var style = document.createElement('style');
    style.id = 'dynamic';
    head.appendChild(style);

    await DebugLogger.setup();
    await KeyboardListener.setup();
    await ParentAccessor.setup();
    await ThemeListener.setup();

    console.debug("Starting Monaco Load");

    (<any>window).require.config({ paths: { 'vs': `${basePath}/MonacoEditorComponent/monaco-editor/min/vs` } });
    (<any>window).require(['vs/editor/editor.main'], function () {
        initializeMonacoEditor(managedOwner, document.getElementById(elementId));
    });
}

const asyncCallback = (promiseId: string, parameter: string) => {
    const promise = asyncCallbackMap[promiseId];
    if (promise) {
        //console.log('Async response: ' + parameter);
        promise(parameter);
    }
}

const returnValueCallback = (returnId: string, returnValue: string) => {
    //console.log('Return value for id ' + returnId + ' is ' + returnValue);
    returnValueCallbackMap[returnId] = returnValue;
}

const invokeAsyncMethod = <T>(syncMethod: NumberCallback): Promise<T> => {
    if (nextAsync==null) {
        nextAsync = 0;
    }
    if (asyncCallbackMap==null) {
        asyncCallbackMap = {};
    }
    const promise = new Promise<T>((resolve, reject) => {
        var nextId = nextAsync++;
        asyncCallbackMap[nextId] = resolve;
        syncMethod(`${nextId}`);
    });
    return promise;
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

const invokeWithReturnValue = (methodToInvoke: MethodWithReturnId): string => {
    const nextId = nextReturn++;
    methodToInvoke(nextId + '');
    var json = returnValueCallbackMap[nextId];
    //console.log('Return json ' + json);
    json = desantize(json);
    return json;
}

const getParentValue = (element:any, name: string): any => {
    const jsonString = invokeWithReturnValue((returnId) => EditorContext.getEditorForElement(element).Accessor.getJsonValue(name, returnId));
    const obj = JSON.parse(jsonString);
    return obj;
}

const getParentJsonValue = (element: any, name: string): string =>
    invokeWithReturnValue((returnId) => EditorContext.getEditorForElement(element).Accessor.getJsonValue(name, returnId))

const getThemeIsHighContrast = (element: any): boolean =>
    invokeWithReturnValue((returnId) => EditorContext.getEditorForElement(element).Theme.getIsHighContrast(returnId)) == "true";

const getThemeCurrentThemeName = (element: any): string =>
    invokeWithReturnValue((returnId) => EditorContext.getEditorForElement(element).Theme.getCurrentThemeName(returnId));


const callParentEventAsync = (element: any, name: string, parameters: string[]): Promise<string> =>
    invokeAsyncMethod<string>(async (promiseId) => {
        let result = await EditorContext.getEditorForElement(element).Accessor.callEvent(name,
            promiseId,
            parameters != null && parameters.length > 0 ? stringifyForMarshalling(parameters[0]) : null,
            parameters != null && parameters.length > 1 ? stringifyForMarshalling(parameters[1]) : null);
        if (result) {
            console.log('Parent event result: ' + name + ' -  ' +  result);
            result = desantize(result);
            console.log('Desanitized: ' + name + ' -  ' + result);
        } else {
            console.log('No Parent event result for ' + name);
        }

        return result;
    });

const callParentActionWithParameters = (element: any, name: string, parameters: string[]): boolean =>
    EditorContext.getEditorForElement(element).Accessor.callActionWithParameters(name,
        parameters != null && parameters.length > 0 ? stringifyForMarshalling(parameters[0]) : null,
        parameters != null && parameters.length > 1 ? stringifyForMarshalling(parameters[1]) : null);
