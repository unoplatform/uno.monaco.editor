# Delayed Initialization Implementation

## Problem
The `CodeEditor` control takes time to fully initialize. Its inner `CodeEditorPresenter` creates an instance of Monaco in a DOM node and then adds it to the main tree. Properties set before it's fully initialized don't take effect because the property changed callbacks check initialization status and return early.

## Solution
Implemented an event queue that stores property changed events from dependency properties until the `WebView_DOMContentLoaded` event handler is invoked (specifically in the `CodeEditorLoaded` method). After initialization and queue playback, further updates do not need to be queued and are processed immediately.

## Implementation Details

### Queue Infrastructure
Added to `CodeEditor.cs`:
- `_propertyChangeQueue`: A queue of `Func<Task>` representing property change actions
- `_queueLock`: A lock object for thread-safe queue operations

### Key Methods

#### `QueueOrExecutePropertyChange(Func<Task> action)`
- Checks if `_initialized` is true
- If initialized: executes the action immediately (fire and forget)
- If not initialized: queues the action for later replay

#### `ReplayQueuedPropertyChanges()`
- Called in `CodeEditorLoaded()` after setting `_initialized = true`
- Copies all queued actions outside the lock
- Executes each action sequentially, awaiting completion
- Includes error handling to prevent one failed action from blocking others

### Updated Properties
The following dependency property callbacks now use the queue mechanism:

1. **Text** - Calls `updateContent` script
2. **SelectedText** - Calls `updateSelectedContent` script
3. **Options** (via `Options_PropertyChanged`) - Calls `updateLanguage` and `updateOptions` scripts
4. **Decorations** - Calls `DeltaDecorationsHelperAsync`
   - Both the property callback and `Decorations_VectorChanged` use the queue
5. **Markers** - Calls `SetModelMarkersAsync`
   - Both the property callback and `Markers_VectorChanged` use the queue

### Initialization Flow

1. User creates `CodeEditor` and sets properties (e.g., `Text = "hello"`)
2. Property callbacks fire but editor is not initialized
3. Actions are queued in `_propertyChangeQueue`
4. `CodeEditorPresenter` loads and completes initialization
5. `CodeEditorLoaded()` is called
6. `_initialized` is set to `true`
7. `ReplayQueuedPropertyChanges()` is called
8. All queued property changes are executed in order
9. Subsequent property changes execute immediately

## Benefits

1. **Automatic**: No need to manually track which properties were set before initialization
2. **Comprehensive**: Works for all properties that use the queue mechanism
3. **Order-preserving**: Actions are replayed in the order they were queued
4. **Thread-safe**: Uses locks to prevent race conditions
5. **Error-resilient**: Errors in one action don't prevent others from executing

## DelayedInitControl Base Class

A `DelayedInitControl` base class is provided in `Helpers/DelayedInitControl.cs` as a reference implementation for other controls that may need similar functionality. However, `CodeEditor` implements the pattern directly because:

1. `CodeEditor` is sealed
2. Property change logic is in static dependency property callbacks
3. Direct implementation allows for more control and is more minimal

## Testing

To test the implementation:

1. Create a `CodeEditor` instance
2. Set properties immediately (e.g., `Text`, `CodeLanguage`, `Decorations`)
3. Add the editor to the visual tree
4. Wait for initialization to complete
5. Verify that all properties have taken effect in the Monaco editor

Example:
```csharp
var editor = new CodeEditor
{
    Text = "console.log('Hello, World!');",
    CodeLanguage = "javascript",
    ReadOnly = false
};

// Add decorations before initialization
editor.Decorations.Add(new ModelDeltaDecoration
{
    Range = new Range { StartLineNumber = 1, StartColumn = 1, EndLineNumber = 1, EndColumn = 10 },
    Options = new ModelDecorationOptions { InlineClassName = "highlight" }
});

// Add to visual tree - initialization will occur
MyContainer.Children.Add(editor);

// After initialization, all properties and decorations should be applied
```
