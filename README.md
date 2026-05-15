<p align="center">
  <img src="Resources/OhUsingsIcon.png" alt="OhUsings logo" width="128" />
</p>

<h1 align="center">OhUsings</h1>

<p align="center">
  <strong>Import all missing C# <code>using</code> directives in Visual Studio — in one click.</strong>
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="MIT License" /></a>
  <a href="https://visualstudio.microsoft.com/vs/"><img src="https://img.shields.io/badge/Visual%20Studio-2022-blue.svg" alt="VS 2022" /></a>
  <a href="#"><img src="https://img.shields.io/badge/Roslyn-powered-blueviolet.svg" alt="Roslyn" /></a>
</p>

---

Stop hunting for namespaces one red squiggle at a time. **OhUsings** scans your C# file (or your entire project/solution), resolves every missing type through Roslyn, and adds all safe `using` directives at once — sorted, formatted, and ready to go.

## Why OhUsings?

If you've ever opened a C# file full of unresolved types and found yourself pressing `Ctrl+.` over and over, you know the pain. Visual Studio's built-in "Add using" handles one type at a time. JetBrains Rider has "Import Missing References," but that requires switching IDEs.

**OhUsings brings that workflow to Visual Studio** — with a safety-first approach that never guesses when a type is ambiguous.

## Features

| Feature | Description |
|---------|-------------|
| **One-Click Import** | Adds all unambiguous `using` directives in a single operation |
| **Three Scopes** | Run on the current file, the active project, or the entire solution |
| **Light Bulb Actions** | Suggested actions appear inline — pick a namespace right from the editor |
| **Ambiguity Dialog** | When a type maps to multiple namespaces, a picker lets you choose |
| **Sorted & Formatted** | `System.*` namespaces first, alphabetically sorted, Roslyn-formatted |
| **10+ Diagnostics** | Handles CS0246, CS0103, CS0234, CS1061, CS1935, and more |
| **Extension Methods** | Detects missing `using` for LINQ and other extension methods |
| **Privacy-First** | Zero telemetry, zero network calls — ever |

## Quick Start

### Install

**From Visual Studio Marketplace (recommended)**

1. Open Visual Studio 2022.
2. Go to **Extensions → Manage Extensions**.
3. Search for **OhUsings**.
4. Click **Download** and restart Visual Studio.

Or install directly from: [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=mabroukmahdhi.OhUsings)

**From VSIX (local build)**

1. Build the solution in **Visual Studio 2022** (17.0+).
2. Locate `OhUsings.vsix` in the `bin/` output.
3. Double-click the `.vsix` file to install.
4. Restart Visual Studio.

### Use

**Option A — Menu command**

1. Open a C# file with unresolved types.
2. Go to **Tools → OhUsings: Import All Missing Usings**.
3. Done. Check the status bar for a summary.

**Option B — Context menu**

Right-click in the editor → **OhUsings: Import All Missing Usings**.

**Option C — Light bulb**

Place the caret on an unresolved type. Click the light bulb (or press `Ctrl+.`) and choose from the OhUsings suggested actions.

### Handling Ambiguous Types

When a type like `Timer` exists in multiple namespaces (`System.Timers`, `System.Threading`), OhUsings skips it during automatic import and opens the **Ambiguous Usings Dialog** so you can choose.

## How It Works

```
┌─────────────────────────┐
│  You invoke the command  │
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  ActiveDocumentService   │  Locates the Roslyn Document(s) for the scope
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  MissingUsingsAnalyzer   │  Reads compiler diagnostics, extracts type
│                          │  names, resolves namespaces via SymbolFinder
└────────────┬────────────┘
             ▼
        ┌────┴─────┐
   Unambiguous   Ambiguous
        │            │
        ▼            ▼
┌──────────────┐ ┌───────────────┐
│ Auto-applied │ │ Reported /    │
│ via Applier  │ │ Dialog shown  │
└──────┬───────┘ └───────────────┘
       ▼
┌─────────────────────────┐
│  UsingDirectiveApplier   │  Inserts, sorts, formats, applies changes
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  NotificationService     │  Status bar + Output Window summary
└─────────────────────────┘
```

Under the hood, OhUsings uses **Roslyn's semantic model** — not regex or text matching — to detect unresolved types and resolve namespaces. This means zero false positives from names that are already in scope.

## Supported Diagnostics

| ID | Meaning | Handling |
|----|---------|----------|
| CS0246 | Type or namespace not found | Primary — full resolution |
| CS0103 | Name does not exist in current context | Full resolution |
| CS0234 | Type/namespace does not exist in namespace | Full resolution |
| CS0305, CS0308 | Generic type errors | Full resolution |
| CS0400, CS0426, CS0616 | Type errors | Full resolution |
| CS1503, CS8179 | Argument/type mismatch errors | Full resolution |
| CS1061, CS1929 | Missing extension methods | Extension method search |
| CS1935 | Missing query pattern | Auto-suggests `System.Linq` |

## Configuration

OhUsings includes a lightweight options system:

| Option | Default | Description |
|--------|---------|-------------|
| `SortUsings` | `true` | Sort `using` directives alphabetically after adding |
| `PlaceSystemFirst` | `true` | Place `System.*` namespaces before third-party ones |
| `MaxDiagnosticsPerDocument` | `200` | Cap on diagnostics processed per file (performance guard) |

## Limitations

- Resolves types only from assemblies already referenced by the project.
- Does not add NuGet packages or project references (yet — see [roadmap](docs/roadmap.md)).
- Ambiguous imports require manual resolution via the dialog or light bulb.

## Architecture

The codebase follows a clean, interface-based design with four core services behind abstractions. See [docs/architecture.md](docs/architecture.md) for the full breakdown.

## Roadmap

Key planned features include a configurable keyboard shortcut, solution-wide preview/diff, NuGet package suggestions, and global usings support. See [docs/roadmap.md](docs/roadmap.md) for the complete plan.

## Contributing

Contributions are welcome!

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/my-feature`.
3. Make your changes with tests.
4. Ensure the solution builds and all tests pass.
5. Submit a pull request.

### Development Setup

| Requirement | Details |
|-------------|---------|
| IDE | Visual Studio 2022 with the **Visual Studio extension development** workload |
| Framework | .NET Framework 4.7.2+ |
| APIs | Roslyn (via VS SDK) |

### Running Tests

Open Test Explorer in Visual Studio and run all tests, or use the CLI:

```bash
dotnet test tests/OhUsings.Tests/OhUsings.Tests.csproj
```

### Code Style

- Follow the `.editorconfig` rules.
- Keep classes small and focused.
- Add XML doc comments for public APIs.

## License

MIT — see [LICENSE](LICENSE) for details.

---

<p align="center">
  Made with ☕ by <a href="https://github.com/MabroukMahdhi">Mabrouk Mahdhi</a> and <a href="https://github.com/wiemksaier">Wiem Ksaier</a>
</p>
