// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api.Controllers;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// The seed procedure returns one result set, and every direct production caller captures it.
/// These tests read source files. They do not open a database connection or execute SQL.
/// </summary>
public sealed class WorkspaceSeedResultContractTests
{
	/// <summary>The deployment script and the canonical source are the same procedure.</summary>
	[Fact]
	public void SeedDeployment_MatchesCanonicalProcedure()
	{
		var canonical = Normalize(AlterBody(Sql("pb_show_tree_step5_topology_seed_builder.sql")));
		var deployed = Normalize(AlterBody(Sql("Members", "011_topology_seed_result_set.sql")));

		Assert.Equal(canonical, deployed);
		Assert.Contains("CREATE TABLE #BuiltTopologyEdges", canonical, StringComparison.Ordinal);
		Assert.Contains("CREATE TABLE #BuiltSystemEdgeLabels", canonical, StringComparison.Ordinal);
		Assert.Contains("SystemId uniqueidentifier NOT NULL PRIMARY KEY", canonical, StringComparison.Ordinal);
		Assert.Contains("CAST(N'edge' AS nvarchar(8))", canonical, StringComparison.Ordinal);
		Assert.Contains("CAST(N'label' AS nvarchar(8))", canonical, StringComparison.Ordinal);
		Assert.Contains("CAST(NULL AS nvarchar(max))", canonical, StringComparison.Ordinal);
		Assert.DoesNotContain("#TopologyEdges", canonical, StringComparison.Ordinal);
		Assert.DoesNotContain("#SystemEdgeLabels", canonical, StringComparison.Ordinal);
	}

	/// <summary>Recovery restores the procedure that writes the caller tables and returns no result set.</summary>
	[Fact]
	public void SeedRecovery_RestoresCallerTableWrites()
	{
		var recovery = Normalize(AlterBody(Sql("Members", "011_topology_seed_result_set_recovery.sql")));

		Assert.Contains("INSERT INTO #TopologyEdges", recovery, StringComparison.Ordinal);
		Assert.Contains("INSERT INTO #SystemEdgeLabels", recovery, StringComparison.Ordinal);
		Assert.DoesNotContain("#BuiltTopologyEdges", recovery, StringComparison.Ordinal);
		Assert.DoesNotContain("CAST(N'edge' AS nvarchar(8))", recovery, StringComparison.Ordinal);
	}

	/// <summary>The already deployed profile script stays a bare seed call. The new script captures the result.</summary>
	[Fact]
	public void ProfileDeployment_CapturesTheSeedResult()
	{
		var current = Normalize(AlterBody(Sql("Members", "010_profile_avatar_batch_graph_copy.sql")));
		var recovery = Normalize(AlterBody(Sql("Members", "012_profile_seed_result_capture_recovery.sql")));
		var next = Normalize(AlterBody(Sql("Members", "012_profile_seed_result_capture.sql")));

		Assert.Equal(current, recovery);
		Assert.Contains("EXEC dbo.pb_show_tree_step5_topology_seed_builder", current, StringComparison.Ordinal);
		Assert.DoesNotContain("@SeedRows", current, StringComparison.Ordinal);
		Assert.Equal(1, CountOf(next, "EXEC dbo.pb_show_tree_step5_topology_seed_builder"));
		Assert.True(next.IndexOf("INSERT INTO @SeedRows", StringComparison.Ordinal) < next.IndexOf("EXEC dbo.pb_show_tree_step5_topology_seed_builder", StringComparison.Ordinal));
		Assert.Contains("INSERT INTO #AvatarBatchGraphEdges", next, StringComparison.Ordinal);
		Assert.Contains("WHERE RowKind = N'edge'", next, StringComparison.Ordinal);
		Assert.Contains("WHERE RowKind = N'label'", next, StringComparison.Ordinal);
	}

	/// <summary>Every other direct SQL caller consumes the seed result before its own result sets.</summary>
	[Fact]
	public void DirectSqlCallers_CaptureTheSeedResult()
	{
		AssertCaptured(Sql("Left Panel Tree", "pb_show_tree_step4_members_by_email.sql"));
		AssertCaptured(Sql("Members", "get_members_for_anchor.sql"));
		AssertCaptured(Sql("Members", "009_inherit_example_visibility_permissions.sql"));
	}

	/// <summary>The opening seed uses the capture batch. Walker, expansion, and the labeler stay on the procedure command.</summary>
	[Fact]
	public void OpeningSession_CapturesOnlyTheSeedProcedure()
	{
		var session = File.ReadAllText(WorkspaceFile("WorkspaceOpeningSession.cs"));
		var seed = session.IndexOf("command.Text == WorkspaceOpeningPlan.SeedProcedure", StringComparison.Ordinal);
		var capture = session.IndexOf("ExecuteSeedCaptureAsync", StringComparison.Ordinal);

		Assert.True(seed >= 0);
		Assert.True(capture > seed);
		Assert.Contains("ExecuteProcedureAsync", session, StringComparison.Ordinal);
		Assert.Contains(WorkspaceOpeningPlan.SeedProcedure, AnchorMemberProfileSave.SeedResultCaptureSql, StringComparison.Ordinal);
		Assert.Equal(1, CountOf(AnchorMemberProfileSave.SeedResultCaptureSql, "EXEC " + WorkspaceOpeningPlan.SeedProcedure));
	}

	/// <summary>Both save batches and the single-avatar read use the same capture text.</summary>
	[Fact]
	public void SaveAndAvatar_UseTheSharedCaptureBatch()
	{
		var source = File.ReadAllText(ControllerFile("AnchorMemberProfileSave.cs"));
		var save = MethodBody(source, "internal static async Task<SavedProfile> ExecuteAsync(");
		var fronting = MethodBody(source, "internal static async Task<bool> ApplyFrontingAsync(");
		var avatar = MethodBody(source, "internal static async Task<(byte[] Bytes, string ContentType)?> ReadAvatarAsync(");

		Assert.Equal(1, CountOf(source, "internal const string SeedResultCaptureSql"));
		Assert.Contains("SeedRowsDeclarationSql + SeedResultCaptureSql", save, StringComparison.Ordinal);
		Assert.Contains("SeedRowsDeclarationSql + SeedResultCaptureSql", fronting, StringComparison.Ordinal);
		Assert.Contains("SeedRowsDeclarationSql + SeedResultCaptureSql", avatar, StringComparison.Ordinal);
	}

	private static void AssertCaptured(string sql)
	{
		var text = Normalize(sql);
		var capture = text.IndexOf("INSERT INTO @SeedRows", StringComparison.Ordinal);
		var call = text.IndexOf("EXEC dbo.pb_show_tree_step5_topology_seed_builder", StringComparison.Ordinal);
		Assert.True(capture >= 0);
		Assert.True(call > capture);
		Assert.Contains("WHERE RowKind = N'edge'", text, StringComparison.Ordinal);
		Assert.Contains("WHERE RowKind = N'label'", text, StringComparison.Ordinal);
	}

	private static string AlterBody(string text)
	{
		var normalized = Normalize(text);
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
		var next = source.IndexOf("\n\tinternal static", start + signature.Length, StringComparison.Ordinal);
		if (next < 0)
		{
			next = source.IndexOf("\n\tprivate static", start + signature.Length, StringComparison.Ordinal);
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
