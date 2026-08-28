/**
 * Multi-file diff view, built on Monaco's own multi-diff widget.
 *
 * ## Why the deep imports
 *
 * `monaco.editor.createMultiFileDiffEditor` exists (monaco.d.ts, returns `any`), but it is
 * unusable: Monaco's build tree-shakes at ShakeLevel.ClassMembers, so `MultiDiffEditorWidget`
 * keeps only its constructor -- no `setViewModel`, `createViewModel`, `layout` or `reveal` --
 * and it hardcodes `{}` as the `IWorkbenchUIElementFactory`, built eagerly in that same
 * constructor, so filename headers can never be populated through it.
 *
 * `MultiDiffEditorWidgetImpl` -- the class that actually renders -- survived intact, so this
 * module constructs it directly. Same widget, same DOM, same CSS (already in our bundle,
 * because the barrel pulls the widget in whether we use it or not); just reached one layer
 * lower so it can be given a resource-label factory.
 *
 * Nothing here is typed by monaco.d.ts and esbuild strips types without checking them, so a
 * broken deep import or a renamed private field fails only at runtime. `assertMonacoInternals`
 * below turns that into a loud error instead of an empty placeholder. See docs/architecture.md.
 */

import * as monaco from 'monaco-editor';

import { StandaloneServices } from 'monaco-editor/esm/vs/editor/standalone/browser/standaloneServices';
import { IStandaloneThemeService } from 'monaco-editor/esm/vs/editor/standalone/common/standaloneTheme';
import { MultiDiffEditorWidgetImpl } from 'monaco-editor/esm/vs/editor/browser/widget/multiDiffEditor/multiDiffEditorWidgetImpl';
import { MultiDiffEditorViewModel } from 'monaco-editor/esm/vs/editor/browser/widget/multiDiffEditor/multiDiffEditorViewModel';
import { RefCounted } from 'monaco-editor/esm/vs/editor/browser/widget/diffEditor/utils';
import { autorun, observableValue } from 'monaco-editor/esm/vs/base/common/observable';
import { Dimension } from 'monaco-editor/esm/vs/base/browser/dom';

import { EditorContext } from './otherScriptsToBeOrganized';

/** One file's two sides, as pushed from C#'s DiffFileEntry. */
export interface MultiDiffFile {
    /** Identity and primary header label. Must be unique within the control. */
    path: string;
    /** When set and different from `path`, the file renders as a rename. */
    originalPath?: string | null;
    /** `null` means the file was added: the original side is omitted entirely. `""` is an empty file. */
    originalText?: string | null;
    /** `null` means the file was deleted: the modified side is omitted entirely. `""` is an empty file. */
    modifiedText?: string | null;
    /** `null` infers the language from the file extension. */
    language?: string | null;
    collapsed?: boolean;
}

interface DocEntry {
    path: string;
    originalPath: string | null;
    hasOriginal: boolean;
    hasModified: boolean;
    original?: monaco.editor.ITextModel;
    modified?: monaco.editor.ITextModel;
    /** The IDocumentDiffItem handed to Monaco. Identity matters: the view model caches on it. */
    item: any;
    ref: any;
    /** Last `collapsed` value pushed from C#, so a user toggle is not clobbered by a re-push. */
    lastPushedCollapsed?: boolean;
}

interface MultiDiffState {
    contextId: string;
    element: any;
    impl: any;
    viewModel: any;
    dimObs: any;
    vmObs: any;
    documents: ValueWithChangeEvent<any[]>;
    docs: Map<string, DocEntry>;
    order: string[];
    /** Shared per-file diff options. Items expose it through a getter, so one object serves all. */
    itemOptions: any;
    fireOptionsChanged: () => void;
    onOptionsDidChange: (listener: () => void) => { dispose(): void };
    disposables: { dispose(): void }[];
    activeFilePath?: string;
    disposed: boolean;
}

interface ValueWithChangeEvent<T> {
    value: T;
    onDidChange: (listener: () => void) => { dispose(): void };
    set(next: T): void;
}

