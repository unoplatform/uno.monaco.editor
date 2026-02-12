using System.ComponentModel;
using System.Linq;
using System.Text.Json;

using Monaco.Languages;
using Monaco.Serialization;


namespace Monaco
{
    /// <summary>
    /// Provides access to the <c>monaco.languages.*</c> registration APIs, including
    /// completion, hover, code-action, code-lens, and color providers.
    /// </summary>
    /// <remarks>
    /// Obtain an instance from <see cref="CodeEditor.Languages"/>. Do not construct directly.
    /// See <see href="https://microsoft.github.io/monaco-editor/typedoc/modules/languages.html">monaco.languages</see>.
    /// </remarks>
    [method: Obsolete("Use <Editor Instance>.Languages.* instead of constructing your own LanguagesHelper.")]
    [method: EditorBrowsable(EditorBrowsableState.Never)]
    public sealed partial class LanguagesHelper(CodeEditor editor)
    {
        private readonly WeakReference<CodeEditor> _editor = new(editor);

        /// <summary>
        /// Gets the list of registered language identifiers and their extension points.
        /// </summary>
        /// <returns>A list of <see cref="ILanguageExtensionPoint"/> instances, or
        /// <see langword="null"/> if the editor reference has been collected.</returns>
        /// <remarks>Wraps Monaco <c>languages.getLanguages</c>.</remarks>
        public async Task<IList<ILanguageExtensionPoint>?> GetLanguagesAsync()
        {
            if (_editor.TryGetTarget(out var editor))
            {
                return await editor.SendScriptAsync<IList<ILanguageExtensionPoint>>("monaco.languages.getLanguages()").AsAsyncOperation();
            }

            return null;
        }

        /// <summary>
        /// Registers a new language with Monaco.
        /// </summary>
        /// <param name="language">The language extension point describing the language to register.</param>
        /// <remarks>Wraps Monaco <c>languages.register</c>.</remarks>
        public async Task RegisterAsync(ILanguageExtensionPoint language)
        {
            if (_editor.TryGetTarget(out var editor))
            {
                await editor.InvokeScriptAsync("monaco.languages.register", language).AsAsyncAction();
            }
        }

        /// <summary>
        /// Registers a code action provider for the specified language.
        /// </summary>
        /// <param name="languageId">The language identifier (e.g., <c>"csharp"</c>).</param>
        /// <param name="provider">The provider implementation.</param>
        /// <remarks>Wraps Monaco <c>languages.registerCodeActionProvider</c>.</remarks>
        public async Task RegisterCodeActionProviderAsync(string languageId, CodeActionProvider provider)
        {
            if (_editor.TryGetTarget(out var editor))
            {
                // link:registerCodeActionProvider.ts:ProvideCodeActions
                editor._parentAccessor?.RegisterEvent("ProvideCodeActions" + languageId, async (args) =>
                {
                    if (args != null && args.Length >= 2)
                    {
                        var range = JsonSerializer.Deserialize(args[0], MonacoJsonContext.Default.Range);
                        var context = JsonSerializer.Deserialize(args[1], MonacoJsonContext.Default.CodeActionContext);

                        if (editor.GetModel() is { } model
                            && range is not null
                            && context is not null)
                        {
                            var list = await provider.ProvideCodeActionsAsync(model, range, context);

                            if (list != null)
                            {
                                return JsonSerializer.Serialize(list, MonacoJsonContext.Relaxed.CodeActionList);
                            }
                        }
                    }

                    return "";
                });

                // link:registerCodeActionProvider.ts:registerCodeActionProvider
                await editor.InvokeScriptAsync("registerCodeActionProvider", [languageId]).AsAsyncAction();
            }
        }

