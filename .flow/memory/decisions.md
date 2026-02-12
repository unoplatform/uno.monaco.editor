# Decisions

Architectural choices with rationale. Why we chose X over Y.

<!-- Entries added manually via `flowctl memory add` -->

## 2026-02-12 manual [decision]
Emitter pipeline tests validate source-text output (attributes, structure, patterns) rather than runtime serialization because: (1) the emitter contract is 'produce correct C# source', (2) runtime tests exist in SerializationContractTests, (3) runtime compilation of emitted types requires Roslyn scripting infrastructure
