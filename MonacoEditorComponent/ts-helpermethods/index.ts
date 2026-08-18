/**
 * Entry point for the unified uno-monaco-helpers.js bundle.
 *
 * Imports Monaco Editor from ESM, all helper modules, and the JSON-RPC bridge.
 * Configures MonacoEnvironment.getWorkerUrl() for platform-specific worker resolution.
 * Assigns public functions to globalThis for [JSImport("globalThis.*")] compatibility.
 *
 * Output format: IIFE (via esbuild --format=iife)
 * Do NOT use esbuild's globalName option -- explicit globalThis assignments below.
 */

import * as monaco from 'monaco-editor';

// Helper modules
import {
    createMonacoEditor,
    disposeEditor,
    InvokeJS,
    languageIdFromExtension,
    sanitize,
    desanitize,
    stringifyForMarshalling,
    callParentEventAsync,
    callParentActionWithParameters
} from './asyncCallbackHelpers';

import {
    EditorContext,
    registerHoverProvider,
    addAction,
    addCommand,
    createContext,
    updateContext,
    updateContent,
    updateDecorations,
    updateStyle,
    getOptions,
    updateOptions,
    updateLanguage,
    changeTheme,
    keyDown,
    getParentValue,
    getParentJsonValue,
    getParentValueAsync,
    getParentJsonValueAsync,
    getThemeIsHighContrast,
    getThemeCurrentThemeName,
    getThemeIsHighContrastAsync,
    getThemeCurrentThemeNameAsync
} from './otherScriptsToBeOrganized';

import { registerCodeActionProvider } from './registerCodeActionProvider';
import { registerCodeLensProvider } from './registerCodeLensProvider';
import { registerColorProvider } from './registerColorProvider';
import { registerCompletionItemProvider } from './registerCompletionItemProvider';
import { registerLanguage } from './registerLanguage';
import { updateSelectedContent } from './updateSelectedContent';

// Languages Monaco does not ship, bundled with the component
import { registerDiffLanguage } from './languages/diff';

// Bridge module
import { createBridgeConnection, isDesktopHost, getConnection } from './bridge/jsonRpcBridge';

// ---------------------------------------------------------------------------
// Configure Monaco workers
// ---------------------------------------------------------------------------

// Detect platform: desktop (WebView2) serves from virtual host root or file://,
// WASM serves from the Uno Bootstrap base path.
const isDesktop = isDesktopHost();

/**
 * Resolve a worker URL relative to the main bundle script's location.
 *
 * Using the script's own URL as base (via document.currentScript.src) ensures
 * workers resolve correctly in both:
 * - Desktop: file:// or https://uno-monaco.example/ (relative to editor.html)
 * - WASM: subpath deployments where WasmScripts/ may be at a non-root path
 *
 * Falls back to document.baseURI if currentScript is unavailable (deferred scripts).
 */
const _scriptBase: string = (() => {
    const cs = (document as any).currentScript;
    if (cs && cs.src) {
        // Strip filename to get the directory: ".../uno-monaco-helpers.js" -> ".../"
        return cs.src.substring(0, cs.src.lastIndexOf('/') + 1);
    }
    return document.baseURI || '';
})();

function resolveWorkerUrl(filename: string): string {
    return _scriptBase + `workers/${filename}`;
}

(self as any).MonacoEnvironment = {
    getWorkerUrl: function (_moduleId: string, label: string) {
        // Map language labels to worker file names
        if (label === 'json') {
            return resolveWorkerUrl('json.worker.js');
        }
        if (label === 'css' || label === 'scss' || label === 'less') {
            return resolveWorkerUrl('css.worker.js');
        }
        if (label === 'html' || label === 'handlebars' || label === 'razor') {
            return resolveWorkerUrl('html.worker.js');
        }
        if (label === 'typescript' || label === 'javascript') {
            return resolveWorkerUrl('ts.worker.js');
        }
        // Default editor worker
        return resolveWorkerUrl('editor.worker.js');
    }
};

