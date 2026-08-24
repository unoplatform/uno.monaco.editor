import * as monaco from 'monaco-editor';
import { callProviderEventAsync, registerSingleProvider } from './providerRegistrations';

function isTextEdit(edit: monaco.languages.IWorkspaceTextEdit | monaco.languages.IWorkspaceFileEdit): edit is monaco.languages.IWorkspaceTextEdit {
    return (edit as monaco.languages.IWorkspaceTextEdit).textEdit !== undefined;
}

export const registerCodeActionProvider = function (unused: any, languageId: string) {
    return registerSingleProvider('codeAction', languageId, () => monaco.languages.registerCodeActionProvider(languageId, {
        provideCodeActions: function (model, range, context, token) {
            return callProviderEventAsync(model, "ProvideCodeActions" + languageId, [JSON.stringify(range), JSON.stringify(context)]).then(result => {
                if (result) {
                    const list: monaco.languages.CodeActionList = JSON.parse(result);

                    // Need to add in the model.uri to any edits to connect the dots
                    if (list.actions &&
                        list.actions.length > 0) {
                        list.actions.forEach((action) => {
                            if (action.edit &&
                                action.edit.edits &&
                                action.edit.edits.length > 0) {
                                action.edit.edits.forEach((inneredit) => {
                                    if (isTextEdit(inneredit)) {
                                        inneredit.resource = model.uri;
                                    }
                                });
                            }
                        });
                    }

                    // Add dispose method for IDisposable that Monaco is looking for.
                    list.dispose = () => {};

                    return list;
                }
            });
        },
    }));
}
