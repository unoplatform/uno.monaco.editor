/**
 * esbuild configuration for Uno Monaco Editor.
 *
 * Produces:
 *   - Main bundle: WasmScripts/uno-monaco-helpers.js (IIFE, contains Monaco + helpers + bridge)
 *   - Worker bundles: WasmScripts/workers/*.worker.js (IIFE, separate files for new Worker())
 *
 * The main bundle is loaded on both WASM (EmbeddedResource) and Desktop (Content).
 * Workers are loaded at runtime via MonacoEnvironment.getWorkerUrl().
 *
 * Usage:
 *   node MonacoEditorComponent/ts-helpermethods/esbuild.config.mjs
 *   node MonacoEditorComponent/ts-helpermethods/esbuild.config.mjs --watch
 */

import * as esbuild from 'esbuild';
import * as path from 'path';
import { fileURLToPath } from 'url';
import * as fs from 'fs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const componentDir = path.resolve(__dirname, '..');
const rootDir = path.resolve(componentDir, '..');
const wasmScriptsDir = path.join(componentDir, 'WasmScripts');
const workersDir = path.join(wasmScriptsDir, 'workers');
const desktopContentDir = path.join(componentDir, 'DesktopContent');
const desktopWorkersDir = path.join(desktopContentDir, 'workers');

const isWatch = process.argv.includes('--watch');
const isMinify = process.argv.includes('--minify');

// Ensure output directories exist
fs.mkdirSync(workersDir, { recursive: true });
fs.mkdirSync(desktopWorkersDir, { recursive: true });

/**
 * Copies built files from WasmScripts to DesktopContent.
 * Used both after single builds and as an onEnd plugin for watch mode.
 */
function copyToDesktop() {
    fs.copyFileSync(
        path.join(wasmScriptsDir, 'uno-monaco-helpers.js'),
        path.join(desktopContentDir, 'uno-monaco-helpers.js')
    );

    const cssSource = path.join(wasmScriptsDir, 'uno-monaco-helpers.css');
    if (fs.existsSync(cssSource)) {
        fs.copyFileSync(cssSource, path.join(desktopContentDir, 'uno-monaco-helpers.css'));
    }

    for (const name of Object.keys(workerEntries)) {
        const src = path.join(workersDir, `${name}.js`);
        if (fs.existsSync(src)) {
            fs.copyFileSync(src, path.join(desktopWorkersDir, `${name}.js`));
        }
    }
}

/**
 * esbuild plugin that copies main bundle + CSS to DesktopContent after each build.
 * Attached to the main bundle context in watch mode.
 */
const copyMainToDesktopPlugin = {
    name: 'copy-main-to-desktop',
    setup(build) {
        build.onEnd((result) => {
            if (result.errors.length === 0) {
                try {
                    const jsOut = path.join(wasmScriptsDir, 'uno-monaco-helpers.js');
                    if (fs.existsSync(jsOut)) {
                        fs.copyFileSync(jsOut, path.join(desktopContentDir, 'uno-monaco-helpers.js'));
                    }
                    const cssOut = path.join(wasmScriptsDir, 'uno-monaco-helpers.css');
                    if (fs.existsSync(cssOut)) {
                        fs.copyFileSync(cssOut, path.join(desktopContentDir, 'uno-monaco-helpers.css'));
                    }
                    console.log('[esbuild] Copied main bundle to DesktopContent.');
                } catch (err) {
                    console.warn('[esbuild] Copy main to DesktopContent failed:', err.message);
                }
            }
        });
    }
};

/**
 * Creates an esbuild plugin that copies a specific worker to DesktopContent after rebuild.
 * Each worker context gets its own plugin so DesktopContent stays in sync during watch.
 */
function createCopyWorkerPlugin(workerName) {
    return {
        name: `copy-worker-${workerName}`,
        setup(build) {
            build.onEnd((result) => {
                if (result.errors.length === 0) {
                    try {
                        const src = path.join(workersDir, `${workerName}.js`);
                        if (fs.existsSync(src)) {
                            fs.copyFileSync(src, path.join(desktopWorkersDir, `${workerName}.js`));
                            console.log(`[esbuild] Copied ${workerName}.js to DesktopContent/workers/.`);
                        }
                    } catch (err) {
                        console.warn(`[esbuild] Copy ${workerName} to DesktopContent failed:`, err.message);
                    }
                }
            });
        }
    };
}

// Common build options
const commonOptions = {
    bundle: true,
    format: 'iife',
    target: 'es2015',
    platform: 'browser',
    logLevel: 'info',
    minify: isMinify,
    // Monaco ESM distribution includes .ttf font files via CSS @font-face
    // Use dataurl to inline fonts directly into the CSS output (avoids separate file serving)
    loader: {
        '.ttf': 'dataurl',
    },
};

// Worker entry points from Monaco ESM distribution
const workerEntries = {
    'editor.worker': 'monaco-editor/esm/vs/editor/editor.worker.js',
    'json.worker': 'monaco-editor/esm/vs/language/json/json.worker.js',
    'css.worker': 'monaco-editor/esm/vs/language/css/css.worker.js',
    'html.worker': 'monaco-editor/esm/vs/language/html/html.worker.js',
    'ts.worker': 'monaco-editor/esm/vs/language/typescript/ts.worker.js',
};

async function build() {
    // Build main bundle
    const mainBuildOptions = {
        ...commonOptions,
        entryPoints: [path.join(__dirname, 'index.ts')],
        outfile: path.join(wasmScriptsDir, 'uno-monaco-helpers.js'),
        sourcemap: 'inline',
        // Resolve vscode-jsonrpc from its browser entry
        conditions: ['browser', 'import'],
        // Resolve node_modules from root
        nodePaths: [path.join(rootDir, 'node_modules')],
    };

    // Build worker bundles (output to WasmScripts/workers/)
    const workerBuildOptions = Object.entries(workerEntries).map(([name, entry]) => ({
        ...commonOptions,
        entryPoints: [entry],
        outfile: path.join(workersDir, `${name}.js`),
        // No source maps for workers (not needed for debugging)
        sourcemap: false,
        nodePaths: [path.join(rootDir, 'node_modules')],
    }));

    if (isWatch) {
        // Watch mode: rebuild on change, copy to DesktopContent after each rebuild.
        // Each context gets its own copy plugin so all outputs stay in sync.
        const mainCtx = await esbuild.context({
            ...mainBuildOptions,
            plugins: [...(mainBuildOptions.plugins || []), copyMainToDesktopPlugin],
        });
        await mainCtx.watch();
        console.log('[esbuild] Watching main bundle for changes...');

        for (const [name, entry] of Object.entries(workerEntries)) {
            const opts = {
                ...commonOptions,
                entryPoints: [entry],
                outfile: path.join(workersDir, `${name}.js`),
                sourcemap: false,
                nodePaths: [path.join(rootDir, 'node_modules')],
                plugins: [createCopyWorkerPlugin(name)],
            };
            const ctx = await esbuild.context(opts);
            await ctx.watch();
        }
        console.log('[esbuild] Watching worker bundles for changes...');
    } else {
        // Single build
        console.log('[esbuild] Building main bundle...');
        await esbuild.build(mainBuildOptions);

        console.log('[esbuild] Building worker bundles...');
        for (const opts of workerBuildOptions) {
            await esbuild.build(opts);
        }

        // Copy files to DesktopContent for desktop packaging
        console.log('[esbuild] Copying bundles to DesktopContent...');
        copyToDesktop();

        console.log('[esbuild] Build complete.');
    }
}

build().catch((err) => {
    console.error('[esbuild] Build failed:', err);
    process.exit(1);
});
