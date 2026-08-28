/**
 * esbuild bundles a `.css` import into the output stylesheet; TypeScript has no notion of one.
 * Nothing in the pipeline runs `tsc` today, but the editor does, and an unresolved import is a
 * red squiggle over a file whose whole point is that its failures are invisible at build time.
 */
declare module '*.css';