        /// <summary>
        /// Registers a code lens provider for the specified language.
        /// </summary>
        /// <param name="languageId">The language identifier (e.g., <c>"csharp"</c>).</param>
        /// <param name="provider">The provider implementation.</param>
        /// <remarks>Wraps Monaco <c>languages.registerCodeLensProvider</c>.</remarks>
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
                            return JsonSerializer.Serialize(list, MonacoJsonContext.Relaxed.CodeLensList);
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
                            && JsonSerializer.Deserialize(args[0], MonacoJsonContext.Default.CodeLens) is { } codeLens)
                        {
                            var lens = await provider.ResolveCodeLensAsync(model, codeLens);

                            if (lens != null)
                            {
                                return JsonSerializer.Serialize(lens, MonacoJsonContext.Relaxed.CodeLens);
                            }
                        }
                    }

                    return "";
                });

                // link:registerCodeLensProvider.ts:registerCodeLensProvider
                await editor.InvokeScriptAsync("registerCodeLensProvider", [languageId]).AsAsyncAction();
            }
        }

        /// <summary>
        /// Registers a document color provider for the specified language.
        /// </summary>
        /// <param name="languageId">The language identifier (e.g., <c>"css"</c>).</param>
        /// <param name="provider">The provider implementation.</param>
        /// <remarks>Wraps Monaco <c>languages.registerColorProvider</c>.</remarks>
        public async Task RegisterColorProviderAsync(string languageId, DocumentColorProvider provider)
        {
            if (_editor.TryGetTarget(out var editor)
                && editor._parentAccessor is not null)
            {
                Console.WriteLine($"Register color provider: {languageId}/{editor.GetHashCode():X8}");

                // link:registerColorProvider.ts:ProvideColorPresentations
                editor._parentAccessor.RegisterEvent("ProvideColorPresentations" + languageId, async (args) =>
                {
                    if (args != null && args.Length >= 1)
                    {
                        if (editor.GetModel() is { } model
                        && JsonSerializer.Deserialize(args[0], MonacoJsonContext.Default.ColorInformation) is { } colorInformation)
                        {
                            var items = await provider.ProvideColorPresentationsAsync(model, colorInformation);

                            if (items != null)
                            {
                                return JsonSerializer.Serialize(items.ToArray(), MonacoJsonContext.Relaxed.ColorPresentationArray);
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
                            return JsonSerializer.Serialize(items.ToArray(), MonacoJsonContext.Relaxed.ColorInformationArray);
                        }
                    }

                    return "";
                });

                // link:registerColorProvider.ts:registerColorProvider
                await editor.InvokeScriptAsync("registerColorProvider", [languageId]).AsAsyncAction();
            }
        }

        /// <summary>
        /// Registers a completion item provider for the specified language.
        /// </summary>
        /// <param name="languageId">The language identifier (e.g., <c>"javascript"</c>).</param>
        /// <param name="provider">The provider implementation.</param>
        /// <remarks>Wraps Monaco <c>languages.registerCompletionItemProvider</c>.</remarks>
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
                        && JsonSerializer.Deserialize(args[0], MonacoJsonContext.Default.Position) is { } position
                        && JsonSerializer.Deserialize(args[1], MonacoJsonContext.Default.CompletionContext) is { } completionContext)
                        {
                            var items = await provider.ProvideCompletionItemsAsync(model, position, completionContext);

                            if (items != null)
                            {
                                System.Diagnostics.Debug.WriteLine("Items: " + items);
                                var serialized = JsonSerializer.Serialize(items, MonacoJsonContext.Relaxed.CompletionList);
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
                        && JsonSerializer.Deserialize(args[0], MonacoJsonContext.Default.CompletionItem) is { } requestedItem)
                        {
                            var completionItem = await provider.ResolveCompletionItemAsync(model, requestedItem);

                            if (completionItem != null)
                            {
                                return JsonSerializer.Serialize(completionItem, MonacoJsonContext.Relaxed.CompletionItem);
                            }
                        }
                    }

                    return "";
                });

                // link:registerCompletionItemProvider.ts:registerCompletionItemProvider
                await editor.InvokeScriptAsync("registerCompletionItemProvider", [languageId, provider.TriggerCharacters]).AsAsyncAction();
            }
        }

        /// <summary>
        /// Registers a hover information provider for the specified language.
        /// </summary>
        /// <param name="languageId">The language identifier (e.g., <c>"typescript"</c>).</param>
        /// <param name="provider">The provider implementation.</param>
        /// <remarks>Wraps Monaco <c>languages.registerHoverProvider</c>.</remarks>
        public async Task RegisterHoverProviderAsync(string languageId, HoverProvider provider)
        {
            if (_editor.TryGetTarget(out var editor)
                && editor._parentAccessor is not null)
            {
                // Wrapper around Hover Provider to Monaco editor.
                // TODO: Add Incremented Id so that we can register multiple providers per language?
                editor._parentAccessor.RegisterEvent("HoverProvider" + languageId, async (args) =>
                {
                    System.Diagnostics.Debug.WriteLine($"Hover provider.......... {args != null}");
                    if (args != null && args.Length >= 1)
                    {
                        if (editor.GetModel() is { } model
                        && JsonSerializer.Deserialize(args[0], MonacoJsonContext.Default.Position) is { } position)
                        {
                            var hover = await provider.ProvideHover(model, position);

                            if (hover != null)
                            {
                                return JsonSerializer.Serialize(hover, MonacoJsonContext.Relaxed.Hover);
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
