// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.Markdown.Myst.Directives.Settings;

public class SettingsViewModel
{
	public required YamlSettings SettingsCollection { get; init; }

	public required Func<string, string> RenderMarkdown { get; init; }
}
