///<reference path="../monaco-editor/monaco.d.ts" />

const registerColorProvider = function (unused: any, languageId) {

    return monaco.languages.registerColorProvider(languageId, {
        provideColorPresentations: function (model, colorInfo, token) {

            var element = EditorContext.getElementFromModel(model);

            return callParentEventAsync(element, "ProvideColorPresentations" + languageId, [JSON.stringify(colorInfo)]).then(result => {
                if (result) {
                    return JSON.parse(result);
                }
            });
        },
        provideDocumentColors: function (model, token) {
            var element = EditorContext.getElementFromModel(model);

            return callParentEventAsync(element, "ProvideDocumentColors" + languageId, []).then(result => {
                if (result) {
                    return JSON.parse(result);
                }
            });
        }
    });
}