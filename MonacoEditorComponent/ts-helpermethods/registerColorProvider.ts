///<reference path="../monaco-editor/monaco.d.ts" />

const registerColorProvider = function (element: any, languageId) {
    var editorContext = EditorContext.getEditorForElement(element);

    return monaco.languages.registerColorProvider(languageId, {
        provideColorPresentations: function (model, colorInfo, token) {
            return callParentEventAsync(element, "ProvideColorPresentations" + languageId, [JSON.stringify(colorInfo)]).then(result => {
                if (result) {
                    return JSON.parse(result);
                }
            });
        },
        provideDocumentColors: function (model, token) {
            return callParentEventAsync(element, "ProvideDocumentColors" + languageId, []).then(result => {
                if (result) {
                    return JSON.parse(result);
                }
            });
        }
    });
}