let _contextSeed = 0;

// ---------------------------------------------------------------------------
// Small primitives -- hand-rolled rather than deep-imported
// ---------------------------------------------------------------------------

function createEmitter() {
    const listeners = new Set<() => void>();
    return {
        event: (listener: () => void) => {
            listeners.add(listener);
            return { dispose: () => { listeners.delete(listener); } };
        },
        fire: () => { [...listeners].forEach(l => l()); },
    };
}

/**
 * The shape `MultiDiffEditorViewModel` expects for `model.documents`. It only ever reaches it
 * through `observableFromValueWithChangeEvent`, which reduces to
 * `observableFromEvent(owner, value.onDidChange, () => value.value)` -- so `{ value, onDidChange }`
 * is the whole contract. `ValueWithChangeEvent.const` does not exist in 0.52.2.
 */
function createValueWithChangeEvent<T>(initial: T): ValueWithChangeEvent<T> {
    const emitter = createEmitter();
    return {
        value: initial,
        onDidChange: emitter.event,
        set(next: T) { this.value = next; emitter.fire(); },
    };
}

/**
 * Dispose removed document refs only after the view model has processed the new list.
 *
 * Disposing in the same turn as the swap makes Monaco throw
 * "TextModel got disposed before DiffEditorWidget model got reset" and leaves the widget with
 * "no diff result available": the item's view model still holds the models at that point.
 * Two frames is what it takes for the derived chain to settle and release its refs.
 */
function deferDispose(refs: any[]): void {
    if (refs.length === 0) {
        return;
    }

    const run = () => {
        for (const ref of refs) {
            try {
                ref.dispose();
            } catch (err) {
                console.warn('[multiDiffEditor] Failed to dispose a document ref:', err);
            }
        }
    };

    if (typeof requestAnimationFrame === 'function') {
        requestAnimationFrame(() => requestAnimationFrame(run));
    } else {
        setTimeout(run, 0);
    }
}

/**
 * Fail loudly when a future Monaco bump tree-shakes something else away. Without this the
 * widget renders an empty "No Changed Files" placeholder and nothing throws, because the
 * public factory is typed `any`.
 */
function assertMonacoInternals(): void {
    const checks: [string, boolean][] = [
        ['MultiDiffEditorWidgetImpl', typeof MultiDiffEditorWidgetImpl === 'function'],
        ['MultiDiffEditorViewModel', typeof MultiDiffEditorViewModel === 'function'],
        ['RefCounted.create', typeof (RefCounted as any)?.create === 'function'],
        ['StandaloneServices.initialize', typeof (StandaloneServices as any)?.initialize === 'function'],
        ['observableValue', typeof observableValue === 'function'],
        ['Dimension', typeof Dimension === 'function'],
    ];

    const missing = checks.filter(([, ok]) => !ok).map(([name]) => name);
    if (missing.length > 0) {
        throw new Error(
            `[multiDiffEditor] Monaco multi-diff internals changed: ${missing.join(', ')} missing. ` +
            'See docs/architecture.md, "Multi-file diff".');
    }
}

// ---------------------------------------------------------------------------
// Models
// ---------------------------------------------------------------------------

/**
 * The original/modified discriminator goes in the URI *authority*, never the path.
 * `diffEditorItemTemplate` flags a rename whenever `originalUri.path !== modifiedUri.path`, so
 * a path-based discriminator makes every modified file render with an "R" badge.
 */
function modelUri(contextId: string, side: 'original' | 'modified', path: string): monaco.Uri {
    return monaco.Uri.from({
        scheme: 'multidiff',
        authority: `${contextId}-${side}`,
        path: path.startsWith('/') ? path : `/${path}`,
    });
}

