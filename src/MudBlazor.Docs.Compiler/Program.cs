﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ColorCode;
//using MarkdownSharp;

namespace MudBlazor.Docs.Compiler
{
    public class Program
    {
        const string DocDir = "MudBlazor.Docs";
        const string SnippetsFile = "Snippets.generated.cs";
        const string TestsFile = "_AllComponents.cs";
        const string ExampleDiscriminator = "Example";  // example components must contain this string

        static void Main(string[] args)
        {
            var path = Path.GetFullPath(".");
            var src_path = string.Join("/", path.Split('/', '\\').TakeWhile(x => x != "src").Concat(new[] { "src" }));
            var doc_path = Directory.EnumerateDirectories(src_path, DocDir).FirstOrDefault();
            if (doc_path == null)
                throw new InvalidOperationException("Directory not found: " + DocDir);
            var snippets_path = Directory.EnumerateFiles(doc_path, SnippetsFile, SearchOption.AllDirectories).FirstOrDefault();
            if (snippets_path == null)
                throw new InvalidOperationException("File not found: " + SnippetsFile);
            //Console.WriteLine(path);
            //Console.WriteLine(src_path);
            //Console.WriteLine(doc_path);
            //CreateSnippets(snippets_path, doc_path);
            CreateHilitedCode( doc_path);
            var test_path = Directory.EnumerateFiles(src_path, TestsFile, SearchOption.AllDirectories).FirstOrDefault();
            if (test_path == null)
                throw new InvalidOperationException("File not found: " + TestsFile);
            CreateTestsFromExamples(test_path, doc_path);
        }

