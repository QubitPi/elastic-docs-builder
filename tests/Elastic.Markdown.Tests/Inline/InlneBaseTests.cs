// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.IO.Abstractions.TestingHelpers;
using System.Runtime.InteropServices;
using Elastic.Documentation;
using Elastic.Documentation.Configuration;
using Elastic.Documentation.Configuration.Versions;
using Elastic.Markdown.IO;
using FluentAssertions;
using JetBrains.Annotations;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Elastic.Markdown.Tests.Inline;

public abstract class LeafTest<TDirective>(ITestOutputHelper output, [LanguageInjection("markdown")] string content)
	: InlineTest(output, content)
	where TDirective : LeafInline
{
	protected TDirective? Block { get; private set; }

	public override async ValueTask InitializeAsync()
	{
		await base.InitializeAsync();
		Block = Document
			.Descendants<TDirective>()
			.FirstOrDefault();
	}

	[Fact]
	public void BlockIsNotNull() => Block.Should().NotBeNull();

}

public abstract class BlockTest<TDirective>(ITestOutputHelper output, [LanguageInjection("markdown")] string content)
	: InlineTest(output, content, new Dictionary<string, string> { { "a-variable", "This is a variable" } })
	where TDirective : Block
{
	protected TDirective? Block { get; private set; }

	public override async ValueTask InitializeAsync()
	{
		await base.InitializeAsync();
		Block = Document
			.Descendants<TDirective>()
			.FirstOrDefault();
	}

	[Fact]
	public void BlockIsNotNull() => Block.Should().NotBeNull();

}

public abstract class InlineTest<TDirective>(ITestOutputHelper output, [LanguageInjection("markdown")] string content, Dictionary<string, string>? globalVariables = null)
	: InlineTest(output, content, globalVariables)
	where TDirective : ContainerInline
{
	protected TDirective? Block { get; private set; }

	public override async ValueTask InitializeAsync()
	{
		await base.InitializeAsync();
		Block = Document
			.Descendants<TDirective>()
			.FirstOrDefault();
	}

	[Fact]
	public void BlockIsNotNull() => Block.Should().NotBeNull();

}
public abstract class InlineTest : IAsyncLifetime
{
	protected MarkdownFile File { get; }
	protected string Html { get; private set; }
	protected MarkdownDocument Document { get; private set; }
	protected TestDiagnosticsCollector Collector { get; }
	protected MockFileSystem FileSystem { get; }
	protected DocumentationSet Set { get; }

	private bool TestingFullDocument { get; }

	protected InlineTest(
		ITestOutputHelper output,
		[LanguageInjection("markdown")] string content,
		Dictionary<string, string>? globalVariables = null)
	{
		var logger = new TestLoggerFactory(output);
		TestingFullDocument = string.IsNullOrEmpty(content) || content.StartsWith("---", StringComparison.OrdinalIgnoreCase);

		var documentContents = TestingFullDocument ? content :
// language=markdown
$"""
 # Test Document

 {content}
 """;

		FileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
		{
			{ "docs/index.md", new MockFileData(documentContents) }
		}, new MockFileSystemOptions
		{
			CurrentDirectory = Paths.WorkingDirectoryRoot.FullName,
		});
		// ReSharper disable once VirtualMemberCallInConstructor
		// nasty but sub implementations won't use class state.
		AddToFileSystem(FileSystem);
		var baseRootPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
		 ? Paths.WorkingDirectoryRoot.FullName.Replace('\\', '/')
		 : Paths.WorkingDirectoryRoot.FullName;
		var root = FileSystem.DirectoryInfo.New($"{baseRootPath}/docs/");
		FileSystem.GenerateDocSetYaml(root, globalVariables);

		Collector = new TestDiagnosticsCollector(output);
		var versionsConfig = new VersionsConfiguration
		{
			VersioningSystems = new Dictionary<VersioningSystemId, VersioningSystem>
			{
				{
					VersioningSystemId.Stack, new VersioningSystem
					{
						Id = VersioningSystemId.Stack,
						Current = new SemVersion(8, 0, 0),
						Base = new SemVersion(8, 0, 0)
					}
				}
			}
		};
		var context = new BuildContext(Collector, FileSystem, versionsConfig)
		{
			UrlPathPrefix = "/docs"
		};
		var linkResolver = new TestCrossLinkResolver();
		Set = new DocumentationSet(context, logger, linkResolver);
		File = Set.DocumentationFileLookup(FileSystem.FileInfo.New("docs/index.md")) as MarkdownFile ?? throw new NullReferenceException();
		Html = default!; //assigned later
		Document = default!;
	}

	protected virtual void AddToFileSystem(MockFileSystem fileSystem) { }

	public virtual async ValueTask InitializeAsync()
	{
		_ = Collector.StartAsync(TestContext.Current.CancellationToken);

		await Set.ResolveDirectoryTree(TestContext.Current.CancellationToken);
		await Set.LinkResolver.FetchLinks(TestContext.Current.CancellationToken);

		Document = await File.ParseFullAsync(TestContext.Current.CancellationToken);
		var html = MarkdownFile.CreateHtml(Document).AsSpan();
		var find = "</h1>\n</section>";
		var start = html.IndexOf(find, StringComparison.Ordinal);
		Html = start >= 0 && !TestingFullDocument
			? html[(start + find.Length)..].ToString().Trim(Environment.NewLine.ToCharArray())
			: html.ToString().Trim(Environment.NewLine.ToCharArray());
		await Collector.StopAsync(TestContext.Current.CancellationToken);
	}

	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}
