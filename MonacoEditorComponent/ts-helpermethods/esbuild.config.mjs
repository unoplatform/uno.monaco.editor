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
 *       Production build (one-shot): minified, no source maps. This is what every call
 *       site uses -- `npm run build`, the MSBuild targets in MonacoEditorComponent.csproj,
 *       install-dependencies.ps1, and CI.
 *   node MonacoEditorComponent/ts-helpermethods/esbuild.config.mjs --watch
 *       Development build: unminified, inline source maps, rebuild on change.
 *
 * Overrides (apply to both modes):
 *   --no-minify   Force minification off (debuggable one-shot build).
 *   --minify      Force minification on; wins over --no-minify. Useful as
 *                 `--watch --minify --sourcemap` to reproduce a minification-only bug
 *                 in a fast rebuild loop.
 *   --sourcemap   Force inline source maps on. There is deliberately no --no-sourcemap:
 *                 one-shot builds never emit maps unless asked.
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

// Watch = development: unminified with inline source maps, for debuggability.
// One-shot = production: minified, no source maps. Every call site invokes this script
// with no flags (npm run build, the MSBuild targets, install-dependencies.ps1, CI), so the
// production intent has to live in the default rather than in the callers.
// --minify wins over --no-minify so `--watch --minify --sourcemap` stays a usable debug
// loop for problems that only reproduce in minified output.
const isMinify = process.argv.includes('--minify')
    || (!isWatch && !process.argv.includes('--no-minify'));
const mainSourcemap = (isWatch || process.argv.includes('--sourcemap')) ? 'inline' : false;

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
    // Keep third-party license banners (/*! ... */, @license, @preserve) in a block at the
    // end of every output file. This is already esbuild's default when bundle: true; it is
    // pinned explicitly because these bundles redistribute Monaco (MIT) and the TypeScript
    // compiler (Apache-2.0), and that attribution must not silently disappear if esbuild's
    // default changes or someone sets 'none' while tuning output size.
    legalComments: 'eof',
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
        sourcemap: mainSourcemap,
        // Resolve vscode-jsonrpc from its browser entry
        conditions: ['browser', 'import'],
        // Resolve node_modules from root
        nodePaths: [path.join(rootDir, 'node_modules')],
    };

    // Build worker bundles (output to WasmScripts/workers/).
    // Factory so the one-shot path and the watch path cannot drift apart.
    const createWorkerBuildOptions = (name, entry) => ({
        ...commonOptions,
        entryPoints: [entry],
        outfile: path.join(workersDir, `${name}.js`),
        // No source maps for workers in any mode, including watch: these are third-party
        // Monaco/TypeScript bundles that are never debugged from source here, and the maps
        // would dominate their size.
        sourcemap: false,
        nodePaths: [path.join(rootDir, 'node_modules')],
    });

    const workerBuildOptions = Object.entries(workerEntries)
        .map(([name, entry]) => createWorkerBuildOptions(name, entry));

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
            const ctx = await esbuild.context({
                ...createWorkerBuildOptions(name, entry),
                plugins: [createCopyWorkerPlugin(name)],
            });
            await ctx.watch();
        }
        console.log('[esbuild] Watching worker bundles for changes...');
    } else {
        // Single build
        console.log(`[esbuild] Mode: ${isMinify ? 'minified' : 'unminified'}, source maps: ${mainSourcemap || 'none'}.`);
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
