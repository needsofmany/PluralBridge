// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api.Controllers;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// Graph expansion returns one result set, and every direct production caller captures it.
/// These tests read source files. They do not open a database connection or execute SQL.
/// </summary>
public sealed class WorkspaceExpansionResultContractTests
{
	/// <summary>The deployment script and the canonical procedure are the same body.</summary>
	[Fact]
	public void ExpansionDeployment_MatchesCanonicalProcedure()
	{
		var canonical = Normalize(AlterBody(Sql("pb_show_tree_step5_graph_expansion.sql"))).Trim();
		var deployed = Normalize(AlterBody(Sql("Members", "014_graph_expansion_result_set.sql"))).Trim();

		Assert.Equal(canonical, deployed);
		Assert.Contains("@InputContractVersion int", canonical, StringComparison.Ordinal);
		Assert.Contains("@InputContractVersion <> 1", canonical, StringComparison.Ordinal);
		Assert.Contains("@WalkTopology dbo.pb_WalkTopologyRows READONLY", canonical, StringComparison.Ordinal);
		Assert.Contains("@TopologyEdges dbo.pb_GraphEdgeRows READONLY", canonical, StringComparison.Ordinal);
		Assert.Contains("@ExistingGraphEdges dbo.pb_GraphEdgeRows READONLY", canonical, StringComparison.Ordinal);
		Assert.Contains("CREATE TABLE #BuiltGraphEdges", canonical, StringComparison.Ordinal);
		Assert.DoesNotContain("#WalkTopology", canonical, StringComparison.Ordinal);
		Assert.DoesNotContain("#TopologyEdges", canonical, StringComparison.Ordinal);
		Assert.DoesNotContain("#GraphEdges", canonical, StringComparison.Ordinal);
		Assert.DoesNotContain("##", canonical, StringComparison.Ordinal);
		Assert.Equal(1, AnchorMemberProfileSave.GraphExpansionInputContractVersion);
	}

	/// <summary>Recovery restores the procedure that writes the caller graph table and returns no result set.</summary>
	[Fact]
	public void ExpansionRecovery_RestoresCallerTableWrites()
	{
		var recovery = Normalize(AlterBody(Sql("Members", "014_graph_expansion_result_set_recovery.sql")));

		Assert.Contains("INSERT INTO #GraphEdges", recovery, StringComparison.Ordinal);
		Assert.Contains("FROM #WalkTopology", recovery, StringComparison.Ordinal);
		Assert.Contains("FROM #TopologyEdges", recovery, StringComparison.Ordinal);
		Assert.DoesNotContain("@InputContractVersion", recovery, StringComparison.Ordinal);
		Assert.DoesNotContain("#BuiltGraphEdges", recovery, StringComparison.Ordinal);
	}

	/// <summary>Profile and step 4 capture expansion without moving their client result sets.</summary>
	[Fact]
	public void DatabaseCallers_CaptureBeforeTheirClientResults()
	{
		var profile = Normalize(AlterBody(Sql("Members", "015_profile_graph_expansion_capture.sql")));
		var profileRecovery = Normalize(AlterBody(Sql("Members", "015_profile_graph_expansion_capture_recovery.sql")));
		var installedProfile = Normalize(AlterBody(Sql("Members", "012_profile_seed_result_capture.sql")));
		var step = Normalize(AlterBody(Sql("Left Panel Tree", "pb_show_tree_step4_members_by_email.sql")));
		var stepDeploy = Normalize(AlterBody(Sql("Members", "016_step4_graph_expansion_capture.sql"))).Trim();
		var stepRecovery = Normalize(AlterBody(Sql("Members", "016_step4_graph_expansion_capture_recovery.sql")));

		Assert.Equal(installedProfile, profileRecovery);
		Assert.Equal(step.Trim(), stepDeploy);
		AssertCapturedBefore(profile, "INSERT INTO #AvatarBatchGraphEdges");
		AssertCapturedBefore(step, "EXEC dbo.pb_show_tree_step5_render_layer");
		Assert.Contains("SELECT N'Context'", step, StringComparison.Ordinal);
		Assert.True(step.IndexOf("SELECT N'Context'", StringComparison.Ordinal) < step.IndexOf("@InputContractVersion = 1", StringComparison.Ordinal));
		Assert.Contains("EXEC dbo.pb_show_tree_step5_graph_expansion", stepRecovery, StringComparison.Ordinal);
		Assert.DoesNotContain("@ExpansionRows", stepRecovery, StringComparison.Ordinal);
		Assert.Contains("EXEC dbo.pb_show_tree_step5_render_layer", stepRecovery, StringComparison.Ordinal);
	}