        [Obsolete("We don'T need that any more")]
        private static void CreateSnippets(string snippets_path, string doc_path)
        {
            using (var f = File.Open(snippets_path, FileMode.Create))
            using (var w = new StreamWriter(f))
            {
                w.WriteLine("// NOTE: this file is autogenerated. Any changes will be overwritten!");
                w.WriteLine(
                    @"namespace MudBlazor.Docs.Models
{
    public static partial class Snippets
    {
");
                foreach (var entry in Directory.EnumerateFiles(doc_path, "*.razor", SearchOption.AllDirectories))
                {
                    var filename = Path.GetFileName(entry);
                    var component_name = Path.GetFileNameWithoutExtension(filename);
                    if (!filename.Contains(ExampleDiscriminator))
                        continue;
                    Console.WriteLine("Found code snippet: " + component_name);
                    w.WriteLine($"public const string {component_name} = @\"```html");
                    var escaped_src = EscapeComponentSource(entry);
                    w.WriteLine(escaped_src.Replace("@code {", "```\n\n```csharp\n@code {"));
                    w.WriteLine("```\";");
                }

                w.WriteLine(
                    @"    }
}
");
                w.Flush();
            }
        }

        [Obsolete("We don'T need that any more")]
        private static string EscapeComponentSource(string path)
        {
            var source = File.ReadAllText(path, Encoding.UTF8);
            source = Regex.Replace(source, "@using .+?\n", "");
            source = Regex.Replace(source, "@namespace .+?\n", "");
            return source.Replace("\"", "\"\"").Trim();
        }

        private static string StripComponentSource(string path)
        {
            var source = File.ReadAllText(path, Encoding.UTF8);
            source = Regex.Replace(source, "@using .+?\n", "");
            source = Regex.Replace(source, "@namespace .+?\n", "");
            return source.Trim();
        }

        private static void CreateHilitedCode(string doc_path)
        {
            //Markdown markdown = new Markdown();
            var formatter = new HtmlClassFormatter();
            foreach (var entry in Directory.EnumerateFiles(doc_path, "*.razor", SearchOption.AllDirectories).ToArray())
            {
                if (entry.EndsWith("Code.razor"))
                    continue;
                var filename = Path.GetFileName(entry);
                if (!filename.Contains(ExampleDiscriminator))
                    continue;
                //var component_name = Path.GetFileNameWithoutExtension(filename);
                var markup_path = entry.Replace("Examples", "Code").Replace(".razor", "Code.razor");
                var markup_dir = Path.GetDirectoryName(markup_path);
                if (!Directory.Exists(markup_dir))
                    Directory.CreateDirectory(markup_dir);
                //Console.WriteLine("Found code snippet: " + component_name);
                var src = StripComponentSource(entry);
                var blocks=src.Split("@code");
                // Note: the @ creates problems and thus we replace it with an unlikely placeholder and in the markup replace back.
                var html = formatter.GetHtmlString(blocks[0].Replace("@", "PlaceholdeR"), Languages.Html).Replace("PlaceholdeR", "&#64;");
                html = AttributePostprocessing(html);
                using (var f = File.Open(markup_path, FileMode.Create))
                using (var w = new StreamWriter(f))
                {
                    w.WriteLine("@* Auto-generated markup. Any changes will be overwritten *@");
                    w.WriteLine("@namespace MudBlazor.Docs.Examples.Markup");
                    w.WriteLine("<div class=\"mud-codeblock\">");
                    w.WriteLine(html);
                    if (blocks.Length == 2)
                    {
                        w.WriteLine(formatter.GetHtmlString("@code" + blocks[1], Languages.CSharp).Replace("@", "&#64;"));
                    }
                    w.WriteLine("</div>");
                    w.Flush();
                }
            }
        }

        public static string AttributePostprocessing(string html)
        {
            return Regex.Replace(html, @"<span class=""htmlAttributeValue"">&quot;(?'value'.*?)&quot;</span>", new MatchEvaluator(
                m =>
                {
                    var value = m.Groups["value"].Value;
                    return $@"<span class=""quot"">&quot;</span>{AttributeValuePostprocessing(value)}<span class=""quot"">&quot;</span>";
                }));
        }

        private static string AttributeValuePostprocessing(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;
            if (value == "true" || value == "false")
                return $"<span class=\"keyword\">{value}</span>";
            if (Regex.IsMatch(value, "^[A-Z][A-Za-z0-9]+[.][A-Za-z][A-Za-z0-9]+$"))
            {
                var tokens = value.Split('.');
                return $"<span class=\"enum\">{tokens[0]}</span><span class=\"enumValue\">.{tokens[1]}</span>";
            }
            return $"<span class=\"htmlAttributeValue\">{value}</span>";
        }


    private static void CreateTestsFromExamples(string testPath, string docPath)
        {
            using (var f = File.Open(testPath, FileMode.Create))
            using (var w = new StreamWriter(f))
            {
                w.WriteLine("// NOTE: this file is autogenerated. Any changes will be overwritten!");
                w.WriteLine(
@"using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using MudBlazor.UnitTests.Mocks;
using MudBlazor.Docs;
using MudBlazor.Dialog;

namespace MudBlazor.UnitTests.Components
{
    [TestFixture]
    public class _AllComponents
    {
        // These tests just check if all the examples from the doc page render without errors

");
                foreach (var entry in Directory.EnumerateFiles(docPath, "*.razor", SearchOption.AllDirectories))
                {
                    if (entry.EndsWith("Code.razor"))
                        continue;
                    var filename = Path.GetFileName(entry);
                    var component_name = Path.GetFileNameWithoutExtension(filename);
                    if (!filename.Contains(ExampleDiscriminator))
                        continue;
                    w.WriteLine(
@$"
        [Test]
        public void {component_name}_Test()
        {{
                using var ctx = new Bunit.TestContext();
                ctx.Services.AddSingleton<NavigationManager>(new MockNavigationManager());
                ctx.Services.AddSingleton<IDialogService>(new DialogService());
                var comp = ctx.RenderComponent<{component_name}>();
        }}
");
                }

                w.WriteLine(
                    @"    }
}
");
                w.Flush();
            }
        }
    }

}
