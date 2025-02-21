using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace Monaco.Editor
{
    /// <summary>
    /// Helper to access IModel interface methods off of CodeEditor object.
    /// https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.imodel.html
    /// https://microsoft.github.io/monaco-editor/api/interfaces/monaco.editor.itextmodel.html
    /// </summary>
    public sealed class ModelHelper : IModel
    {
        private readonly WeakReference<CodeEditor> _editor;

        public ModelHelper(CodeEditor editor)
        {
            _editor = new WeakReference<CodeEditor>(editor);
        }

        public string Id => throw new NotImplementedException();

        public Uri Uri => throw new NotImplementedException();

        public IAsyncAction DetectIndentationAsync(bool defaultInsertSpaces, bool defaultTabSize)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.InvokeScriptAsync("EditorContext.getEditorForElement(element).model.detectIndentationAsync", new object[] { defaultInsertSpaces, defaultTabSize }).AsAsyncAction();
            }

            return null;
        }

        public IAsyncOperation<IEnumerable<FindMatch>> FindMatchesAsync(string searchString, bool searchOnlyEditableRange, bool isRegex, bool matchCase, string wordSeparators, bool captureMatches)
        {
            // Default limit results: https://github.com/microsoft/vscode/blob/b2d0292a20c4a012005c94975019a5b572ce6a63/src/vs/editor/common/model/textModel.ts#L117
            return FindMatchesAsync(searchString, searchOnlyEditableRange, isRegex, matchCase, wordSeparators, captureMatches, 999);
        }

        public IAsyncOperation<IEnumerable<FindMatch>> FindMatchesAsync(string searchString, IRange searchScope, bool isRegex, bool matchCase, string wordSeparators, bool captureMatches)
        {
            // Default limit results: https://github.com/microsoft/vscode/blob/b2d0292a20c4a012005c94975019a5b572ce6a63/src/vs/editor/common/model/textModel.ts#L117
            return FindMatchesAsync(searchString, searchScope, isRegex, matchCase, wordSeparators, captureMatches, 999);
        }

        public IAsyncOperation<IEnumerable<FindMatch>> FindMatchesAsync(string searchString, bool searchOnlyEditableRange, bool isRegex, bool matchCase, string wordSeparators, bool captureMatches, double limitResultCount)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.InvokeScriptAsync<IEnumerable<FindMatch>>("EditorContext.getEditorForElement(element).model.findMatches", new object[] { searchString, searchOnlyEditableRange, isRegex, matchCase, wordSeparators, captureMatches, limitResultCount }).AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<IEnumerable<FindMatch>> FindMatchesAsync(string searchString, IRange searchScope, bool isRegex, bool matchCase, string wordSeparators, bool captureMatches, double limitResultCount)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.InvokeScriptAsync<IEnumerable<FindMatch>>("EditorContext.getEditorForElement(element).model.findMatches", new object[] { searchString, searchScope, isRegex, matchCase, wordSeparators, captureMatches, limitResultCount }).AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<FindMatch> FindNextMatchAsync(string searchString, IPosition searchStart, bool isRegex, bool matchCase, string wordSeparators, bool captureMatches)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.InvokeScriptAsync<FindMatch>("EditorContext.getEditorForElement(element).model.findNextMatch", new object[] { searchString, searchString, isRegex, matchCase, wordSeparators, captureMatches }).AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<FindMatch> FindPreviousMatchAsync(string searchString, IPosition searchStart, bool isRegex, bool matchCase, string wordSeparators, bool captureMatches)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.InvokeScriptAsync<FindMatch>("EditorContext.getEditorForElement(element).model.findPreviousMatch", new object[] { searchString, searchString, isRegex, matchCase, wordSeparators, captureMatches }).AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<uint> GetAlternativeVersionIdAsync()
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getAlternativeVersionId();").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<string> GetEOLAsync()
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.getEOL();").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<Range> GetFullModelRangeAsync()
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<Range>("EditorContext.getEditorForElement(element).model.getFullModelRange();").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<string> GetLineContentAsync(uint lineNumber)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.getLineContent(" + lineNumber + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<uint> GetLineCountAsync()
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getLineCount();").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<uint> GetLineFirstNonWhitespaceColumnAsync(uint lineNumber)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getLineFirstNonWhitespaceColumn(" + lineNumber + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<uint> GetLineLastNonWhitespaceColumnAsync(uint lineNumber)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getLineLastNonWhitespaceColumn(" + lineNumber + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<uint> GetLineLengthAsync(uint lineNumber)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getLineLength(" + lineNumber + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<uint> GetLineMaxColumnAsync(uint lineNumber)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getLineMaxColumn(" + lineNumber + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<uint> GetLineMinColumnAsync(uint lineNumber)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getLineMinColumn(" + lineNumber + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<IEnumerable<string>> GetLinesContentAsync()
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<IEnumerable<string>>("EditorContext.getEditorForElement(element).model.getLinesContent();").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<string> GetModelIdAsync()
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.getModelId();").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<uint> GetOffsetAtAsync(IPosition position)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getOffsetAt(" + JsonConvert.SerializeObject(position) + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<string> GetOneIndentAsync()
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.getOneIndent();").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<Position> GetPositionAtAsync(uint offset)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<Position>("EditorContext.getEditorForElement(element).model.getPositionAt(" + offset + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<string> GetValueAsync()
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.getValue();").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<string> GetValueAsync(EndOfLinePreference eol)
        {
            throw new NotImplementedException();
        }

        public IAsyncOperation<string> GetValueAsync(EndOfLinePreference eol, bool preserveBOM)
        {
            throw new NotImplementedException();
        }

        public IAsyncOperation<string> GetValueInRangeAsync(IRange range)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.getValueInRange(" + JsonConvert.SerializeObject(range) + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<string> GetValueInRangeAsync(IRange range, EndOfLinePreference eol)
        {
            throw new NotImplementedException();
        }

        public IAsyncOperation<uint> GetValueLengthAsync()
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getValueLength();").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<uint> GetValueLengthAsync(EndOfLinePreference eol)
        {
            throw new NotImplementedException();
        }

        public IAsyncOperation<uint> GetValueLengthAsync(EndOfLinePreference eol, bool preserveBOM)
        {
            throw new NotImplementedException();
        }

        public IAsyncOperation<uint> GetValueLengthInRangeAsync(IRange range)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getValueLengthInRange(" + JsonConvert.SerializeObject(range) + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<uint> GetVersionIdAsync()
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<uint>("EditorContext.getEditorForElement(element).model.getVersionId();").AsAsyncOperation();
            }

            return null;
        }

        // TODO: Need to investigate why with .NET Native the InterfaceToClassConverter isn't working anymore?
        public IAsyncOperation<WordAtPosition> GetWordAtPositionAsync(IPosition position)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<WordAtPosition>("EditorContext.getEditorForElement(element).model.getWordAtPosition(" + JsonConvert.SerializeObject(position) + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<WordAtPosition> GetWordUntilPositionAsync(IPosition position)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<WordAtPosition>("EditorContext.getEditorForElement(element).model.getWordUntilPosition(" + JsonConvert.SerializeObject(position) + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<Position> ModifyPositionAsync(IPosition position, int number)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<Position>("EditorContext.getEditorForElement(element).model.modifyPosition(" + JsonConvert.SerializeObject(position) + ", " + number + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<string> NormalizeIndentationAsync(string str)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<string>("EditorContext.getEditorForElement(element).model.normalizeIndentations(JSON.parse(" + JsonConvert.ToString(str) + "));").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncAction PushStackElementAsync()
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync("EditorContext.getEditorForElement(element).model.pushStackElement();").AsAsyncAction();
            }

            return null;
        }

        public IAsyncAction SetEOLAsync(EndOfLineSequence eol)
        {
            throw new NotImplementedException();
        }

        public IAsyncAction SetValue(string newValue)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync("EditorContext.getEditorForElement(element).model.setValue(JSON.parse(" + JsonConvert.ToString(newValue) + "));").AsAsyncAction();
            }

            return null;
        }

        public IAsyncOperation<Position> ValidatePositionAsync(IPosition position)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<Position>("EditorContext.getEditorForElement(element).model.validatePosition(" + JsonConvert.SerializeObject(position) + ");").AsAsyncOperation();
            }

            return null;
        }

        public IAsyncOperation<Range> ValidateRangeAsync(IRange range)
        {
            if (_editor.TryGetTarget(out CodeEditor editor))
            {
                return editor.SendScriptAsync<Range>("EditorContext.getEditorForElement(element).model.validateRange(" + JsonConvert.SerializeObject(range) + ");").AsAsyncOperation();
            }

            return null;
        }
    }
    }
