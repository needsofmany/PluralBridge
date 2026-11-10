// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

internal sealed record AnchorMemberProfile(
	Guid MemberId,
	string DisplayName,
	string? Pronouns,
	string? Description,
	string? Color);

internal sealed class AnchorMemberProfileResultSet
{
	public required IReadOnlyList<AnchorMemberProfileColumn> Columns { get; init; }

	public required IReadOnlyList<IReadOnlyList<object>> Rows { get; init; }
}

internal readonly record struct AnchorMemberProfileColumn(string Name, string SqlTypeName);

internal static class AnchorMemberProfileReader
{
	private static readonly AnchorMemberProfileColumn[] ProfileColumns =
	[
		new("MemberId", "uniqueidentifier"),
		new("DisplayName", "nvarchar"),
		new("Pronouns", "nvarchar"),
		new("Description", "nvarchar"),
		new("Color", "nvarchar")
	];

	internal static async Task<IReadOnlyList<AnchorMemberProfile>?> ReadAsync(
		SqlDataReader reader,
		CancellationToken cancellationToken)
	{
		var resultSets = new List<AnchorMemberProfileResultSet>();

		do
		{
			var columns = new AnchorMemberProfileColumn[reader.FieldCount];
			for (var index = 0; index < reader.FieldCount; index++)
			{
				columns[index] = new AnchorMemberProfileColumn(
					reader.GetName(index),
					reader.GetDataTypeName(index));
			}

			var rows = new List<IReadOnlyList<object>>();
			while (await reader.ReadAsync(cancellationToken))
			{
				var values = new object[reader.FieldCount];
				reader.GetValues(values);
				rows.Add(values);
			}

			resultSets.Add(new AnchorMemberProfileResultSet
			{
				Columns = columns,
				Rows = rows
			});
		}
		while (await reader.NextResultAsync(cancellationToken));

		return Map(resultSets);
	}

	internal static IReadOnlyList<AnchorMemberProfile>? Map(
		IReadOnlyList<AnchorMemberProfileResultSet> resultSets)
	{
		if (resultSets.Count != 1 || !ColumnsMatch(resultSets[0], ProfileColumns))
		{
			return null;
		}

		var profiles = new List<AnchorMemberProfile>(resultSets[0].Rows.Count);
		foreach (var row in resultSets[0].Rows)
		{
			if (!TryProfileRow(row, out var profile))
			{
				return null;
			}

			profiles.Add(profile);
		}

		return profiles;
	}

	private static bool ColumnsMatch(
		AnchorMemberProfileResultSet resultSet,
		IReadOnlyList<AnchorMemberProfileColumn> expected)
	{
		if (resultSet.Columns.Count != expected.Count)
		{
			return false;
		}

		for (var index = 0; index < expected.Count; index++)
		{
			var actual = resultSet.Columns[index];
			var required = expected[index];
			if (!string.Equals(actual.Name, required.Name, StringComparison.Ordinal)
				|| !string.Equals(actual.SqlTypeName, required.SqlTypeName, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}

		return true;
	}

	private static bool TryProfileRow(
		IReadOnlyList<object> row,
		out AnchorMemberProfile profile)
	{
		if (row.Count == ProfileColumns.Length
			&& row[0] is Guid memberId
			&& row[1] is string displayName
			&& TryOptionalText(row[2], out var pronouns)
			&& TryOptionalText(row[3], out var description)
			&& TryOptionalText(row[4], out var color))
		{
			profile = new AnchorMemberProfile(memberId, displayName, pronouns, description, color);
			return true;
		}

		profile = null!;
		return false;
	}

	private static bool TryOptionalText(object value, out string? text)
	{
		if (value is DBNull)
		{
			text = null;
			return true;
		}

		if (value is string content)
		{
			text = content;
			return true;
		}

		text = null;
		return false;
	}
}
