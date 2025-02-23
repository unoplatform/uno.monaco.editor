using Monaco.Languages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Windows.Foundation;

namespace Monaco
{
    /// <summary>
    /// Helper to static Monaco.Languages Namespace methods.
    /// https://microsoft.github.io/monaco-editor/api/modules/monaco.languages.html
    /// </summary>
    public sealed class LanguagesHelper
    {
        private readonly WeakReference<CodeEditor> _editor;

        [Obsolete("Use <Editor Instance>.Languages.* instead of constructing your own LanguagesHelper.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public LanguagesHelper(CodeEditor editor) // TODO: Make Internal later.
        {
            // We need the editor component in order to execute JavaScript within 
            // the WebView environment to retrieve data (even though this Monaco class is static).
            _editor = new WeakReference<CodeEditor>(editor);
        }

        public async Task<IList<ILanguageExtensionPoint>?> GetLanguagesAsync()
        {
            if (_editor.TryGetTarget(out var editor))
            {
                return await editor.SendScriptAsync<IList<ILanguageExtensionPoint>>("monaco.languages.getLanguages()").AsAsyncOperation();
            }

            return null;
        }

        public async Task RegisterAsync(ILanguageExtensionPoint language)
        {
            if (_editor.TryGetTarget(out var editor))
            {
                await editor.InvokeScriptAsync("monaco.languages.register", language).AsAsyncAction();
            }
        }

        public async Task RegisterCodeActionProviderAsync(string languageId, CodeActionProvider provider)
        {
            if (_editor.TryGetTarget(out var editor))
            {
                // link:registerCodeActionProvider.ts:ProvideCodeActions
                editor._parentAccessor?.RegisterEvent("ProvideCodeActions" + languageId, async (args) =>
                {
                    if (args != null && args.Length >= 2)
                    {
                        var range = JsonConvert.DeserializeObject<Range>(args[0]);
                        var context = JsonConvert.DeserializeObject<CodeActionContext>(args[1]);

                        if (editor.GetModel() is { } model
                            && range is not null
                            && context is not null)
                        {
                            var list = await provider.ProvideCodeActionsAsync(model, range, context);

                            if (list != null)
                            {
                                return JsonConvert.SerializeObject(list);
                            }
                        }
                    }

                    return "";
                });

                // link:registerCodeActionProvider.ts:registerCodeActionProvider
                await editor.InvokeScriptAsync("registerCodeActionProvider", new object[] { languageId }).AsAsyncAction();
            }
        }

        public async Task RegisterCodeLensProviderAsync(string languageId, CodeLensProvider provider)
        {
            if (_editor.TryGetTarget(out var editor) && editor._parentAccessor is not null)
            {
                // link:registerCodeLensProvider.ts:ProvideCodeLenses
                editor._parentAccessor.RegisterEvent("ProvideCodeLenses" + languageId, async (args) =>
                {
                    if (editor.GetModel() is { } model)
                    {
                        var list = await provider.ProvideCodeLensesAsync(model);

                        if (list != null)
                        {
                            return JsonConvert.SerializeObject(list);
                        }
                    }

                    return "";
                });

                // link:registerCodeLensProvider.ts:ResolveCodeLens
                editor._parentAccessor.RegisterEvent("ResolveCodeLens" + languageId, async (args) =>
                {
                    if (args != null && args.Length >= 1)
                    {
                        if (editor.GetModel() is { } model
                            && JsonConvert.DeserializeObject<CodeLens>(args[0]) is { } codeLens)
                        {
                            var lens = await provider.ResolveCodeLensAsync(model, codeLens);

                            if (lens != null)
                            {
                                return JsonConvert.SerializeObject(lens);
                            }
                        }
                    }

                    return "";
                });

                // link:registerCodeLensProvider.ts:registerCodeLensProvider
                await editor.InvokeScriptAsync("registerCodeLensProvider", new object[] { languageId }).AsAsyncAction();
            }
        }

        public async Task RegisterColorProviderAsync(string languageId, DocumentColorProvider provider)
        {
            if (_editor.TryGetTarget(out var editor)
                && editor._parentAccessor is not null)
            {
                // link:registerColorProvider.ts:ProvideColorPresentations
                editor._parentAccessor.RegisterEvent("ProvideColorPresentations" + languageId, async (args) =>
                {
                    if (args != null && args.Length >= 1)
                    {
                        if (editor.GetModel() is { } model
                        && JsonConvert.DeserializeObject<ColorInformation>(args[0]) is { } colorInformation)
                        {
                            var items = await provider.ProvideColorPresentationsAsync(model, colorInformation);

                            if (items != null)
                            {
                                return JsonConvert.SerializeObject(items);
                            }
                        }
                    }

                    return "";
                });

                // link:registerColorProvider.ts:ProvideDocumentColors
                editor._parentAccessor.RegisterEvent("ProvideDocumentColors" + languageId, async (args) =>
                {
                    if (editor.GetModel() is { } model)
                    {
                        var items = await provider.ProvideDocumentColorsAsync(model);

                        if (items != null)
                        {
                            return JsonConvert.SerializeObject(items);
                        }
                    }

                    return "";
                });

                // link:registerColorProvider.ts:registerColorProvider
                await editor.InvokeScriptAsync("registerColorProvider", new object[] { languageId }).AsAsyncAction();
            }
        }

        public async Task RegisterCompletionItemProviderAsync(string languageId, CompletionItemProvider provider)
        {
            if (_editor.TryGetTarget(out var editor)
                && editor._parentAccessor is not null)
            {
                // TODO: Add Incremented Id so that we can register multiple providers per language?
                // link:registerCompletionItemProvider.ts:CompletionItemProvider
                editor._parentAccessor.RegisterEvent("CompletionItemProvider" + languageId, async (args) =>
                {
                    if (args != null && args.Length >= 2)
                    {
                        if (editor.GetModel() is { } model
                        && JsonConvert.DeserializeObject<Position>(args[0]) is { } position
                        && JsonConvert.DeserializeObject<CompletionContext>(args[1]) is { } completionContext)
                        {
                            var items = await provider.ProvideCompletionItemsAsync(model, position, completionContext);

                            if (items != null)
                            {
                                System.Diagnostics.Debug.WriteLine("Items: " + items);
                                var serialized = JsonConvert.SerializeObject(items);
                                System.Diagnostics.Debug.WriteLine("Items in JSON: " + serialized);
                                return serialized;
                            }
                        }
                    }

                    return "";
                });

                // link:registerCompletionItemProvider.ts:CompletionItemRequested
                editor._parentAccessor.RegisterEvent("CompletionItemRequested" + languageId, async (args) =>
                {
                    if (args != null && args.Length >= 1)
                    {
                        if (editor.GetModel() is { } model
                        && JsonConvert.DeserializeObject<CompletionItem>(args[0]) is { } requestedItem)
                        {
                            var completionItem = await provider.ResolveCompletionItemAsync(model, requestedItem);

                            if (completionItem != null)
                            {
                                return JsonConvert.SerializeObject(completionItem);
                            }
                        }
                    }

                    return "";
                });

                // link:registerCompletionItemProvider.ts:registerCompletionItemProvider
                await editor.InvokeScriptAsync("registerCompletionItemProvider", new object[] { languageId, provider.TriggerCharacters }).AsAsyncAction();
            }
        }

        public async Task RegisterHoverProviderAsync(string languageId, HoverProvider provider)
        {
            if (_editor.TryGetTarget(out var editor)
                && editor._parentAccessor is not null)
            {
                // Wrapper around Hover Provider to Monaco editor.
                // TODO: Add Incremented Id so that we can register multiple providers per language?
                editor._parentAccessor.RegisterEvent("HoverProvider" + languageId, async (args) =>
                {
                    System.Diagnostics.Debug.WriteLine($"Hover provider.......... {args!=null}");
                    if (args != null && args.Length >= 1)
                    {
                        if (editor.GetModel() is { } model
                        && JsonConvert.DeserializeObject<Position>(args[0]) is { } position)
                        {
                            var hover = await provider.ProvideHover(model, position);

                            if (hover != null)
                            {
                                return JsonConvert.SerializeObject(hover);
                            }
                        }
                    }

                    return string.Empty;
                });

                // link:otherScriptsToBeOrganized.ts:registerHoverProvider
                await editor.InvokeScriptAsync("registerHoverProvider", languageId).AsAsyncAction();
            }
        }
    }
}
