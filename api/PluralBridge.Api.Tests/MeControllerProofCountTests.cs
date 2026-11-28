// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Tests;

/// <summary>
/// The legacy proof counts use columns that exist on each table.
/// This reads the production source. It does not open a connection or execute SQL.
/// </summary>
public sealed class MeControllerProofCountTests
{
	/// <summary>
	/// Custom fields are counted by the membership account. The other proof counts keep their system filter.
	/// </summary>
	[Fact]
	public void CustomFieldCount_UsesAccountIdFromTheMemberships()
	{
		var source = File.ReadAllText(ControllerPath());
		var method = MethodBody(source, "private static async Task<Dictionary<string, long>> ReadCountsAsync(");

		Assert.Contains("membership.AccountId", method, StringComparison.Ordinal);
		Assert.Contains("FROM dbo.pb_custom_fields WHERE AccountId IN ({accountIdInClause})", method, StringComparison.Ordinal);
		Assert.DoesNotContain("pb_custom_fields WHERE SystemId", method, StringComparison.Ordinal);
		Assert.Contains("command.Parameters.AddWithValue($\"@AccountId{index}\", accountIds[index])", method, StringComparison.Ordinal);

		Assert.Contains("FROM dbo.pb_systems WHERE SystemId IN ({systemIdInClause})", method, StringComparison.Ordinal);
		Assert.Contains("FROM dbo.pb_members WHERE SystemId IN ({systemIdInClause})", method, StringComparison.Ordinal);
		Assert.Contains("FROM dbo.pb_privacy_buckets WHERE SystemId IN ({systemIdInClause})", method, StringComparison.Ordinal);
		Assert.Contains("FROM dbo.pb_front_history WHERE SystemId IN ({systemIdInClause})", method, StringComparison.Ordinal);
		Assert.Contains("TRY_CONVERT(uniqueidentifier, sim.PluralBridgeId) IN ({systemIdInClause})", method, StringComparison.Ordinal);
		Assert.Contains("FROM dbo.pb_source_records AS sr WHERE sr.ImportBatchId IN ({sourceBatchFilter})", method, StringComparison.Ordinal);
		Assert.Contains("ss.SourceSystemCode IN", method, StringComparison.Ordinal);
	}

	private static string MethodBody(string source, string signature)
	{
		var start = source.IndexOf(signature, StringComparison.Ordinal);
		Assert.True(start >= 0);
		var next = source.IndexOf("\n\tprivate static async Task<ProofSystem?>", start + signature.Length, StringComparison.Ordinal);
		Assert.True(next > start);
		return source[start..next];
	}

	private static string ControllerPath([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
	{
		return Path.GetFullPath(Path.Combine(
			Path.GetDirectoryName(sourceFile) ?? string.Empty,
			"..",
			"PluralBridge.Api",
			"PluralBridge.Api",
			"Controllers",
			"MeController.cs"));
	}
}
