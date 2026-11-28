// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Tests;

/// <summary>
/// Custom field rows are read by the authorized account after the route system is checked.
/// This reads the production source. It does not open a connection or execute SQL.
/// </summary>
public sealed class CustomFieldsControllerTests
{
	/// <summary>
	/// The query filters <c>AccountId</c> from the current account. The response system id stays the authorized system.
	/// </summary>
	[Fact]
	public void Read_FiltersByCurrentAccountAndKeepsTheRouteSystem()
	{
		var source = File.ReadAllText(ControllerPath());
		var action = MethodBody(source, "public async Task<IActionResult> Get(Guid systemId)");
		var reader = MethodBody(source, "private static async Task<List<CustomField>> ReadCustomFieldsAsync(");

		Assert.Contains("currentSystem.SystemId != systemId", action, StringComparison.Ordinal);
		Assert.Contains("accessContext.CurrentAccount.AccountId", action, StringComparison.Ordinal);
		Assert.Contains("systemId = currentSystem.SystemId", action, StringComparison.Ordinal);
		Assert.DoesNotContain("ReadCustomFieldsAsync(\n\t\t\t\t\tconnection,\n\t\t\t\t\tcurrentSystem.SystemId)", action, StringComparison.Ordinal);

		Assert.Contains("WHERE AccountId = @AccountId", reader, StringComparison.Ordinal);
		Assert.Contains("Add(\"@AccountId\", System.Data.SqlDbType.UniqueIdentifier).Value = accountId", reader, StringComparison.Ordinal);
		Assert.DoesNotContain("SystemId", reader, StringComparison.Ordinal);
		Assert.Contains("reader.GetString(1)", reader, StringComparison.Ordinal);
		Assert.Contains("string FieldName", source, StringComparison.Ordinal);
	}

	private static string MethodBody(string source, string signature)
	{
		var start = source.IndexOf(signature, StringComparison.Ordinal);
		Assert.True(start >= 0);
		var next = source.IndexOf("\n\tprivate static", start + signature.Length, StringComparison.Ordinal);
		if (next < 0)
		{
			next = source.IndexOf("\n\tprivate sealed", start + signature.Length, StringComparison.Ordinal);
		}

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
			"CustomFieldsController.cs"));
	}
}
