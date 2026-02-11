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
    InvokeJS,
    refreshLayout,
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
    getThemeIsHighContrast,
    getThemeCurrentThemeName
} from './otherScriptsToBeOrganized';

import { registerCodeActionProvider } from './registerCodeActionProvider';
import { registerCodeLensProvider } from './registerCodeLensProvider';
import { registerColorProvider } from './registerColorProvider';
import { registerCompletionItemProvider } from './registerCompletionItemProvider';
import { updateSelectedContent } from './updateSelectedContent';

// Bridge module
import { createBridgeConnection, isDesktopHost } from './bridge/jsonRpcBridge';

// ---------------------------------------------------------------------------
// Configure Monaco workers
// ---------------------------------------------------------------------------

// Detect platform: desktop (WebView2) serves from virtual host root or file://,
// WASM serves from the Uno Bootstrap base path.
const isDesktop = isDesktopHost();

/**
 * Resolve a worker URL relative to the current document.
 * On desktop (file:// or virtual-host), absolute paths like "/workers/..."
 * break on macOS/Linux. Using a URL relative to the document's own location
 * works across Windows (https://uno-monaco.example/), macOS, and Linux (file://).
 */
function resolveWorkerUrl(filename: string): string {
    // Both WASM and desktop use document-relative paths
    return `workers/${filename}`;
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
// Auto-init bridge on desktop
// ---------------------------------------------------------------------------

if (isDesktop) {
    const connection = createBridgeConnection();
    connection.listen();
    // Notify the C# host that the bridge is ready and accepting JSON-RPC messages
    connection.sendNotification('editor/ready', { protocolVersion: 1 });
}

// ---------------------------------------------------------------------------
// Assign public functions to globalThis for [JSImport] / InvokeScriptAsync
// ---------------------------------------------------------------------------

// Core editor functions (referenced by [JSImport("globalThis.*")])
globalThis.createMonacoEditor = createMonacoEditor;
globalThis.refreshLayout = refreshLayout;
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
(globalThis as any).getThemeIsHighContrast = getThemeIsHighContrast;
(globalThis as any).getThemeCurrentThemeName = getThemeCurrentThemeName;

// Expose Monaco namespace globally (needed by eval-style InvokeScriptAsync calls)
(globalThis as any).monaco = monaco;

// Expose bridge utilities for external use
(globalThis as any).isDesktopHost = isDesktopHost;
(globalThis as any).createBridgeConnection = createBridgeConnection;
