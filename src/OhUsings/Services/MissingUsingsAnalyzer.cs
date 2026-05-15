using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using OhUsings.Models;
using OhUsings.Options;

namespace OhUsings.Services
{
    /// <summary>
    /// Inspects a C# document for unresolved type diagnostics and resolves candidate namespaces using Roslyn.
    /// </summary>
    public sealed class MissingUsingsAnalyzer : IMissingUsingsAnalyzer
    {
        // Diagnostics for missing types/namespaces
        private static readonly HashSet<string> TypeDiagnosticIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "CS0103", // The name 'X' does not exist in the current context
            "CS0234", // The type or namespace name 'X' does not exist in the namespace 'Y'
            "CS0246", // The type or namespace name 'X' could not be found
            "CS0305", // Using the generic type 'X<T>' requires N type arguments
            "CS0308", // The non-generic type 'X' cannot be used with type arguments
            "CS0400", // The type or namespace name 'X' could not be found in the global namespace
            "CS0426", // The type name 'X' does not exist in the type 'Y'
            "CS0616", // 'X' is not an attribute class
            "CS1503", // Cannot convert from 'X' to 'Y'
            "CS8179", // Predefined type 'System.ValueTuple`N' is not defined or imported
        };

        // Diagnostics for missing extension methods
        private static readonly HashSet<string> ExtensionMethodDiagnosticIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "CS1061", // 'X' does not contain a definition for 'Y' (extension methods)
            "CS1929", // 'X' does not contain a definition for 'Y' and best extension method requires different receiver
        };

        // Diagnostics where the fix is almost always a specific namespace
        private static readonly HashSet<string> QueryPatternDiagnosticIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "CS1935", // Could not find an implementation of the query pattern (missing System.Linq)
        };

        private static readonly HashSet<string> AllDiagnosticIds;

        static MissingUsingsAnalyzer()
        {
            AllDiagnosticIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in TypeDiagnosticIds) AllDiagnosticIds.Add(id);
            foreach (string id in ExtensionMethodDiagnosticIds) AllDiagnosticIds.Add(id);
            foreach (string id in QueryPatternDiagnosticIds) AllDiagnosticIds.Add(id);
        }

        private static readonly HashSet<string> CSharpKeywordsAndAliases = new HashSet<string>(StringComparer.Ordinal)
        {
            "bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong",
            "double", "float", "decimal", "string", "char", "void", "object", "typeof",
            "sizeof", "null", "true", "false", "if", "else", "while", "for", "foreach",
            "do", "switch", "case", "default", "lock", "try", "throw", "catch", "finally",
            "goto", "break", "continue", "return", "public", "private", "internal",
            "protected", "static", "readonly", "sealed", "const", "fixed", "stackalloc",
            "volatile", "new", "override", "abstract", "virtual", "event", "extern",
            "ref", "out", "in", "is", "as", "params", "this", "base", "namespace",
            "using", "class", "struct", "interface", "enum", "delegate", "checked",
            "unchecked", "unsafe", "operator", "implicit", "explicit", "var", "dynamic",
            "async", "await", "nameof", "when", "where", "yield", "partial", "global",
            "nint", "nuint", "record", "with", "init", "required", "file", "scoped"
        };

        private readonly OhUsingsOptions _options;

        public MissingUsingsAnalyzer()
            : this(new OhUsingsOptions())
        {
        }

        public MissingUsingsAnalyzer(OhUsingsOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MissingUsingCandidate>> AnalyzeAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);

            if (semanticModel == null || syntaxRoot == null)
                return Array.Empty<MissingUsingCandidate>();

            var diagnostics = semanticModel.GetDiagnostics(cancellationToken: cancellationToken);

            var existingUsings = GetExistingUsings(syntaxRoot);
            var unresolvedEntries = ExtractUnresolvedEntries(diagnostics, syntaxRoot);

            var candidates = new List<MissingUsingCandidate>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in unresolvedEntries)
            {
                // Deduplicate by name+kind
                string key = $"{entry.Kind}:{entry.Name}";
                if (!seen.Add(key))
                    continue;

                IReadOnlyList<string> namespaces;

                switch (entry.Kind)
                {
                    case UnresolvedKind.ExtensionMethod:
                        namespaces = await FindExtensionMethodNamespacesAsync(
                            entry.Name, document, existingUsings, cancellationToken);
                        break;

                    case UnresolvedKind.QueryPattern:
                        // CS1935 almost always means System.Linq is missing
                        namespaces = existingUsings.Contains("System.Linq")
                            ? Array.Empty<string>()
                            : new[] { "System.Linq" };
                        break;

                    default:
                        namespaces = await FindCandidateNamespacesAsync(
                            entry.Name, document, existingUsings, cancellationToken);
                        break;
                }

                if (namespaces.Count > 0 || entry.Kind == UnresolvedKind.Type)
                {
                    candidates.Add(new MissingUsingCandidate(entry.Name, namespaces));
                }
            }

            return candidates;
        }

        /// <summary>
        /// Collects already-imported namespace names from the syntax root.
        /// </summary>
        internal static HashSet<string> GetExistingUsings(SyntaxNode root)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            var compilationUnit = root as CompilationUnitSyntax;
            if (compilationUnit != null)
            {
                foreach (var usingDirective in compilationUnit.Usings)
                {
                    if (usingDirective.Name != null)
                        result.Add(usingDirective.Name.ToString());
                }
            }

            // Also gather usings inside namespace declarations
            foreach (var ns in root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
            {
                foreach (var usingDirective in ns.Usings)
                {
                    if (usingDirective.Name != null)
                        result.Add(usingDirective.Name.ToString());
                }
            }

            return result;
        }

        /// <summary>
        /// Classifies what kind of unresolved symbol we're dealing with.
        /// </summary>
        internal enum UnresolvedKind
        {
            Type,
            ExtensionMethod,
            QueryPattern
        }

        /// <summary>
        /// An unresolved name extracted from a diagnostic, tagged with its kind.
        /// </summary>
        internal readonly struct UnresolvedEntry
        {
            public string Name { get; }
            public UnresolvedKind Kind { get; }

            public UnresolvedEntry(string name, UnresolvedKind kind)
            {
                Name = name;
                Kind = kind;
            }
        }

        /// <summary>
        /// Extracts unresolved entries from diagnostics, classifying each by kind.
        /// </summary>
        internal IReadOnlyList<UnresolvedEntry> ExtractUnresolvedEntries(
            IEnumerable<Diagnostic> diagnostics,
            SyntaxNode root)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<UnresolvedEntry>();
            int count = 0;

            foreach (var diagnostic in diagnostics)
            {
                if (count >= _options.MaxDiagnosticsPerDocument)
                    break;

                if (!AllDiagnosticIds.Contains(diagnostic.Id))
                    continue;

                if (diagnostic.Location == null || !diagnostic.Location.IsInSource)
                    continue;

                // Determine the kind
                UnresolvedKind kind;
                if (QueryPatternDiagnosticIds.Contains(diagnostic.Id))
                    kind = UnresolvedKind.QueryPattern;
                else if (ExtensionMethodDiagnosticIds.Contains(diagnostic.Id))
                    kind = UnresolvedKind.ExtensionMethod;
                else
                    kind = UnresolvedKind.Type;

                var span = diagnostic.Location.SourceSpan;
                var node = root.FindNode(span, findInsideTrivia: false, getInnermostNodeForTie: true);

                string? name = ExtractTypeName(node);

                if (name == null)
                    continue;

                if (CSharpKeywordsAndAliases.Contains(name))
                    continue;

                // Skip single-character names (likely generic type parameters: T, K, V)
                if (name.Length <= 1)
                    continue;

                // For type diagnostics, skip names starting with lowercase
                // For extension methods, lowercase names are expected (e.g., .Where, .Select)
                if (kind == UnresolvedKind.Type
                    && char.IsLower(name[0])
                    && name != name.ToUpperInvariant())
                    continue;

                string key = $"{kind}:{name}";
                if (seen.Add(key))
                {
                    entries.Add(new UnresolvedEntry(name, kind));
                    count++;
                }
            }

            return entries;
        }

        /// <summary>
        /// Extracts unique unresolved type/identifier names from relevant diagnostics.
        /// Kept for backward compatibility with tests.
        /// </summary>
        internal IReadOnlyList<string> ExtractUnresolvedNames(
            IEnumerable<Diagnostic> diagnostics,
            SyntaxNode root)
        {
            return ExtractUnresolvedEntries(diagnostics, root)
                .Where(e => e.Kind == UnresolvedKind.Type)
                .Select(e => e.Name)
                .ToList();
        }

        private static string? ExtractTypeName(SyntaxNode? node)
        {
            if (node == null)
                return null;

            switch (node)
            {
                case IdentifierNameSyntax identifier:
                    return identifier.Identifier.ValueText;

                case GenericNameSyntax generic:
                    return generic.Identifier.ValueText;

                case QualifiedNameSyntax qualified:
                    // For CS0234 the left part is known; we want the rightmost unresolved part
                    return qualified.Right.Identifier.ValueText;

                default:
                    // Try to find the most relevant identifier in the span
                    var identifierNode = node.DescendantNodesAndSelf()
                        .OfType<IdentifierNameSyntax>()
                        .FirstOrDefault();
                    return identifierNode?.Identifier.ValueText;
            }
        }

        /// <summary>
        /// Searches the solution for accessible named type symbols matching the given name.
        /// Returns the distinct namespaces that are not already imported.
        /// </summary>
        internal static async Task<IReadOnlyList<string>> FindCandidateNamespacesAsync(
            string typeName,
            Document document,
            HashSet<string> existingUsings,
            CancellationToken cancellationToken)
        {
            var solution = document.Project.Solution;
            var compilation = await document.Project.GetCompilationAsync(cancellationToken);
            if (compilation == null)
                return Array.Empty<string>();

            var symbols = await SymbolFinder.FindDeclarationsAsync(
                document.Project,
                typeName,
                ignoreCase: false,
                cancellationToken: cancellationToken);

            var namespaces = symbols
                .OfType<INamedTypeSymbol>()
                .Where(s => IsAccessible(s, compilation))
                .Select(s => s.ContainingNamespace)
                .Where(ns => ns != null && !ns.IsGlobalNamespace)
                .Select(ns => ns!.ToDisplayString())
                .Where(ns => !existingUsings.Contains(ns))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ns => ns, StringComparer.Ordinal)
                .ToList();

            return namespaces;
        }

        /// <summary>
        /// Searches the solution for extension methods with the given name.
        /// Returns the distinct containing namespaces that are not already imported.
        /// </summary>
        internal static async Task<IReadOnlyList<string>> FindExtensionMethodNamespacesAsync(
            string methodName,
            Document document,
            HashSet<string> existingUsings,
            CancellationToken cancellationToken)
        {
            var compilation = await document.Project.GetCompilationAsync(cancellationToken);
            if (compilation == null)
                return Array.Empty<string>();

            // Search for all declarations matching the method name
            var symbols = await SymbolFinder.FindDeclarationsAsync(
                document.Project,
                methodName,
                ignoreCase: false,
                filter: SymbolFilter.Member,
                cancellationToken: cancellationToken);

            var namespaces = symbols
                .OfType<IMethodSymbol>()
                .Where(m => m.IsExtensionMethod)
                .Where(m => IsMethodAccessible(m, compilation))
                .Select(m => m.ContainingType?.ContainingNamespace)
                .Where(ns => ns != null && !ns!.IsGlobalNamespace)
                .Select(ns => ns!.ToDisplayString())
                .Where(ns => !existingUsings.Contains(ns))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ns => ns, StringComparer.Ordinal)
                .ToList();

            return namespaces;
        }

        private static bool IsMethodAccessible(IMethodSymbol method, Compilation compilation)
        {
            // The method itself must be public, and its containing static class must be accessible
            if (method.DeclaredAccessibility != Accessibility.Public)
                return false;

            var containingType = method.ContainingType;
            if (containingType == null)
                return false;

            return containingType.DeclaredAccessibility == Accessibility.Public
                || (containingType.DeclaredAccessibility == Accessibility.Internal
                    && compilation.Assembly.Name == containingType.ContainingAssembly?.Name);
        }

        private static bool IsAccessible(INamedTypeSymbol symbol, Compilation compilation)
        {
            // Accept public types and internal types (from the same assembly or InternalsVisibleTo)
            return symbol.DeclaredAccessibility == Accessibility.Public
                || (symbol.DeclaredAccessibility == Accessibility.Internal
                    && compilation.Assembly.Name == symbol.ContainingAssembly?.Name);
        }
    }
}
