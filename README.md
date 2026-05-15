# OhUsings

**OhUsings** adds a command to Visual Studio that imports all missing C# `using` directives in the current document at once — similar to JetBrains Rider's "Import Missing References" experience, focused on `using` directives.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Features

- **Import All Missing Usings** — Scans the active C# document for unresolved types and adds all safe, unambiguous `using` directives in one go.
- **Ambiguity-safe** — Skips symbols that resolve to multiple namespaces and reports them in a summary.
- **Sorted & clean** — Added `using` directives are alphabetically sorted and the document is formatted automatically.
- **Non-invasive** — Only modifies the active document. Never adds NuGet packages or project references.
- **Privacy-friendly** — No telemetry, no network calls.

## Installation

### From VSIX (local)

1. Build the solution in Visual Studio 2022.
2. Find `OhUsings.vsix` in the `bin/` output folder.
3. Double-click the `.vsix` to install.
4. Restart Visual Studio.

### From Visual Studio Marketplace

> Coming soon.

## Usage

1. Open a C# file that has unresolved type references (red squiggles from missing `using` directives).
2. Run the command via one of:
   - **Tools → OhUsings: Import All Missing Usings**
   - Right-click in the editor → **OhUsings: Import All Missing Usings**
3. The extension will:
   - Detect all unresolved type diagnostics.
   - Resolve namespaces using Roslyn.
   - Add all unambiguous `using` directives.
   - Format and simplify the document.
   - Show a summary in the status bar.

## Current Limitations

- **v1 scope**: Only processes the active document (not solution-wide).
- Only resolves types from assemblies already referenced by the project.
- Does not add NuGet packages or project references.
- Ambiguous imports (multiple candidate namespaces) are skipped and reported.
- Primarily targets `CS0246` diagnostics; `CS0103` and `CS0234` are handled on a best-effort basis.

## Architecture

See [docs/architecture.md](docs/architecture.md) for a detailed overview of the extension's design.

## Roadmap

See [docs/roadmap.md](docs/roadmap.md) for planned future features.

## Contributing

Contributions are welcome! Here's how to get started:

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/my-feature`.
3. Make your changes with tests.
4. Ensure the solution builds and tests pass.
5. Submit a pull request.

### Development Setup

- Visual Studio 2022 with the **Visual Studio extension development** workload.
- .NET Framework 4.7.2+ (for VSIX project).
- The solution uses Roslyn APIs from the VS SDK.

### Code Style

- Follow the `.editorconfig` rules in the repository.
- Keep classes small and focused.
- Add XML doc comments for public APIs.

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.
