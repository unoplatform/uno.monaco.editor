import * as monaco from 'monaco-editor';
import { callProviderEventAsync, registerSingleProvider } from './providerRegistrations';

export const registerCodeLensProvider = function (unused: any, languageId: string) {
    return registerSingleProvider('codeLens', languageId, () => monaco.languages.registerCodeLensProvider(languageId, {
        provideCodeLenses: function (model, token) {
            return callProviderEventAsync(model, "ProvideCodeLenses" + languageId, []).then(result => {
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
            return callProviderEventAsync(model, "ResolveCodeLens" + languageId, [JSON.stringify(codeLens)]).then(result => {
                if (result) {
                    return JSON.parse(result);
                }
                return null;
            });
        }
    }));
}