	/// <summary>Scripts and the application pass contract version 1 and replace graph edges after capture.</summary>
	[Fact]
	public void DirectCallers_PassTheContractVersionAndReplaceAfterCapture()
	{
		AssertCaptured(Sql("Members", "009_inherit_example_visibility_permissions.sql"));
		AssertCaptured(Sql("Members", "get_members_for_anchor.sql"));
		Assert.Equal(1, CountOf(Sql("Members", "009_inherit_example_visibility_permissions.sql"), "DECLARE @ExpansionRows TABLE"));

		var capture = AnchorMemberProfileSave.ExpansionResultCaptureSql;
		Assert.Contains("@InputContractVersion = 1", capture, StringComparison.Ordinal);
		Assert.True(capture.IndexOf("EXEC dbo.pb_show_tree_step5_graph_expansion", StringComparison.Ordinal) < capture.IndexOf("DELETE FROM #GraphEdges", StringComparison.Ordinal));

		var source = File.ReadAllText(ControllerFile("AnchorMemberProfileSave.cs"));
		var save = MethodBody(source, "internal static async Task<SavedProfile> ExecuteAsync(");
		var fronting = MethodBody(source, "internal static async Task<bool> ApplyFrontingAsync(");
		var avatar = MethodBody(source, "internal static async Task<(byte[] Bytes, string ContentType)?> ReadAvatarAsync(");
		Assert.Contains("ExpansionRowsDeclarationSql + ExpansionResultCaptureSql", save, StringComparison.Ordinal);
		Assert.Contains("ExpansionRowsDeclarationSql + ExpansionResultCaptureSql", fronting, StringComparison.Ordinal);
		Assert.Contains("ExpansionRowsDeclarationSql + ExpansionResultCaptureSql", avatar, StringComparison.Ordinal);

		var session = File.ReadAllText(WorkspaceFile("WorkspaceOpeningSession.cs"));
		var expansionMethod = MethodBody(session, "private static async Task ExecuteExpansionCaptureAsync(");
		Assert.Contains("ExpansionRowsDeclarationSql + AnchorMemberProfileSave.ExpansionResultCaptureSql", expansionMethod, StringComparison.Ordinal);
		Assert.Contains("transaction)", expansionMethod, StringComparison.Ordinal);
	}

	private static void AssertCapturedBefore(string sql, string laterMarker)
	{
		AssertCaptured(sql);
		var call = sql.IndexOf("EXEC dbo.pb_show_tree_step5_graph_expansion", StringComparison.Ordinal);
		var later = sql.IndexOf(laterMarker, StringComparison.Ordinal);
		Assert.True(later > call);
	}

	private static void AssertCaptured(string sql)
	{
		var text = Normalize(sql);
		var call = text.IndexOf("EXEC dbo.pb_show_tree_step5_graph_expansion", StringComparison.Ordinal);
		var replace = text.IndexOf("DELETE FROM #GraphEdges", call, StringComparison.Ordinal);
		Assert.True(call >= 0);
		Assert.True(replace > call);
		Assert.Contains("@InputContractVersion = 1", text, StringComparison.Ordinal);
		Assert.Contains("@ExistingGraphEdges = @ExistingGraphEdges", text, StringComparison.Ordinal);
	}

	private static string AlterBody(string text)
	{
		var normalized = Normalize(text).Replace("ALTER   PROCEDURE", "ALTER PROCEDURE", StringComparison.Ordinal);
		var marker = "\nALTER PROCEDURE [";
		var start = normalized.IndexOf(marker, StringComparison.Ordinal);
		Assert.True(start >= 0);
		var body = normalized[(start + 1)..];
		var stop = body.IndexOf("\nSET NOEXEC OFF", StringComparison.Ordinal);
		return stop >= 0 ? body[..stop] : body;
	}

	private static string Normalize(string text)
	{
		return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
	}

	private static int CountOf(string text, string value)
	{
		var count = 0;
		var index = 0;
		while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
		{
			count++;
			index += value.Length;
		}

		return count;
	}

	private static string MethodBody(string source, string signature)
	{
		var start = source.IndexOf(signature, StringComparison.Ordinal);
		Assert.True(start >= 0);
		var next = source.IndexOf("\n\tprivate static", start + signature.Length, StringComparison.Ordinal);
		if (next < 0)
		{
			next = source.IndexOf("\n\tinternal static", start + signature.Length, StringComparison.Ordinal);
		}

		Assert.True(next > start);
		return source[start..next];
	}

	private static string Sql(params string[] parts)
	{
		return File.ReadAllText(Path.GetFullPath(Path.Combine(new[] { RepositoryRoot(), "scripts", "sqlserver" }.Concat(parts).ToArray())));
	}

	private static string ControllerFile(string fileName)
	{
		return Path.GetFullPath(Path.Combine(RepositoryRoot(), "api", "PluralBridge.Api", "PluralBridge.Api", "Controllers", fileName));
	}

	private static string WorkspaceFile(string fileName)
	{
		return Path.GetFullPath(Path.Combine(RepositoryRoot(), "api", "PluralBridge.Api", "PluralBridge.Api", "Workspace", fileName));
	}

	private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
	{
		return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile) ?? string.Empty, "..", "..", ".."));
	}
}