function inferLanguage(file: MultiDiffFile): string {
    if (file.language) {
        return file.language;
    }

    const source = file.path ?? '';
    const dot = source.lastIndexOf('.');
    if (dot < 0) {
        return 'plaintext';
    }

    const langs = monaco.languages.getLanguages();
    const ext = source.substring(dot).toLowerCase();
    for (const l of langs) {
        if (l.extensions?.some(e => e.toLowerCase() === ext)) {
            return l.id;
        }
    }

    return 'plaintext';
}

/**
 * `monaco.editor.createModel` throws if a model with the same URI already exists, and stable
 * URIs are what let `DocumentDiffItemViewModel.getKey()` keep scroll and collapsed state across
 * a re-push -- so every create goes through a lookup first.
 */
function ensureModel(uri: monaco.Uri, text: string, language: string): monaco.editor.ITextModel {
    const existing = monaco.editor.getModel(uri);
    if (existing) {
        if (existing.getValue() !== text) {
            existing.setValue(text);
        }
        if (language && existing.getLanguageId() !== language) {
            monaco.editor.setModelLanguage(existing, language);
        }
        return existing;
    }

    return monaco.editor.createModel(text, language, uri);
}

function createEntry(state: MultiDiffState, file: MultiDiffFile): DocEntry {
    const language = inferLanguage(file);
    const originalPath = file.originalPath ?? null;
    const hasOriginal = file.originalText !== null && file.originalText !== undefined;
    const hasModified = file.modifiedText !== null && file.modifiedText !== undefined;

    const original = hasOriginal
        ? ensureModel(modelUri(state.contextId, 'original', originalPath ?? file.path), file.originalText!, language)
        : undefined;
    const modified = hasModified
        ? ensureModel(modelUri(state.contextId, 'modified', file.path), file.modifiedText!, language)
        : undefined;

    const entry: DocEntry = {
        path: file.path,
        originalPath,
        hasOriginal,
        hasModified,
        original,
        modified,
        item: undefined,
        ref: undefined,
    };

    // RefCounted.create uses the value itself as the disposable (there is no
    // createOfNonDisposable in 0.52.2), so this dispose() is the single place the models die.
    entry.item = {
        original,
        modified,
        get options() { return state.itemOptions; },
        onOptionsDidChange: state.onOptionsDidChange,
        dispose() {
            original?.dispose();
            modified?.dispose();
        },
    };
    entry.ref = (RefCounted as any).create(entry.item, 'uno-multi-diff');

    return entry;
}

/** Whether an existing entry can be updated in place, or has to be rebuilt. */
function isSameShape(entry: DocEntry, file: MultiDiffFile): boolean {
    return entry.originalPath === (file.originalPath ?? null)
        && entry.hasOriginal === (file.originalText !== null && file.originalText !== undefined)
        && entry.hasModified === (file.modifiedText !== null && file.modifiedText !== undefined);
}

// ---------------------------------------------------------------------------
// State lookup
// ---------------------------------------------------------------------------

function tryGetState(element: any): MultiDiffState | undefined {
    const context = EditorContext.tryGetEditorForElement(element) as any;
    return context?.multiDiff as MultiDiffState | undefined;
}

function viewModelItemFor(state: MultiDiffState, path: string): any {
    const entry = state.docs.get(path);
    if (!entry || !state.viewModel) {
        return undefined;
    }

    return state.viewModel.items.get().find((i: any) => i.documentDiffItem === entry.item);
}

// ---------------------------------------------------------------------------
// Public surface
// ---------------------------------------------------------------------------

/**
 * Replace the file list. Incremental by design: an entry whose path and shape are unchanged
 * keeps the *same* IDocumentDiffItem object, because `mapObservableArrayCached` caches by
 * identity -- which is what preserves each file's scroll offset and collapsed state.
 */
