import * as monaco from 'monaco-editor';
import { EditorContext } from './otherScriptsToBeOrganized';

export const updateSelectedContent = function (element: any, content: string) {
    var editorContext = EditorContext.getEditorForElement(element);

    let selection = editorContext.editor.getSelection()!;

    // Need to ignore updates from us notifying of a change
    if (content != editorContext.model.getValueInRange(selection)) {
        editorContext.modifingSelection = true;
        let range = new monaco.Range(selection.startLineNumber, selection.startColumn, selection.endLineNumber, selection.endColumn);
        let op = { identifier: { major: 1, minor: 1 }, range, text: content, forceMoveMarkers: true };

        editorContext.model.pushEditOperations([], [op], null as any);

        // Update selection to new text.
        const newEndLineNumber = selection.startLineNumber + content.split('\r').length - 1;
        const newEndColumn = (selection.startLineNumber === selection.endLineNumber)
            ? selection.startColumn + content.length
            : content.length - content.lastIndexOf('\r');

        selection = selection.setEndPosition(newEndLineNumber, newEndColumn);
        selection = selection.setEndPosition(selection.endLineNumber, selection.endColumn);

        editorContext.modifingSelection = false;
        editorContext.editor.setSelection(selection);
    }
};
