# OhUsings Roadmap

## Current Release (v1.0)

- [x] Import all missing usings in the active C# document.
- [x] Detect unresolved types via Roslyn diagnostics (CS0246, CS0103, CS0234).
- [x] Resolve candidate namespaces using Roslyn SymbolFinder.
- [x] Add unambiguous using directives automatically.
- [x] Skip ambiguous imports and report them.
- [x] Sort using directives alphabetically (System first).
- [x] Format and simplify the document after changes.
- [x] Show a summary in the Visual Studio status bar.
- [x] Tools menu and editor context menu integration.
- [x] Unit tests for analyzer and applier.

## Planned Features

### v1.1 — Quality of Life

- [ ] Add a configurable keyboard shortcut (e.g., `Ctrl+Shift+U`).
- [ ] Add a Visual Studio settings/options page for OhUsings preferences.
- [ ] Improve handling of `CS0103` and `CS0234` diagnostics.
- [ ] Better filtering of false positives (extension methods, attributes, etc.).

### v1.2 — Ambiguous Import Picker

- [ ] When ambiguous imports are found, show a Quick Pick / light bulb UI to let the user choose.
- [ ] Remember user choices per project or solution.

### v2.0 — Multi-Document Support

- [ ] Support project-wide "Import All Missing Usings" (all C# files in the active project).
- [ ] Support solution-wide "Import All Missing Usings".
- [ ] Show a preview/diff of all changes before applying.

### v2.1 — Reference Management

- [ ] Suggest adding missing project references when a type exists in another project.
- [ ] Suggest NuGet packages when a type is not found in current references.
- [ ] Integration with NuGet Package Manager for one-click install + import.

### Future Ideas

- [ ] Telemetry-free diagnostics and logging to an output window pane.
- [ ] Support for global usings (C# 10+).
- [ ] Support for `using static` directives.
- [ ] Support for type alias (`using Alias = Namespace.Type`) suggestions.
- [ ] Roslyn analyzer/code fix provider integration (light bulb actions).
- [ ] Visual Studio for Mac support (if applicable).
- [ ] Extension marketplace publishing with CI/CD pipeline.
- [ ] Localization support for status messages.

## Non-Goals (by design)

- No telemetry or data collection — ever.
- No modification of files other than the active document (until v2.0).
- No automatic NuGet package installation (until v2.1).
- No automatic project reference changes (until v2.1).
