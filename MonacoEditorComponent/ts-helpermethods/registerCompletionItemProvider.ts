///<reference path="../monaco-editor/monaco.d.ts" />

const registerCompletionItemProvider = function (element:any, languageId, characters) {
    var editorContext = EditorContext.getEditorForElement(element);

    return monaco.languages.registerCompletionItemProvider(languageId, {
        triggerCharacters: characters,
        provideCompletionItems: function (model, position, context, token) {
            return callParentEventAsync(element, "CompletionItemProvider" + languageId, [JSON.stringify(position), JSON.stringify(context)]).then(result => {
                if (result) {
                    const list: monaco.languages.CompletionList = JSON.parse(result);

                    // Add dispose method for IDisposable that Monaco is looking for.
                    list.dispose = () => { };

                    return list;
                }
            });
        },
        resolveCompletionItem: function (item, token) {
            return callParentEventAsync(element, "CompletionItemRequested" + languageId, [JSON.stringify(item)]).then(result => {
                if (result) {
                    return JSON.parse(result);
                }
            });
        }
    });
}