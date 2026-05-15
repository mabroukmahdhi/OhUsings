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
        // CS0103: The name 'X' does not exist in the current context
        // CS0234: The type or namespace name 'X' does not exist in the namespace 'Y'
        // CS0246: The type or namespace name 'X' could not be found
        // CS0305: Using the generic type 'X<T>' requires N type arguments
        // CS0308: The non-generic type 'X' cannot be used with type arguments
        // CS0426: The type name 'X' does not exist in the type 'Y'
        // CS0616: 'X' is not an attribute class
        // CS0619: 'X' is obsolete (can surface when wrong type is resolved)
        // CS1061: 'X' does not contain a definition for 'Y' (extension methods)
        // CS1501: No overload for method 'X' takes N arguments (extension methods)
        // CS1503: Cannot convert from 'X' to 'Y'
        // CS1929: 'X' does not contain a definition for 'Y' and the best extension method overload requires a receiver of type 'Z'
        // CS1935: Could not find an implementation of the query pattern for source type 'X' (missing System.Linq)
        // CS8179: Predefined type 'System.ValueTuple`N' is not defined or imported
        // CS0400: The type or namespace name 'X' could not be found in the global namespace
        // CS0012: The type 'X' is defined in an assembly that is not referenced
        private static readonly HashSet<string> TargetDiagnosticIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "CS0103",
            "CS0234",
            "CS0246",
            "CS0305",
            "CS0308",
            "CS0400",
            "CS0426",
            "CS0616",
            "CS1061",
            "CS1501",
            "CS1503",
            "CS1929",
            "CS1935",
            "CS8179",
        };

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
            var unresolvedNames = ExtractUnresolvedNames(diagnostics, syntaxRoot);

            var candidates = new List<MissingUsingCandidate>();

            foreach (string typeName in unresolvedNames)
            {
                var namespaces = await FindCandidateNamespacesAsync(
                    typeName, document, existingUsings, cancellationToken);

                candidates.Add(new MissingUsingCandidate(typeName, namespaces));
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
        /// Extracts unique unresolved type/identifier names from relevant diagnostics.
        /// </summary>
        internal IReadOnlyList<string> ExtractUnresolvedNames(
            IEnumerable<Diagnostic> diagnostics,
            SyntaxNode root)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var names = new List<string>();
            int count = 0;

            foreach (var diagnostic in diagnostics)
            {
                if (count >= _options.MaxDiagnosticsPerDocument)
                    break;

                if (!TargetDiagnosticIds.Contains(diagnostic.Id))
                    continue;

                if (diagnostic.Location == null || !diagnostic.Location.IsInSource)
                    continue;

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

                // Skip names starting with lowercase (likely local variables, not types)
                if (char.IsLower(name[0]) && name != name.ToUpperInvariant())
                    continue;

                if (seen.Add(name))
                {
                    names.Add(name);
                    count++;
                }
            }

            return names;
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

        private static bool IsAccessible(INamedTypeSymbol symbol, Compilation compilation)
        {
            // Accept public types and internal types (from the same assembly or InternalsVisibleTo)
            return symbol.DeclaredAccessibility == Accessibility.Public
                || (symbol.DeclaredAccessibility == Accessibility.Internal
                    && compilation.Assembly.Name == symbol.ContainingAssembly?.Name);
        }
    }
}
