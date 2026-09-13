# Re-applied documents are never tokenized on a continuously rendering host

A document handed to an existing editor via `updateLanguage` + `updateContent` could stay permanently
untokenized on a WebAssembly host whose main thread paints every frame. The model reported the correct
language and the correct content, the grammar was registered, the model was attached — and the view
painted only the default token class.

The **first** load into a given editor was unaffected, which is what made this look like a
"re-open" bug in consuming applications rather than a tokenization bug.

**Fixed.** `updateContent`, `updateLanguage`, `updateOriginalContent` and `updateOriginalLanguage` now
tokenize the visible range themselves — see [The fix](#the-fix). This document is kept because the
mechanism is subtle, the two tests that guard it will look arbitrary without it, and the same trap is
waiting in any future helper that assigns to a model.

---

## Symptom

- Painted spans carried only `mtk1` (the theme's default foreground).
- Brackets and braces were still coloured, because bracket-pair colorization is delivered as
  decorations (`bracket-highlighting-<n>` co-classes) and is computed independently of the tokenizer.
- Consequence: a C-family document looked *partially* highlighted — punctuation coloured, keywords and
  type names flat. An XML/XAML document looked completely flat, because it has no bracket pairs to
  decorate. The two symptoms have one cause and are easy to mistake for two different bugs.
- `model.getLanguageId()` returned the right language throughout, so every model-level assertion
  passed while the page rendered as plain text.

### Measured state

Collected from a focused browser window running a WASM consumer, with the document visibly unhighlighted:

```json
{
  "hostCount": 1,
  "liveHost": "monaco-1027174264",
  "box": "1383x817",
  "dpr": 1,
  "win": "1920x919",
  "viewLines": 27,
  "classes": "mtk1",
  "colours": "rgb(212, 212, 212) rgb(218, 112, 214) rgb(23, 159, 255) rgb(255, 215, 0)",
  "mtkRules": 24,
  "lang": "csharp",
  "len": "628",
  "theme": "monaco-editor no-user-select  showUnused showDeprecated vs-dark"
}
```

`rgb(212,212,212)` is `.mtk1`; the other three are the default bracket-pair colours. `mtkRules: 24`
shows the theme stylesheet was fully present, so this was not a CSS-delivery problem — the tokens were
simply never computed.

---

## Cause

It takes **two** mechanisms to produce this, which is why it looked so selective. Monaco has two ways
to get a line tokenized, and a same-viewport re-apply misses both.

### 1. The background tokenizer is gated on idle time

`setModelLanguage` and `setValue` do not tokenize. They flush the token store and *queue* the
background tokenizer, which is scheduled on an idle callback. From
`vs/editor/common/model/textModelTokens.js` (Monaco 0.52.2):

```js
_beginBackgroundTokenization() {
    if (this._isScheduled
        || !this._tokenizerWithStateStore._textModel.isAttachedToEditor()
        || !this._hasLinesToTokenize()) {
        return;
    }
    this._isScheduled = true;
    runWhenGlobalIdle((deadline) => {
        this._isScheduled = false;
        this._backgroundTokenizeWithDeadline(deadline);
    });
}
```

`runWhenGlobalIdle` resolves to `globalThis.requestIdleCallback` when the API exists. A host that
repaints its canvas on every animation frame — and owns the main thread while the window is focused —
need never present an idle period, so the queued callback can remain pending indefinitely. `_isScheduled`
stays `true`, and the only two places that clear it are the constructor and the callback itself, so
nothing re-queues either. Until the next flush, that model has no background pass at all.

### 2. The viewport refresh is gated on the visible range *changing*

Monaco's second path is not idle-driven, which is why the bug is not universal.
`TokenizationTextModelPart` keeps an `AttachedViewHandler` per attached view that refreshes the tokens
for the lines that view can see, on a 50 ms `RunOnceScheduler` — a plain `setTimeout`, never starved.
But from `vs/editor/common/model/tokens.js`:

```js
update() {
    if (equals(this._computedLineRanges, this._lineRanges, (a, b) => a.equals(b))) {
        return;
    }
    this._computedLineRanges = this._lineRanges;
    this._refreshTokens();
}
```

It runs only when the visible line range differs from the one it last handled. `resetTokenization`
flushes the token store but leaves `_computedLineRanges` alone, so:

| Situation | Visible range | Outcome |
|---|---|---|
| First document into a fresh editor | `1..1` → `1..27` | changed → refreshed → **highlighted** |
| Same-shaped document re-applied | `1..27` → `1..27` | unchanged → skipped → **flat** |
| Scrolling into unseen lines | `1..27` → `212..237` | changed → refreshed → **highlighted** |
| Resizing the host, width or height | irrelevant | the event never fires → **flat** |

That table is the whole bug. Both gates have to close on the same document for it to stay flat, and a
re-applied payload at an unchanged scroll position is exactly the case that closes both.

### Why the workarounds users found are diagnostic

| Action | Effect | Why |
|---|---|---|
| Minimize and restore the window | **Repairs it** | The animation-frame loop stops, the main thread goes idle, the queued callback finally runs |
| Switch browser tabs and back | **Repairs it** | Same cause |
| Scroll away and back | **Repairs it** | Changes the visible range, so the attached-view refresh runs — with heuristically guessed start states, so the result can differ from an accurate pass |
| Resize the window | **No effect** | Fires no visible-range change and creates no idle period |

That resize does *not* help is the useful half: it rules out layout, viewport and sizing, and points
at the model's token state rather than the view.

---

## Reproduction

Two conditions, both required:

1. **Suspend the idle callback.** A CDP/Playwright-driven window is unfocused and therefore throttled,
   so it always has idle time and the background tokenizer always runs. Twenty-two open/close/re-open
   cycles across five driver variants — alternating grammars, dwell times from 0 to 12 s, documents
   from 31 to 7805 lines, host heights of 320 px and 900 px, and every ordering of hiding/showing the
   host element — were all clean. Deliberately occupying the main thread with `setInterval` (42 ms of
   every 50 ms) also failed to reproduce it; short bursts still leave gaps the browser will use for
   idle work.

   ```js
   const saved = globalThis.requestIdleCallback;
   globalThis.requestIdleCallback = function () { return 0; };
   // … apply the payload and assert, then restore in a finally …
   ```

   Monaco's idle helper picks its implementation once, in an IIFE at bundle load, and the branch it
   picks when the API exists reads `globalThis.requestIdleCallback` on every call — so replacing it at
   runtime does take effect. (When the API is *missing* at bundle load it takes a `setTimeout` branch
   that is never starved, which is why the tests assert the API is present before trusting a pass.)

2. **Hold the visible line range still.** Apply the document once and let it settle, then re-apply the
   same document without scrolling. Without this the re-apply changes the range and the attached-view
   refresh repairs it, and the reproduction goes green whether the fix is present or not.

Then read the painted classes:

```js
[...new Set([...host.querySelectorAll('.view-line span[class*="mtk"]')]
  .map(s => (s.className.match(/mtk\d+/) || [''])[0]).filter(Boolean))].sort().join(',')
```

Expected `mtk1,mtk20,mtk6`-style set; observed `mtk1`.

Both conditions are encoded in `MonacoEditorComponent.Tests/TokenizationStarvationCases.cs`, driving
`WasmIntegrationTests.ReappliedDocument_IsTokenizedWithoutIdleTime` (plain editor) and
`ReappliedOriginalDocument_IsTokenizedWithoutIdleTime` (the diff widget's original pane, which starves
independently). Reverting either half of the fix turns the matching test red on `'mtk1'`.

---

## Diagnostic snippet

Run in the DevTools console while a document is visibly unhighlighted. It reports which repair works,
which identifies the failure precisely. Note that step 1 triggers a lazy grammar import if one is
pending, so its result must be read as "was the grammar loaded when I looked".

```js
(async () => {
  const H = [...document.querySelectorAll('[id^="monaco-"]')]
    .filter(e => !e.hidden && e.clientHeight > 0)[0];
  if (!H) { console.log('no live host'); return; }
  const cx = globalThis.EditorContext && EditorContext.getEditorForElement(H);
  const M = cx.model, E = cx.editor;
  const P = () => [...new Set([...H.querySelectorAll('.view-line span[class*="mtk"]')]
    .map(s => (s.className.match(/mtk\d+/) || [''])[0]).filter(Boolean))].sort().join(',');
  const W = ms => new Promise(r => setTimeout(r, ms));
  const O = ['lang=' + M.getLanguageId() + ' attached=' + M.isAttachedToEditor()];
  O.push('0. painted now            : ' + P());
  O.push('1. grammar token types    : ' + [...new Set(monaco.editor
    .tokenize('public sealed class A { int x = 1; }', M.getLanguageId())
    .flat().map(t => t.type))].filter(t => t !== '').length);
  M.tokenization.forceTokenization(Math.min(M.getLineCount(), 40));
  await W(700); O.push('2. after forceTokenization: ' + P());
  monaco.editor.setModelLanguage(M, M.getLanguageId());
  await W(1400); O.push('3. after re-set language  : ' + P());
  E.render(true);
  await W(700); O.push('4. after forced render    : ' + P());
  console.log(O.join('\n'));
})()
```

On an affected host: `attached=true`, the grammar reports 8 token types, `0.` is `mtk1`, and `2.`
repairs it. That combination is the signature — the grammar and the model are both fine and only the
tokenization pass is missing. `model.tokenization.hasAccurateTokensForLine(n)` is the same answer
without touching the DOM.

---

## The fix

`ts-helpermethods/tokenization.ts` exports `tokenizeVisibleRange(model, editor)`, called by all four
helpers that assign to a model: `updateContent`, `updateLanguage`, `updateOriginalContent` and
`updateOriginalLanguage`. It forces tokenization through the last visible line, which is what
`monaco.editor.create()` already does for a first load — so an assigned document is highlighted by the
time it is painted, whatever the host's idle behaviour.

The bounds are the interesting part, since `forceTokenization` is synchronous and runs on the single
WebAssembly UI thread:

| Bound | Value | Why |
|---|---|---|
| Look-ahead | last visible line + 20 | A small scroll or line-height change should not immediately expose flat text |
| Floor | 100 lines | An editor with no layout yet reports no visible range at all; and a host that arrives small and *grows* gets no attached-view refresh from the resize, so it needs headroom. A viewport line measured ~19 px against this bundle's defaults, so 100 lines is roughly a full-screen editor's worth — proportionally less if a consumer raises `fontSize` or `lineHeight`. It is headroom, not a guarantee |
| Ceiling | 2000 lines | A collapsed or unlaid-out host can report a visible range spanning hundreds of lines, so the viewport is not a bound that can be trusted on its own |

Repeated calls are cheap: `forceTokenization` stops at the first already-valid line, and a pass that
computes nothing fires no token-change event. The call is therefore unconditional, including when
`updateContent` finds the content already in place — a language push that arrived just before it has
flushed the store either way.

`model.tokenization` is public at runtime but absent from `monaco.d.ts`, so the helper checks for it
and for `forceTokenization` before calling, and swallows a throw with a warning. A Monaco upgrade that
moves it degrades to today's behaviour instead of breaking every content push.

### Deliberately not covered

- **Scrolling past the tokenized range.** Monaco's attached-view refresh handles it on a `setTimeout`,
  not on idle, so it is not starved. Its tokens are heuristic rather than accurate when the viewport
  is far ahead of the tokenized prefix; adding a synchronous pass on every scroll event would cost the
  whole prefix on the UI thread to improve on that, which is not a trade worth making here.
- **`updateMultiDiffFiles`.** The multi-file diff has the same class of exposure, but its per-file
  editors are pooled and recycled and its render autorun has previously been wedged by well-meaning
  synchronous work. Left alone on purpose.
- **Typing into a starved model.** Edits are not flush changes, so they do not re-create the
  background tokenizer, and `_isScheduled` is still latched. The token store shifts existing tokens
  across the edit rather than clearing them, so the line degrades gradually rather than going flat.

### Related code paths reviewed while diagnosing

- `CodeEditor/CodeEditor.cs` (~L294) `Options_PropertyChanged` — the language reaches JS from here via
  `updateLanguage`, and only when `_initialized && _view != null`. On a cold editor the guard skips it
  and the language instead arrives as a construction option, which is a second reason first loads and
  later assignments behave differently.
- `CodeEditor/WasmCodeEditorPresenter.cs` (~L142) `InvokeScriptAsync` returns `Task.FromResult`, so the
  language/content assignment order is preserved synchronously on WebAssembly. Assignment ordering is
  *not* a contributing factor here.
- `CodeEditor/CodeEditor.cs` `DeferredTeardownAsync` — not involved: closing a host that merely
  collapses its container never raises `Unloaded`, so no teardown runs and subscriptions stay intact.

## Ruled out

Each of these was measured, not reasoned away:

| Hypothesis | Why it is not the cause |
|---|---|
| Lazy Monarch grammar import | Real and reproducible (a cold grammar paints `mtk1` for ~7–9 s) but it **self-repairs**, so it cannot persist. Grammars are bundled, so there is no chunk fetch to fail. |
| Theme stylesheet lost | `mtkRules: 24` present; `.mtk1`/`.mtk5` resolve to correct colours. |
| More than one editor host | `hostCount: 1`. |
| Model detached from the editor | `isAttachedToEditor()` returns `true`. |
| Language applied after content | `InvokeScriptAsync` is synchronous on WebAssembly; ordering is preserved. |
| Deferred teardown on close | Never fires — the container is collapsed, not unloaded. |
| Layout / viewport / document size | Resize has no effect in either axis; 31–7805 line documents against 320 px and 900 px hosts are all clean when the window is unfocused. |
| `backgroundTokenizationState` | Reports `2` (Completed) on an affected model. It latches on the first completed pass and never goes back, so it is not a usable signal. |
