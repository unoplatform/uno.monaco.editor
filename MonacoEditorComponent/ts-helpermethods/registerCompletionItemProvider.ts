import * as monaco from 'monaco-editor';
import { callProviderEventAsync, registerSingleProvider } from './providerRegistrations';

export const registerCompletionItemProvider = function (unused: any, languageId: string, characters: string[]) {
    return registerSingleProvider('completionItem', languageId, () => {
        // Monaco hands resolveCompletionItem an item but no model, so remember the model the
        // items were produced for -- that is the editor the resolve request has to go back to.
        let lastModel: monaco.editor.ITextModel | null = null;

        return monaco.languages.registerCompletionItemProvider(languageId, {
            triggerCharacters: characters,
            provideCompletionItems: function (model, position, context, token) {
                lastModel = model;

                return callProviderEventAsync(model, "CompletionItemProvider" + languageId, [JSON.stringify(position), JSON.stringify(context)]).then(result => {
                    if (result) {
                        const list: monaco.languages.CompletionList = JSON.parse(result);

                        // Add dispose method for IDisposable that Monaco is looking for.
                        list.dispose = () => { };

                        return list;
                    }
                });
            },
            resolveCompletionItem: function (item, token) {
                if (!lastModel) {
                    return undefined;
                }

                return callProviderEventAsync(lastModel, "CompletionItemRequested" + languageId, [JSON.stringify(item)]).then(result => {
                    if (result) {
                        return JSON.parse(result);
                    }
                });
            }
        });
    });
}
