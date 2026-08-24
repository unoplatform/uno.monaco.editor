import * as monaco from 'monaco-editor';
import { callProviderEventAsync, registerSingleProvider } from './providerRegistrations';

export const registerColorProvider = function (unused: any, languageId: string) {
    return registerSingleProvider('color', languageId, () => monaco.languages.registerColorProvider(languageId, {
        provideColorPresentations: function (model, colorInfo, token) {
            return callProviderEventAsync(model, "ProvideColorPresentations" + languageId, [JSON.stringify(colorInfo)]).then(result => {
                if (result) {
                    return JSON.parse(result);
                }
            });
        },
        provideDocumentColors: function (model, token) {
            return callProviderEventAsync(model, "ProvideDocumentColors" + languageId, []).then(result => {
                if (result) {
                    return JSON.parse(result);
                }
            });
        }
    }));
}
