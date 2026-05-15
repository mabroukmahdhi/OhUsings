using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace OhUsings.Services
{
    /// <summary>
    /// Adds using directives to a CompilationUnitSyntax, sorts them, and applies formatting.
    /// </summary>
    public sealed class UsingDirectiveApplier : IUsingDirectiveApplier
    {
        /// <inheritdoc />
        public async Task<Document> ApplyAsync(
            Document document,
            IReadOnlyList<string> namespacesToAdd,
            CancellationToken cancellationToken = default)
        {
            if (namespacesToAdd == null || namespacesToAdd.Count == 0)
                return document;

            var root = await document.GetSyntaxRootAsync(cancellationToken) as CompilationUnitSyntax;
            if (root == null)
                return document;

            var existingUsings = GetExistingUsingNames(root);

            // Build new using directive nodes, skipping duplicates
            var newDirectives = namespacesToAdd
                .Where(ns => !existingUsings.Contains(ns))
                .Distinct(StringComparer.Ordinal)
                .Select(ns => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns))
                    .NormalizeWhitespace()
                    .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed))
                .ToList();

            if (newDirectives.Count == 0)
                return document;

            // Combine existing + new, then sort
            var allUsings = root.Usings
                .AddRange(newDirectives)
                .OrderBy(u => IsSystemUsing(u) ? 0 : 1)
                .ThenBy(u => u.Name?.ToString() ?? string.Empty, StringComparer.Ordinal)
                .ToSyntaxList();

            var updatedRoot = root.WithUsings(allUsings);

            // Apply formatting
            document = document.WithSyntaxRoot(updatedRoot);
            document = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);

            // Apply simplification
            var simplifiedRoot = await document.GetSyntaxRootAsync(cancellationToken);
            if (simplifiedRoot != null)
            {
                simplifiedRoot = simplifiedRoot.WithAdditionalAnnotations(Simplifier.Annotation);
                document = document.WithSyntaxRoot(simplifiedRoot);
                document = await Simplifier.ReduceAsync(document, cancellationToken: cancellationToken);
            }

            // Apply changes to workspace
            var workspace = document.Project.Solution.Workspace;
            if (!workspace.TryApplyChanges(document.Project.Solution))
            {
                throw new InvalidOperationException(
                    "OhUsings: Failed to apply changes to the workspace. " +
                    "The document may be read-only or locked by another operation.");
            }

            return document;
        }

        private static HashSet<string> GetExistingUsingNames(CompilationUnitSyntax root)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);

            foreach (var u in root.Usings)
            {
                if (u.Name != null)
                    set.Add(u.Name.ToString());
            }

            // Include usings inside namespace declarations
            foreach (var ns in root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
            {
                foreach (var u in ns.Usings)
                {
                    if (u.Name != null)
                        set.Add(u.Name.ToString());
                }
            }

            return set;
        }

        private static bool IsSystemUsing(UsingDirectiveSyntax usingDirective)
        {
            string? name = usingDirective.Name?.ToString();
            return name != null && (name == "System" || name.StartsWith("System.", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Extension to add a range of items to a SyntaxList.
    /// </summary>
    internal static class SyntaxListExtensions
    {
        public static SyntaxList<T> AddRange<T>(this SyntaxList<T> list, IEnumerable<T> items)
            where T : SyntaxNode
        {
            return list.InsertRange(list.Count, items);
        }

        public static SyntaxList<T> ToSyntaxList<T>(this IEnumerable<T> items)
            where T : SyntaxNode
        {
            return SyntaxFactory.List(items);
        }
    }
}
