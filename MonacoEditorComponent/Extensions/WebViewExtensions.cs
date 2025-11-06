using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Monaco.Extensions
{
    internal static class ICodeEditorPresenterExtensions
    {
        public static async Task RunScriptAsync(
            this ICodeEditorPresenter _view,
            string script,
            [CallerMemberName] string? member = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            await _view.RunScriptAsync<object>(script, member, file, line);
        }

        public static async Task<T?> RunScriptAsync<T>(
            this ICodeEditorPresenter _view,
            string script,
            [CallerMemberName] string? member = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            var fullscript = script;

            try
            {
                return await RunScriptHelperAsync<T>(_view, fullscript);
            }
            catch (Exception e)
            {
                throw new JavaScriptExecutionException(member, file, line, script, e);
            }
        }

        private static async Task<T?> RunScriptHelperAsync<T>(ICodeEditorPresenter _view, string script)
        {
            var returnstring = NativeMethods.InvokeJS(_view.ElementId, script);

            // TODO: Need to decode the error correctly
            if (returnstring.Contains("wv_internal_error"))
            {
                throw new JavaScriptInnerException(returnstring, "");
            }

            if (!string.IsNullOrEmpty(returnstring) && returnstring != "\"\"" && returnstring != "null")
            {
                return JsonConvert.DeserializeObject<T>(returnstring);
            }

            return default;
        }

        private static readonly JsonSerializerSettings _settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static async Task InvokeScriptAsync(
            this ICodeEditorPresenter _view,
            string method,
            object arg,
            bool serialize = true,
            [CallerMemberName] string? member = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            await _view.InvokeScriptAsync<object>(method, arg, serialize, member, file, line);
        }

        public static async Task<object?> InvokeScriptAsync(
            this ICodeEditorPresenter _view,
            string method,
            object[] args,
            bool serialize = true,
            [CallerMemberName] string? member = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            return await _view.InvokeScriptAsync<object>(method, args, serialize, member, file, line);
        }

        public static async Task<T?> InvokeScriptAsync<T>(
            this ICodeEditorPresenter _view,
            string method,
            object arg,
            bool serialize = true,
            [CallerMemberName] string? member = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            return await _view.InvokeScriptAsync<T>(method, [arg], serialize, member, file, line);
        }

        public static async Task<T?> InvokeScriptAsync<T>(
            this ICodeEditorPresenter _view,
            string method,
            object?[] args,
            bool serialize = true,
            [CallerMemberName] string? member = null,
            [CallerFilePath] string? file = null,
            [CallerLineNumber] int line = 0)
        {
            string?[] sanitizedargs;

            try
            {
                System.Diagnostics.Debug.WriteLine($"Begin invoke script (serialize - {serialize})");
                if (serialize)
                {
                    sanitizedargs = [.. args.Select(item =>
                    {
                        if (item is int || item is double)
                        {
                            return item.ToString();
                        }
                        else if (item is string)
                        {
                            return JsonConvert.ToString(item);
                        }
                        else
                        {
                            // TODO: Need JSON.parse?
                            return JsonConvert.SerializeObject(item, _settings);
                        }
                    })];
                }
                else
                {
                    sanitizedargs = [.. args.Select(item => item?.ToString())];
                }

                var script = method + "(element," + string.Join(",", sanitizedargs) + ");";

                System.Diagnostics.Debug.WriteLine($"Script {script})");


                return await RunScriptAsync<T>(_view, script, member, file, line);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error {ex.Message} {ex.StackTrace} {ex.InnerException?.Message})");
                return default;
            }
        }
    }

    internal sealed class JavaScriptExecutionException(string? member, string? filename, int line, string? script, Exception inner) : Exception("Error Executing JavaScript Code for " + member + "\nLine " + line + " of " + filename + "\n" + script + "\n", inner)
    {
        public string? Script { get; private set; } = script;

        public string? Member { get; private set; } = member;

        public string? FileName { get; private set; } = filename;

        public int LineNumber { get; private set; } = line;
    }

    internal sealed class JavaScriptInnerException(string message, string stack) : Exception(message)
    {
        public string JavaScriptStackTrace { get; private set; } = stack;
    }

    internal partial class NativeMethods
    {
        [JSImport("globalThis.InvokeJS")]
        public static partial string InvokeJS(string elementId, string script);

        [JSImport("globalThis.languageIdFromFileName")]
        public static partial string LanguageIdFromExtension(string? extension);
    }
}
