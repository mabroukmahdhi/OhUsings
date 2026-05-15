using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using OhUsings.Services;
using Xunit;

namespace OhUsings.Tests
{
    public class MissingUsingsAnalyzerTests
    {
        private static readonly MetadataReference[] DefaultReferences = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        #region Helpers

        private static Document CreateDocument(string sourceCode, params MetadataReference[] additionalReferences)
        {
            var workspace = new AdhocWorkspace();
            var project = workspace.AddProject("TestProject", LanguageNames.CSharp);

            project = project
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddMetadataReferences(DefaultReferences)
                .AddMetadataReferences(additionalReferences);

            var document = project.AddDocument("TestFile.cs", SourceText.From(sourceCode));
            return document;
        }

        private static async Task<IReadOnlyList<string>> GetExtractedNamesAsync(string sourceCode)
        {
            var document = CreateDocument(sourceCode);
            var semanticModel = await document.GetSemanticModelAsync();
            var root = await document.GetSyntaxRootAsync();
            var diagnostics = semanticModel!.GetDiagnostics();

            var analyzer = new MissingUsingsAnalyzer();
            return analyzer.ExtractUnresolvedNames(diagnostics, root!);
        }

        #endregion

        [Fact]
        public async Task ExtractUnresolvedNames_FindsMissingType()
        {
            string code = @"
namespace Test
{
    public class Foo
    {
        public void Bar()
        {
            var x = new StringBuilder();
        }
    }
}";
            var names = await GetExtractedNamesAsync(code);

            Assert.Contains("StringBuilder", names);
        }

        [Fact]
        public async Task ExtractUnresolvedNames_IgnoresKeywords()
        {
            string code = @"
namespace Test
{
    public class Foo
    {
        public void Bar()
        {
            string s = ""hello"";
        }
    }
}";
            var names = await GetExtractedNamesAsync(code);

            Assert.DoesNotContain("string", names);
            Assert.DoesNotContain("void", names);
        }

        [Fact]
        public async Task ExtractUnresolvedNames_IgnoresSingleCharacterNames()
        {
            string code = @"
namespace Test
{
    public class Foo<T>
    {
        public T Value { get; set; }
    }
}";
            var names = await GetExtractedNamesAsync(code);

            Assert.DoesNotContain("T", names);
        }

        [Fact]
        public async Task ExtractUnresolvedNames_NoDuplicates()
        {
            string code = @"
namespace Test
{
    public class Foo
    {
        public void Bar()
        {
            var a = new StringBuilder();
            var b = new StringBuilder();
            var c = new StringBuilder();
        }
    }
}";
            var names = await GetExtractedNamesAsync(code);

            int count = names.Count(n => n == "StringBuilder");
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task ExtractUnresolvedNames_FindsMultipleMissingTypes()
        {
            string code = @"
namespace Test
{
    public class Foo
    {
        public void Bar()
        {
            var a = new StringBuilder();
            var list = new List<int>();
        }
    }
}";
            var names = await GetExtractedNamesAsync(code);

            Assert.Contains("StringBuilder", names);
            Assert.Contains("List", names);
        }

        [Fact]
        public void GetExistingUsings_ReturnsImportedNamespaces()
        {
            string code = @"
using System;
using System.Collections.Generic;

namespace Test
{
    public class Foo { }
}";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var usings = MissingUsingsAnalyzer.GetExistingUsings(root);

            Assert.Contains("System", usings);
            Assert.Contains("System.Collections.Generic", usings);
        }

        [Fact]
        public void GetExistingUsings_IncludesUsingsInsideNamespace()
        {
            string code = @"
namespace Test
{
    using System.Text;

    public class Foo { }
}";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var usings = MissingUsingsAnalyzer.GetExistingUsings(root);

            Assert.Contains("System.Text", usings);
        }

        [Fact]
        public async Task AnalyzeAsync_ReturnsEmptyForCleanDocument()
        {
            string code = @"
using System;

namespace Test
{
    public class Foo
    {
        public void Bar()
        {
            Console.WriteLine(""hello"");
        }
    }
}";
            var document = CreateDocument(code);
            var analyzer = new MissingUsingsAnalyzer();

            var candidates = await analyzer.AnalyzeAsync(document);

            Assert.Empty(candidates);
        }

        [Fact]
        public async Task FindCandidateNamespaces_FindsSystemText_ForStringBuilder()
        {
            string code = @"
namespace Test
{
    public class Foo
    {
        public void Bar()
        {
            var x = new StringBuilder();
        }
    }
}";
            var document = CreateDocument(code,
                MetadataReference.CreateFromFile(typeof(System.Text.StringBuilder).Assembly.Location));

            var existingUsings = new System.Collections.Generic.HashSet<string>();
            var namespaces = await MissingUsingsAnalyzer.FindCandidateNamespacesAsync(
                "StringBuilder", document, existingUsings, default);

            Assert.Contains("System.Text", namespaces);
        }

        [Fact]
        public async Task FindCandidateNamespaces_ExcludesAlreadyImported()
        {
            string code = @"
using System.Text;

namespace Test
{
    public class Foo
    {
        public void Bar()
        {
            var x = new StringBuilder();
        }
    }
}";
            var document = CreateDocument(code,
                MetadataReference.CreateFromFile(typeof(System.Text.StringBuilder).Assembly.Location));

            var existingUsings = new System.Collections.Generic.HashSet<string> { "System.Text" };
            var namespaces = await MissingUsingsAnalyzer.FindCandidateNamespacesAsync(
                "StringBuilder", document, existingUsings, default);

            Assert.DoesNotContain("System.Text", namespaces);
        }
    }
}
