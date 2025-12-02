///<reference path="../monaco-editor/monaco.d.ts" />

const registerCodeLensProvider = function (unused: any, languageId) {
    return monaco.languages.registerCodeLensProvider(languageId, {
        provideCodeLenses: function (model, token) {
            var element = EditorContext.getElementFromModel(model);
            return callParentEventAsync(element, "ProvideCodeLenses" + languageId, []).then(result => {
                if (result) {
                    const list: monaco.languages.CodeLensList = JSON.parse(result);

                    // Add dispose method for IDisposable that Monaco is looking for.
                    list.dispose = () => {};

                    return list;
                }
                return null;

            });
        },
        resolveCodeLens: function (model, codeLens, token) {
            var element = EditorContext.getElementFromModel(model);
            return callParentEventAsync(element, "ResolveCodeLens" + languageId, [JSON.stringify(codeLens)]).then(result => {
                if (result) {
                    return JSON.parse(result);
                }
                return null;
            });
        }
        // TODO: onDidChange, don't know what this does.
    });
}