export const updateMultiDiffFiles = function (element: any, files: MultiDiffFile[] | string) {
    const state = tryGetState(element);
    if (!state || state.disposed) {
        return;
    }

    const parsed: MultiDiffFile[] = typeof files === 'string' ? JSON.parse(files) : (files ?? []);

    const seen = new Set<string>();
    const removed: any[] = [];
    const next: any[] = [];
    const order: string[] = [];

    for (const file of parsed) {
        if (!file || typeof file.path !== 'string' || file.path.length === 0 || seen.has(file.path)) {
            // Duplicate or unusable paths would collide on the model URI; the C# side documents
            // that paths must be unique, so the last one in wins nothing and is simply skipped.
            continue;
        }
        seen.add(file.path);

        let entry = state.docs.get(file.path);
        if (entry && isSameShape(entry, file)) {
            const language = inferLanguage(file);
            if (entry.original && entry.original.getValue() !== (file.originalText ?? '')) {
                entry.original.setValue(file.originalText ?? '');
            }
            if (entry.modified && entry.modified.getValue() !== (file.modifiedText ?? '')) {
                entry.modified.setValue(file.modifiedText ?? '');
            }
            for (const model of [entry.original, entry.modified]) {
                if (model && language && model.getLanguageId() !== language) {
                    monaco.editor.setModelLanguage(model, language);
                }
            }
        } else {
            if (entry) {
                removed.push(entry.ref);
            }
            entry = createEntry(state, file);
            state.docs.set(file.path, entry);
        }

        // Only follow the pushed value when it actually changed, so a user's collapse toggle
        // survives an unrelated text update.
        if (typeof file.collapsed === 'boolean' && file.collapsed !== entry.lastPushedCollapsed) {
            entry.lastPushedCollapsed = file.collapsed;
            const target = file.collapsed;
            queueMicrotask(() => {
                const item = viewModelItemFor(state, file.path);
                item?.collapsed.set(target, undefined);
            });
        }

        next.push(entry.ref);
        order.push(file.path);
    }

    for (const [path, entry] of [...state.docs]) {
        if (!seen.has(path)) {
            removed.push(entry.ref);
            state.docs.delete(path);
        }
    }

    state.order = order;
    state.documents.set(next);
    deferDispose(removed);
};

/** Apply diff options to every file. One shared object; the items expose it through a getter. */
export const updateMultiDiffOptions = function (element: any, options: any) {
    const state = tryGetState(element);
    if (!state || state.disposed) {
        return;
    }

    const parsed = typeof options === 'string' ? JSON.parse(options) : (options ?? {});

    // Read-only in v1: both sides are locked regardless of what the caller sent. The item
    // template forwards these to each DiffEditorWidget via updateOptions(), and only
    // hideUnchangedRegions/scrollbar/overview-ruler keys are overridden by Monaco itself.
    state.itemOptions = { ...parsed, readOnly: true, originalEditable: false };
    state.fireOptionsChanged();
};

/** Collapse or expand one file. */
export const setMultiDiffCollapsed = function (element: any, path: string, collapsed: boolean | string) {
    const state = tryGetState(element);
    if (!state || state.disposed) {
        return;
    }

    const value = collapsed === true || collapsed === 'true' || collapsed === 'True';
    const entry = state.docs.get(path);
    if (entry) {
        entry.lastPushedCollapsed = value;
    }
    viewModelItemFor(state, path)?.collapsed.set(value, undefined);
};

/** Collapse or expand every file. */
export const setAllMultiDiffCollapsed = function (element: any, collapsed: boolean | string) {
    const state = tryGetState(element);
    if (!state || state.disposed || !state.viewModel) {
        return;
    }

    const value = collapsed === true || collapsed === 'true' || collapsed === 'True';
    for (const entry of state.docs.values()) {
        entry.lastPushedCollapsed = value;
    }
    for (const item of state.viewModel.items.get()) {
        item.collapsed.set(value, undefined);
    }
};

/**
 * Scroll a file into view.
 *
 * `MultiDiffEditorWidget.reveal` was tree-shaken away, so the offset is recomputed here.
 * `MultiDiffEditorWidgetImpl.render` tracks two parallel accumulators -- one over
 * min(contentHeight, viewportHeight), one over the full contentHeight -- and it is the *second*
 * that the scroll position is measured against: `contentViewPort` is built from `scrollTop`, and
 * every visibility decision compares it to `itemContentRange`. `setScrollDimensions` agrees,
 * reporting `scrollHeight` as the sum of full content heights. So scroll offsets live in content
 * space; clamping each item to the viewport height here lands short by the overflow of every
 * preceding item.
 */
