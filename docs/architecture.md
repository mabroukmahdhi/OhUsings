# OhUsings Architecture

## Overview

OhUsings is a Visual Studio 2022 VSIX extension that adds a single command — **"OhUsings: Import All Missing Usings"** — to detect and resolve missing C# `using` directives in the active document.

## High-Level Flow

```
User invokes command
        │
        ▼
┌──────────────────────┐
│ ImportAllMissing-     │
│ UsingsCommand         │  Entry point; orchestrates the pipeline
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ ActiveDocumentService │  Gets the Roslyn Document for the active VS editor
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ MissingUsingsAnalyzer │  Finds CS0246/CS0103/CS0234 diagnostics,
│                       │  extracts type names, resolves candidate namespaces
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ UsingDirectiveApplier │  Adds using directives to the syntax tree,
│                       │  sorts them, formats, and applies to workspace
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ NotificationService   │  Shows a summary message in the VS status bar
└──────────────────────┘
```

## Project Structure

```
/src/OhUsings/
├── OhUsingsPackage.cs            # AsyncPackage entry point
├── Commands/
│   └── ImportAllMissingUsingsCommand.cs   # Command handler + orchestration
├── Services/
│   ├── IActiveDocumentService.cs          # Interface: get active Roslyn Document
│   ├── ActiveDocumentService.cs           # DTE + VisualStudioWorkspace integration
│   ├── IMissingUsingsAnalyzer.cs          # Interface: analyze document diagnostics
│   ├── MissingUsingsAnalyzer.cs           # Roslyn diagnostic inspection + SymbolFinder
│   ├── IUsingDirectiveApplier.cs          # Interface: apply using directives
│   ├── UsingDirectiveApplier.cs           # Syntax rewriting + formatting
│   ├── INotificationService.cs            # Interface: show messages to user
│   └── NotificationService.cs             # VS status bar integration
├── Models/
│   ├── MissingUsingCandidate.cs           # A type name + its candidate namespaces
│   ├── ImportResult.cs                     # Outcome of the import operation
│   └── AmbiguousImport.cs                  # A type with multiple candidate namespaces
├── Options/
│   └── OhUsingsOptions.cs                  # Extensible configuration options
└── Resources/
    └── OhUsingsPackage.vsct                # Command table (menus, buttons)
```

## Key Design Decisions

### 1. Interface-based services

Each responsibility is behind an interface (`IActiveDocumentService`, `IMissingUsingsAnalyzer`, etc.) to enable unit testing and future extensibility. Service composition is done in `OhUsingsPackage.InitializeAsync` — no DI container required.

### 2. Roslyn-first approach

The extension uses Roslyn's semantic model for diagnostic detection, `SymbolFinder` for namespace resolution, and the `Formatter`/`Simplifier` for clean output. This avoids fragile text manipulation.

### 3. Ambiguity handling

When a type name maps to multiple namespaces, the extension **skips** it rather than guessing. The user sees a summary of what was skipped and why. This is a deliberate safety-first design.

### 4. Diagnostics-driven detection

Rather than walking the full syntax tree looking for unresolved names, the analyzer reads the semantic model's diagnostics (CS0246, CS0103, CS0234). This is faster and avoids false positives from names that are legitimately in scope.

### 5. AsyncPackage with background loading

The package uses `AsyncPackage` and `PackageAutoLoadFlags.BackgroundLoad` to avoid blocking Visual Studio startup. All command execution uses async/await and avoids blocking the UI thread.

## Diagnostic IDs

| ID     | Description                                           | Priority |
|--------|-------------------------------------------------------|----------|
| CS0246 | Type or namespace name could not be found             | Primary  |
| CS0103 | Name does not exist in the current context            | Secondary|
| CS0234 | Type or namespace does not exist in the namespace     | Secondary|

## Threading Model

- **Package initialization**: Starts on background thread, switches to UI thread for command registration.
- **Command execution**: Fires async via `JoinableTaskFactory.RunAsync`. Switches to UI thread only for DTE access and notification display.
- **Roslyn operations**: Run on background threads (semantic model, symbol finder, formatting).

## Error Handling

All exceptions are caught at the command boundary in `ImportAllMissingUsingsCommand.Execute`. Errors are logged via `Debug.WriteLine` and shown to the user via the status bar. The extension never crashes Visual Studio.