// ---------------------------------------------------------------------------
// Register bundled languages
// ---------------------------------------------------------------------------

// Runs at bundle load, before any editor exists, so the language is available to the
// first model created and is reported by GetLanguagesAsync() whenever C# asks.
registerDiffLanguage();

// ---------------------------------------------------------------------------
// Auto-init bridge on desktop
// ---------------------------------------------------------------------------

if (isDesktop) {
    const connection = createBridgeConnection();
    connection.listen();
    // Signal that the JSON-RPC bridge transport is ready to accept messages.
    // This fires at bundle load, BEFORE editor creation.
    // editor/ready is sent after createMonacoEditor() completes (see asyncCallbackHelpers.ts).
    connection.sendNotification('bridge/ready', { protocolVersion: 1 });
}

// ---------------------------------------------------------------------------
// Assign public functions to globalThis for [JSImport] / InvokeScriptAsync
// ---------------------------------------------------------------------------

// Core editor functions (referenced by [JSImport("globalThis.*")])
globalThis.createMonacoEditor = createMonacoEditor;
globalThis.InvokeJS = InvokeJS;
globalThis.languageIdFromExtension = languageIdFromExtension;

// Editor manipulation (used by InvokeScriptAsync eval patterns)
(globalThis as any).EditorContext = EditorContext;
(globalThis as any).registerHoverProvider = registerHoverProvider;
(globalThis as any).addAction = addAction;
(globalThis as any).addCommand = addCommand;
(globalThis as any).createContext = createContext;
(globalThis as any).updateContext = updateContext;
(globalThis as any).updateContent = updateContent;
(globalThis as any).updateDecorations = updateDecorations;
(globalThis as any).updateStyle = updateStyle;
(globalThis as any).getOptions = getOptions;
(globalThis as any).updateOptions = updateOptions;
(globalThis as any).updateLanguage = updateLanguage;
(globalThis as any).changeTheme = changeTheme;
(globalThis as any).keyDown = keyDown;
(globalThis as any).updateSelectedContent = updateSelectedContent;

// Provider registrations
(globalThis as any).registerCodeActionProvider = registerCodeActionProvider;
(globalThis as any).registerCodeLensProvider = registerCodeLensProvider;
(globalThis as any).registerColorProvider = registerColorProvider;
(globalThis as any).registerLanguage = registerLanguage;
(globalThis as any).registerCompletionItemProvider = registerCompletionItemProvider;

// Utility functions (used internally by InvokeScriptAsync eval code)
(globalThis as any).sanitize = sanitize;
(globalThis as any).desantize = desanitize; // Preserve original typo for backward compat with eval calls
(globalThis as any).desanitize = desanitize;
(globalThis as any).stringifyForMarshalling = stringifyForMarshalling;
(globalThis as any).callParentEventAsync = callParentEventAsync;
(globalThis as any).callParentActionWithParameters = callParentActionWithParameters;
(globalThis as any).getParentValue = getParentValue;
(globalThis as any).getParentJsonValue = getParentJsonValue;
(globalThis as any).getParentValueAsync = getParentValueAsync;
(globalThis as any).getParentJsonValueAsync = getParentJsonValueAsync;
(globalThis as any).getThemeIsHighContrast = getThemeIsHighContrast;
(globalThis as any).getThemeCurrentThemeName = getThemeCurrentThemeName;
(globalThis as any).getThemeIsHighContrastAsync = getThemeIsHighContrastAsync;
(globalThis as any).getThemeCurrentThemeNameAsync = getThemeCurrentThemeNameAsync;

// Expose Monaco namespace globally (needed by eval-style InvokeScriptAsync calls)
(globalThis as any).monaco = monaco;

// Dispose/cleanup
(globalThis as any).disposeEditor = disposeEditor;

// Expose bridge utilities for external use
(globalThis as any).isDesktopHost = isDesktopHost;
(globalThis as any).createBridgeConnection = createBridgeConnection;
(globalThis as any).getConnection = getConnection;
