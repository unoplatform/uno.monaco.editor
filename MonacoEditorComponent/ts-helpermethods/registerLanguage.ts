import * as monaco from 'monaco-editor';

// The bridge invokes registered helpers as `fn(element, ...args)` on both desktop and
// WASM (InvokeMethodAsync prepends the editor element). monaco.languages.register itself
// takes only the language descriptor, so the first parameter here absorbs the unused
// element -- mirroring registerColorProvider and the other register* helpers. Calling the
// raw monaco.languages.register directly would pass the element as the descriptor and
// silently fail to register the language.
export const registerLanguage = function (unused: any, language: monaco.languages.ILanguageExtensionPoint) {
    monaco.languages.register(language);
}
