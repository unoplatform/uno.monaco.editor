import * as monaco from 'monaco-editor';
import { EditorContext } from './otherScriptsToBeOrganized';
import { callParentEventAsync } from './asyncCallbackHelpers';

/**
 * Monaco language-feature providers live on the page-global `monaco.languages` registry,
 * not on an editor instance. On WASM every CodeEditor shares one document -- and therefore
 * one Monaco module -- so each editor that registers a provider for a language adds another
 * entry to that registry, and Monaco concatenates the results of all of them. Four editors
 * meant four document-color providers all reporting the same range, which Monaco rendered as
 * four stacked colorpicker decorations on every line that had a color.
 *
 * The providers below resolve their owning editor from the model they are handed, so a single
 * registration already serves every editor on the page. Keeping exactly one registration per
 * (kind, language) preserves behaviour for the first editor and stops the duplication for the
 * ones that follow. On desktop each editor owns its own WebView (and its own module instance),
 * so the map never holds more than one entry there.
 */
const registrations = new Map<string, monaco.IDisposable>();

/**
 * Registers a provider, replacing any previous registration of the same kind for the same
 * language. The returned disposable removes the registration and forgets it.
 */
export function registerSingleProvider(
    kind: string,
    languageId: string,
    register: () => monaco.IDisposable): monaco.IDisposable {

    const key = `${kind}:${languageId}`;
    registrations.get(key)?.dispose();

    const disposable = register();
    registrations.set(key, disposable);

    return {
        dispose: () => {
            disposable.dispose();
            if (registrations.get(key) === disposable) {
                registrations.delete(key);
            }
        }
    };
}

/**
 * Routes a provider callback to the managed side of the editor that owns <paramref name="model"/>.
 * Returns null when the model belongs to no known editor -- Monaco may ask a provider about
 * models the component never created (peek views, the suggest widget's detail model, ...).
 */
export function callProviderEventAsync(
    model: monaco.editor.ITextModel,
    name: string,
    parameters: string[]): Promise<string | null> {

    const element = EditorContext.getElementFromModel(model);
    if (!element) {
        return Promise.resolve(null);
    }

    return callParentEventAsync(element, name, parameters);
}