export const revealMultiDiffFile = function (element: any, path: string) {
    const state = tryGetState(element);
    if (!state || state.disposed || !state.impl) {
        return;
    }

    const index = state.order.indexOf(path);
    if (index < 0) {
        return;
    }

    const items = state.impl._viewItems.get();
    let scrollTop = 0;
    for (let i = 0; i < index && i < items.length; i++) {
        scrollTop += items[i].contentHeight.get();
    }

    state.impl._scrollableElement.setScrollPosition({ scrollTop });
};

/** Layout target handed to attachEditorRuntime, driven by its ResizeObserver. */
export const layoutMultiDiffEditor = function (state: MultiDiffState): void {
    if (state.disposed || !state.element) {
        return;
    }

    // The widget does NOT self-size: MultiDiffEditorWidgetImpl builds
    // ObservableElementSizeObserver(element, undefined) and never calls setAutomaticLayout(true),
    // so the ResizeObserver behind startObserving() never runs. A fresh Dimension has to be
    // pushed on every resize -- and it must be a new object, since re-setting an equal value
    // does not retrigger the autorun.
    state.dimObs.set(new Dimension(state.element.clientWidth, state.element.clientHeight), undefined);
};

export const disposeMultiDiffState = function (state: MultiDiffState): void {
    if (state.disposed) {
        return;
    }
    state.disposed = true;

    for (const d of state.disposables) {
        try { d.dispose(); } catch { /* teardown is best-effort */ }
    }
    state.disposables.length = 0;

    try { state.impl?.dispose(); } catch { /* teardown is best-effort */ }
    try { state.viewModel?.dispose(); } catch { /* teardown is best-effort */ }

    for (const entry of state.docs.values()) {
        try { entry.ref.dispose(); } catch { /* teardown is best-effort */ }
    }
    state.docs.clear();
    state.order = [];
};

/**
 * Build the widget and wire the observers that report back to C#.
 * Called from `initializeMonacoMultiDiffEditor` in asyncCallbackHelpers.
 */
export const createMultiDiffWidget = function (element: any, initialOptions: any): MultiDiffState {
    assertMonacoInternals();

    const services: any = (StandaloneServices as any).initialize({});

    // StandaloneThemeService only injects its <style class="monaco-colors"> -- which carries the
    // --vscode-* colour variables AND the runtime-generated codicon glyph rules -- when an editor
    // container is registered. Only createStandaloneEditor/DiffEditor/colorize* do that, and this
    // path calls none of them. Without this the widget renders completely unstyled: serif headers,
    // no syntax colours, no diff highlighting, and an invisible collapse chevron.
    // Note StandaloneServices.get, not services.get: the InstantiationService returned by
    // initialize() has createInstance/invokeFunction but no get(), so calling it there throws and
    // -- if that throw is swallowed -- leaves a widget that renders but is entirely unstyled.
    (StandaloneServices as any).get(IStandaloneThemeService).registerEditorContainer(element);

    const optionsEmitter = createEmitter();
    const state: MultiDiffState = {
        contextId: `uno-multidiff-${++_contextSeed}`,
        element,
        impl: undefined,
        viewModel: undefined,
        dimObs: observableValue('multiDiffDimension', undefined),
        vmObs: observableValue('multiDiffViewModel', undefined),
        documents: createValueWithChangeEvent<any[]>([]),
        docs: new Map<string, DocEntry>(),
        order: [],
        itemOptions: { ...(initialOptions ?? {}), readOnly: true, originalEditable: false },
        fireOptionsChanged: optionsEmitter.fire,
        onOptionsDidChange: optionsEmitter.event,
        disposables: [],
        disposed: false,
    };

    state.viewModel = new (MultiDiffEditorViewModel as any)({ documents: state.documents }, services);
    state.impl = services.createInstance(
        MultiDiffEditorWidgetImpl,
        element,
        state.dimObs,
        state.vmObs,
        { createResourceLabel: (container: HTMLElement) => createResourceLabel(container) });

    state.vmObs.set(state.viewModel, undefined);
    layoutMultiDiffEditor(state);

    if (!element.querySelector('.monaco-component.multiDiffEditor')) {
        throw new Error('[multiDiffEditor] The widget produced no DOM. See docs/architecture.md, "Multi-file diff".');
    }

    // The theme stylesheet carries both the --vscode-* colour variables and the runtime-generated
    // codicon glyph rules. Without it the widget still renders, but with serif headers, no syntax
    // colours, no diff highlighting and an invisible collapse chevron -- a failure that is obvious
    // on screen and invisible to every other assertion, so it is checked here.
    if (!document.querySelector('style.monaco-colors')) {
        throw new Error('[multiDiffEditor] The Monaco theme stylesheet was not injected; the widget would render unstyled. See docs/architecture.md, "Multi-file diff".');
    }

    return state;
};

