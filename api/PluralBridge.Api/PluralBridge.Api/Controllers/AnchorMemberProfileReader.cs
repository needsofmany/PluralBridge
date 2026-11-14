// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

internal sealed class AnchorProfileContractException : Exception
{
	internal AnchorProfileContractException(string message)
		: base(message)
	{
	}
}

internal sealed record AnchorProfileBundle(
	IReadOnlyList<AnchorMemberProfile> Profiles,
	bool BranchValid,
	IReadOnlyList<AnchorBranchAnchor> BranchAnchors,
	IReadOnlyList<AnchorMemberPlacement> Placements,
	IReadOnlyList<AnchorMemberFronting> Fronting,
	IReadOnlyList<AnchorMemberAvatar> Avatars);

internal sealed record AnchorMemberPlacement(
	Guid MemberId,
	string? VisibilityMode,
	int? VisibilityTypeId,
	string? PermissionMode,
	string? PermissionCode);

internal sealed record AnchorMemberFronting(Guid MemberId, bool IsFronting);

internal sealed record AnchorMemberAvatar(Guid MemberId, DateTime UpdatedAtUtc);

internal sealed record AnchorBranchAnchor(
	string ObjectType,
	Guid ObjectId,
	string? VisibilityMode,
	int? VisibilityTypeId,
	string? PermissionMode,
	string? PermissionCode);

