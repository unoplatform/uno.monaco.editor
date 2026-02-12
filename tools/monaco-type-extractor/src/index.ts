#!/usr/bin/env node
/**
 * CLI entry point for the Monaco type extractor.
 *
 * Usage:
 *   npx tsx src/index.ts <path-to-monaco.d.ts> [-o output.json]
 *
 * Examples:
 *   npx tsx src/index.ts ../../node_modules/monaco-editor/monaco.d.ts
 *   npx tsx src/index.ts ../../node_modules/monaco-editor/monaco.d.ts -o output/model.json
 */
import { extractMonacoModel } from "./extractor.js";
import * as fs from "node:fs";
import * as path from "node:path";

function main(): void {
  const args = process.argv.slice(2);

  if (args.length === 0 || args.includes("--help") || args.includes("-h")) {
    printUsage();
    process.exit(args.includes("--help") || args.includes("-h") ? 0 : 1);
  }

  // Parse arguments
  let inputPath: string | undefined;
  let outputPath: string | undefined;

  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (arg === "-o" || arg === "--output") {
      outputPath = args[++i];
      if (!outputPath) {
        console.error("Error: -o/--output requires a file path argument");
        process.exit(1);
      }
    } else if (arg === "--") {
      // Skip the -- separator
      continue;
    } else if (!arg.startsWith("-")) {
      inputPath = arg;
    } else {
      console.error(`Error: Unknown option: ${arg}`);
      printUsage();
      process.exit(1);
    }
  }

  if (!inputPath) {
    console.error("Error: No input file specified");
    printUsage();
    process.exit(1);
  }

  // Resolve paths
  const resolvedInput = path.resolve(inputPath);

  if (!fs.existsSync(resolvedInput)) {
    console.error(`Error: Input file not found: ${resolvedInput}`);
    process.exit(1);
  }

  console.error(`Extracting types from: ${resolvedInput}`);

  const model = extractMonacoModel(resolvedInput);

  // Summary statistics
  let totalInterfaces = 0;
  let totalEnums = 0;
  let totalTypeAliases = 0;
  let totalClasses = 0;
  let totalFunctions = 0;

  for (const ns of model.namespaces) {
    totalInterfaces += ns.interfaces.length;
    totalEnums += ns.enums.length;
    totalTypeAliases += ns.typeAliases.length;
    totalClasses += ns.classes.length;
    totalFunctions += ns.functions.length;
  }

  console.error(`\nExtraction complete:`);
  console.error(`  Namespaces:   ${model.namespaces.length}`);
  console.error(`  Interfaces:   ${totalInterfaces}`);
  console.error(`  Enums:        ${totalEnums}`);
  console.error(`  Type aliases: ${totalTypeAliases}`);
  console.error(`  Classes:      ${totalClasses}`);
  console.error(`  Functions:    ${totalFunctions}`);

  const json = JSON.stringify(model, null, 2);

  if (outputPath) {
    const resolvedOutput = path.resolve(outputPath);
    const outputDir = path.dirname(resolvedOutput);

    // Ensure output directory exists
    if (!fs.existsSync(outputDir)) {
      fs.mkdirSync(outputDir, { recursive: true });
    }

    fs.writeFileSync(resolvedOutput, json, "utf-8");
    console.error(`\nOutput written to: ${resolvedOutput}`);
  } else {
    // Write to stdout
    process.stdout.write(json);
    process.stdout.write("\n");
  }
}

function printUsage(): void {
  console.error(`
Usage: npx tsx src/index.ts <path-to-monaco.d.ts> [options]

Options:
  -o, --output <file>   Write JSON output to a file instead of stdout
  -h, --help            Show this help message

Examples:
  npx tsx src/index.ts ../../node_modules/monaco-editor/monaco.d.ts
  npx tsx src/index.ts ../../node_modules/monaco-editor/monaco.d.ts -o output/model.json
`);
}

main();