/**
 * Our IWorkbenchUIElementFactory. Supplying one is the whole reason this module constructs
 * MultiDiffEditorWidgetImpl instead of calling createMultiFileDiffEditor, which hardcodes `{}`
 * and therefore leaves every filename header blank.
 */
function createResourceLabel(container: HTMLElement) {
    return {
        setUri(uri: monaco.Uri | undefined, options?: { strikethrough?: boolean }) {
            container.textContent = uri ? String(uri.path).replace(/^\//, '') : '';
            container.style.textDecoration = options?.strikethrough ? 'line-through' : '';
        },
        dispose() { /* the container is owned by the item template */ },
    };
}

/**
 * Report focus, collapse, and recomputed diffs back to C#. Called once the Accessor exists,
 * which is why it is separate from widget construction.
 */
export const attachMultiDiffObservers = function (state: MultiDiffState, editorContext: any): void {
    // Active file, from the focused item. Mirrors DiffCodeEditor's use of callAction: everything
    // goes through the already-allowlisted parentAccessor/callActionWithParameters, so the
    // multi-file control adds no new JSON-RPC method to the desktop bridge.
    state.disposables.push(autorun(reader => {
        const focused = state.viewModel.focusedDiffItem.read(reader);
        const path = focused ? pathForViewModelItem(state, focused) : undefined;
        if (path === state.activeFilePath) {
            return;
        }
        state.activeFilePath = path;
        editorContext.Accessor?.callActionWithParameters2('MultiDiffActiveFileChanged', [path ?? '']);
    }));

    // Collapse toggles originating in the UI.
    state.disposables.push(autorun(reader => {
        const items = state.viewModel.items.read(reader);
        for (const item of items) {
            const collapsed = item.collapsed.read(reader);
            const path = pathForViewModelItem(state, item);
            if (path === undefined) {
                continue;
            }
            const entry = state.docs.get(path);
            if (entry && entry.lastPushedCollapsed !== collapsed) {
                entry.lastPushedCollapsed = collapsed;
                editorContext.Accessor?.callActionWithParameters2(
                    'MultiDiffFileCollapsedChanged', [path, String(collapsed)]);
            }
        }
    }));

    // Any file's diff finishing a recomputation. Reuses the DiffUpdated action name, so the
    // managed side needs no new callback registration beyond the one DiffCodeEditor already has.
    state.disposables.push(autorun(reader => {
        const items = state.viewModel.items.read(reader);
        for (const item of items) {
            item.diffEditorViewModelRef.object.diff.read(reader);
        }
        editorContext.Accessor?.callAction('DiffUpdated');
    }));
};

function pathForViewModelItem(state: MultiDiffState, item: any): string | undefined {
    for (const [path, entry] of state.docs) {
        if (entry.item === item.documentDiffItem) {
            return path;
        }
    }
    return undefined;
}
