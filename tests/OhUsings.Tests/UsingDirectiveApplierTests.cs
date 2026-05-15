using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OhUsings.Services;
using Xunit;

namespace OhUsings.Tests
{
    public class UsingDirectiveApplierTests
    {
        private static readonly MetadataReference[] DefaultReferences = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        #region Helpers

        private static Document CreateDocument(string sourceCode)
        {
            var workspace = new AdhocWorkspace();
            var project = workspace.AddProject("TestProject", LanguageNames.CSharp);

            project = project
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddMetadataReferences(DefaultReferences);

            var document = project.AddDocument("TestFile.cs", SourceText.From(sourceCode));
            return document;
        }

        private static async Task<CompilationUnitSyntax> GetRootAsync(Document document)
        {
            var root = await document.GetSyntaxRootAsync();
            return (CompilationUnitSyntax)root!;
        }

        private static IEnumerable<string> GetUsingNames(CompilationUnitSyntax root)
        {
            return root.Usings
                .Where(u => u.Name != null)
                .Select(u => u.Name!.ToString());
        }

        #endregion

        [Fact]
        public async Task ApplyAsync_AddsNewUsings()
        {
            string code = @"
namespace Test
{
    public class Foo { }
}";
            var document = CreateDocument(code);
            var applier = new UsingDirectiveApplier();

            var result = await applier.ApplyAsync(document, new[] { "System.Text", "System.Linq" });
            var root = await GetRootAsync(result);
            var usingNames = GetUsingNames(root).ToList();

            Assert.Contains("System.Text", usingNames);
            Assert.Contains("System.Linq", usingNames);
        }

        [Fact]
        public async Task ApplyAsync_DoesNotDuplicateExistingUsings()
        {
            string code = @"using System;

namespace Test
{
    public class Foo { }
}";
            var document = CreateDocument(code);
            var applier = new UsingDirectiveApplier();

            var result = await applier.ApplyAsync(document, new[] { "System", "System.Text" });
            var root = await GetRootAsync(result);
            var usingNames = GetUsingNames(root).ToList();

            int systemCount = usingNames.Count(n => n == "System");
            Assert.Equal(1, systemCount);
            Assert.Contains("System.Text", usingNames);
        }

        [Fact]
        public async Task ApplyAsync_SortsUsingsAlphabetically()
        {
            string code = @"using Zebra;

namespace Test
{
    public class Foo { }
}";
            var document = CreateDocument(code);
            var applier = new UsingDirectiveApplier();

            var result = await applier.ApplyAsync(document, new[] { "Apple", "Mango" });
            var root = await GetRootAsync(result);
            var usingNames = GetUsingNames(root).ToList();

            Assert.True(usingNames.IndexOf("Apple") < usingNames.IndexOf("Mango"));
            Assert.True(usingNames.IndexOf("Mango") < usingNames.IndexOf("Zebra"));
        }

        [Fact]
        public async Task ApplyAsync_SystemNamespacesFirst()
        {
            string code = @"namespace Test
{
    public class Foo { }
}";
            var document = CreateDocument(code);
            var applier = new UsingDirectiveApplier();

            var result = await applier.ApplyAsync(document,
                new[] { "Newtonsoft.Json", "System.Text", "System" });
            var root = await GetRootAsync(result);
            var usingNames = GetUsingNames(root).ToList();

            // System namespaces should come before non-System
            int systemIdx = usingNames.IndexOf("System");
            int systemTextIdx = usingNames.IndexOf("System.Text");
            int newtonsoftIdx = usingNames.IndexOf("Newtonsoft.Json");

            Assert.True(systemIdx < newtonsoftIdx);
            Assert.True(systemTextIdx < newtonsoftIdx);
        }

        [Fact]
        public async Task ApplyAsync_ReturnsOriginalDocument_WhenNoNamespacesToAdd()
        {
            string code = @"namespace Test
{
    public class Foo { }
}";
            var document = CreateDocument(code);
            var applier = new UsingDirectiveApplier();

            var result = await applier.ApplyAsync(document, new List<string>());

            // Document should be unchanged
            var originalText = (await document.GetTextAsync()).ToString();
            var resultText = (await result.GetTextAsync()).ToString();
            Assert.Equal(originalText, resultText);
        }

        [Fact]
        public async Task ApplyAsync_PreservesExistingUsings()
        {
            string code = @"using System;
using System.Collections.Generic;

namespace Test
{
    public class Foo { }
}";
            var document = CreateDocument(code);
            var applier = new UsingDirectiveApplier();

            var result = await applier.ApplyAsync(document, new[] { "System.Text" });
            var root = await GetRootAsync(result);
            var usingNames = GetUsingNames(root).ToList();

            Assert.Contains("System", usingNames);
            Assert.Contains("System.Collections.Generic", usingNames);
            Assert.Contains("System.Text", usingNames);
        }
    }
}