internal sealed record AnchorMemberProfile(
	Guid MemberId,
	string DisplayName,
	string? Pronouns,
	string? Description,
	string? Color,
	int? VisibilityTypeId,
	string? VisibilityTypeCode,
	string? VisibilityTypeName,
	bool IsFronting);

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
		new("Color", "nvarchar"),
		new("VisibilityTypeId", "int"),
		new("VisibilityTypeCode", "nvarchar"),
		new("VisibilityTypeName", "nvarchar"),
		new("IsFronting", "bit")
	];

	private static readonly AnchorMemberProfileColumn[] BranchValidColumns =
	[
		new("BranchValid", "bit")
	];

	private static readonly AnchorMemberProfileColumn[] BranchAnchorColumns =
	[
		new("Ordinal", "int"),
		new("ObjectType", "nvarchar"),
		new("ObjectId", "uniqueidentifier"),
		new("VisibilityMode", "nvarchar"),
		new("VisibilityTypeId", "int"),
		new("PermissionMode", "nvarchar"),
		new("PermissionCode", "nvarchar")
	];

	private static readonly AnchorMemberProfileColumn[] PlacementColumns =
	[
		new("MemberId", "uniqueidentifier"),
		new("VisibilityMode", "nvarchar"),
		new("VisibilityTypeId", "int"),
		new("PermissionMode", "nvarchar"),
		new("PermissionCode", "nvarchar")
	];

	private static readonly AnchorMemberProfileColumn[] FrontingColumns =
	[
		new("MemberId", "uniqueidentifier"),
		new("IsFronting", "bit")
	];

	private static readonly AnchorMemberProfileColumn[] AvatarColumns =
	[
		new("MemberId", "uniqueidentifier"),
		new("UpdatedAtUtc", "datetime2")
	];

	internal static async Task<AnchorProfileBundle?> ReadAsync(
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

		return MapBundle(resultSets);
	}

	internal static AnchorProfileBundle MapBundle(IReadOnlyList<AnchorMemberProfileResultSet> resultSets)
	{
		if (resultSets.Count != 6)
		{
			throw new AnchorProfileContractException(
				"The profile procedure returned "
				+ resultSets.Count
				+ " result sets. Install scripts/sqlserver/Members/008_profile_supporting_reads.sql before using this API.");
		}

		var profiles = Map([resultSets[0]]);
		if (profiles is null
			|| !ColumnsMatch(resultSets[1], BranchValidColumns)
			|| !ColumnsMatch(resultSets[2], BranchAnchorColumns)
			|| !ColumnsMatch(resultSets[3], PlacementColumns)
			|| !ColumnsMatch(resultSets[4], FrontingColumns)
			|| !ColumnsMatch(resultSets[5], AvatarColumns))
		{
			throw new AnchorProfileContractException(
				"The profile procedure result sets do not match the installed contract. Install scripts/sqlserver/Members/008_profile_supporting_reads.sql before using this API.");
		}

		if (resultSets[1].Rows.Count != 1 || resultSets[1].Rows[0].Count != 1 || resultSets[1].Rows[0][0] is not bool branchValid)
		{
			throw new AnchorProfileContractException(
				"The profile procedure did not return BranchValid. Install scripts/sqlserver/Members/008_profile_supporting_reads.sql before using this API.");
		}

		var anchors = new List<AnchorBranchAnchor>(resultSets[2].Rows.Count);
		foreach (var row in resultSets[2].Rows)
		{
			if (row.Count != BranchAnchorColumns.Length
				|| row[1] is not string objectType
				|| row[2] is not Guid objectId
				|| !TryOptionalText(row[3], out var visibilityMode)
				|| !TryOptionalInt(row[4], out var visibilityTypeId)
				|| !TryOptionalText(row[5], out var permissionMode)
				|| !TryOptionalText(row[6], out var permissionCode))
			{
				throw new AnchorProfileContractException(
					"A branch anchor row could not be read.");
			}

			anchors.Add(new AnchorBranchAnchor(
				objectType,
				objectId,
				visibilityMode,
				visibilityTypeId,
				permissionMode,
				permissionCode));
		}

		var seenMembers = new HashSet<Guid>();
		var placements = new List<AnchorMemberPlacement>(resultSets[3].Rows.Count);
		foreach (var row in resultSets[3].Rows)
		{
			if (row.Count != PlacementColumns.Length
				|| row[0] is not Guid memberId
				|| !TryOptionalText(row[1], out var visibilityMode)
				|| !TryOptionalInt(row[2], out var visibilityTypeId)
				|| !TryOptionalText(row[3], out var permissionMode)
				|| !TryOptionalText(row[4], out var permissionCode)
				|| !seenMembers.Add(memberId))
			{
				throw new AnchorProfileContractException(
					"A placement row was missing, duplicated, or could not be read.");
			}

			placements.Add(new AnchorMemberPlacement(
				memberId,
				visibilityMode,
				visibilityTypeId,
				permissionMode,
				permissionCode));
		}

		seenMembers.Clear();
		var fronting = new List<AnchorMemberFronting>(resultSets[4].Rows.Count);
		foreach (var row in resultSets[4].Rows)
		{
			if (row.Count != FrontingColumns.Length
				|| row[0] is not Guid memberId
				|| !TryRequiredBool(row[1], out var isFronting)
				|| !seenMembers.Add(memberId))
			{
				throw new AnchorProfileContractException(
					"A fronting row was missing, duplicated, or could not be read.");
			}

			fronting.Add(new AnchorMemberFronting(memberId, isFronting));
		}

		seenMembers.Clear();
		var avatars = new List<AnchorMemberAvatar>(resultSets[5].Rows.Count);
		foreach (var row in resultSets[5].Rows)
		{
			if (row.Count != AvatarColumns.Length
				|| row[0] is not Guid memberId
				|| row[1] is not DateTime updatedAtUtc
				|| !seenMembers.Add(memberId))
			{
				throw new AnchorProfileContractException(
					"An avatar reference was missing, duplicated, or could not be read.");
			}

			avatars.Add(new AnchorMemberAvatar(memberId, updatedAtUtc));
		}

		return new AnchorProfileBundle(profiles, branchValid, anchors, placements, fronting, avatars);
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
			&& TryOptionalText(row[4], out var color)
			&& TryOptionalInt(row[5], out var visibilityTypeId)
			&& TryOptionalText(row[6], out var visibilityTypeCode)
			&& TryOptionalText(row[7], out var visibilityTypeName)
			&& TryRequiredBool(row[8], out var isFronting))
		{
			profile = new AnchorMemberProfile(
				memberId,
				displayName,
				pronouns,
				description,
				color,
				visibilityTypeId,
				visibilityTypeCode,
				visibilityTypeName,
				isFronting);
			return true;
		}

		profile = null!;
		return false;
	}

	private static bool TryOptionalInt(object value, out int? number)
	{
		if (value is DBNull)
		{
			number = null;
			return true;
		}

		if (value is int id)
		{
			number = id;
			return true;
		}

		number = null;
		return false;
	}

	private static bool TryRequiredBool(object value, out bool flag)
	{
		if (value is bool bit)
		{
			flag = bit;
			return true;
		}

		flag = false;
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
