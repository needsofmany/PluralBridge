// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// The profile procedure returned a result shape this API does not accept.
/// </summary>
internal sealed class AnchorProfileContractException : Exception
{
	/// <summary>
	/// Records the contract mismatch that the profile endpoint returns as an installation error.
	/// </summary>
	/// <param name="message">Caller-supplied explanation, including the script required when the shape is known.</param>
	internal AnchorProfileContractException(string message)
		: base(message)
	{
	}
}

/// <summary>
/// The six profile result sets after their columns have been checked. Duplicate member IDs are rejected inside the placement, fronting, and avatar sets. Profile member IDs are not checked for duplicates, and supporting IDs are not compared with the profile list.
/// </summary>
/// <param name="Profiles">Profile rows in procedure order. Repeated member IDs are preserved.</param>
/// <param name="BranchValid">The procedure's BranchValid bit. This reader does not revalidate the branch.</param>
/// <param name="BranchAnchors">Branch-anchor rows in the order the procedure returned them. This reader does not sort by Ordinal, validate the topology, or require the list to be empty when BranchValid is false.</param>
/// <param name="Placements">Placement rows. A repeated member ID in this set is rejected. An empty set does not remove fronting or avatars.</param>
/// <param name="Fronting">One member-level fronting bit per member returned by the fronting set.</param>
/// <param name="Avatars">Avatar timestamps only. Image bytes are not in this bundle.</param>
internal sealed record AnchorProfileBundle(
	IReadOnlyList<AnchorMemberProfile> Profiles,
	bool BranchValid,
	IReadOnlyList<AnchorBranchAnchor> BranchAnchors,
	IReadOnlyList<AnchorMemberPlacement> Placements,
	IReadOnlyList<AnchorMemberFronting> Fronting,
	IReadOnlyList<AnchorMemberAvatar> Avatars);

/// <summary>
/// Stored visibility and permission settings for one member on the selected anchor. Null modes are left null for the caller to resolve.
/// </summary>
internal sealed record AnchorMemberPlacement(
	Guid MemberId,
	string? VisibilityMode,
	int? VisibilityTypeId,
	string? PermissionMode,
	string? PermissionCode);

/// <summary>
/// Whether the member has an open live fronting interval. This is independent of the profile row's fronting column.
/// </summary>
internal sealed record AnchorMemberFronting(Guid MemberId, bool IsFronting);

/// <summary>
/// The member's avatar timestamp. The image GET uses this value as the cache version and reads the bytes separately.
/// </summary>
internal sealed record AnchorMemberAvatar(Guid MemberId, DateTime UpdatedAtUtc);

/// <summary>
/// One anchor on the selected branch, with the visibility and permission settings used for display inheritance.
/// </summary>
internal sealed record AnchorBranchAnchor(
	string ObjectType,
	Guid ObjectId,
	string? VisibilityMode,
	int? VisibilityTypeId,
	string? PermissionMode,
	string? PermissionCode);

/// <summary>
/// One member profile row from the first result set. Nullable text and visibility fields stay null when the database value is null.
/// </summary>
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

/// <summary>
/// One raw procedure result set: column names and SQL type names, plus the unread cell values.
/// </summary>
internal sealed class AnchorMemberProfileResultSet
{
	public required IReadOnlyList<AnchorMemberProfileColumn> Columns { get; init; }

	public required IReadOnlyList<IReadOnlyList<object>> Rows { get; init; }
}

/// <summary>
/// A result-set column identified by name and SQL type. Matching is ordinal and case-sensitive for the name.
/// </summary>
internal readonly record struct AnchorMemberProfileColumn(string Name, string SqlTypeName);

/// <summary>
/// Reads and checks the anchor member profile procedure.
/// </summary>
/// <remarks>
/// The procedure returns one set when @BranchJson is omitted. Map accepts only that nine-column set.
/// The API always passes @BranchJson, so MapBundle requires all six sets and does not treat a shorter result as success.
/// </remarks>
internal static class AnchorMemberProfileReader
{
	/// <summary>
	/// Calls the profile procedure and requires the six-set contract.
	/// </summary>
	/// <param name="connection">Open connection for the signed-in account's database.</param>
	/// <param name="email">Account email. The procedure resolves the account from this value.</param>
	/// <param name="objectType">system, group, or place.</param>
	/// <param name="anchorName">Anchor display name.</param>
	/// <param name="branch">Selected occurrence chain. Null is sent as an empty branch.</param>
	/// <param name="cancellationToken">Cancels the procedure or the result-set read.</param>
	/// <param name="capture">The workspace-load capture. Null records nothing and does not time the reader.</param>
	/// <returns>The six-set bundle.</returns>
	/// <exception cref="AnchorProfileContractException">The reader did not produce the six-set bundle.</exception>
	internal static async Task<AnchorProfileBundle> ExecuteAsync(
		SqlConnection connection,
		string email,
		string objectType,
		string anchorName,
		string? branch,
		CancellationToken cancellationToken,
		AnchorProfileReadCapture? capture = null)
	{
		var branchJson = AnchorMemberProfileSave.BranchJson(branch ?? "");
		capture?.Bind(email, objectType, anchorName, branchJson);
		await using var command = new SqlCommand(
			"dbo.pb_get_anchor_member_profiles_by_email",
			connection)
		{
			CommandType = CommandType.StoredProcedure
		};
		command.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = email;
		command.Parameters.Add("@AnchorObjectType", SqlDbType.NVarChar, 64).Value = objectType;
		command.Parameters.Add("@AnchorName", SqlDbType.NVarChar, 510).Value = anchorName;
		command.Parameters.Add("@BranchJson", SqlDbType.NVarChar, -1).Value = branchJson;

		var total = capture == null ? null : Stopwatch.StartNew();
		var execute = capture == null ? null : Stopwatch.StartNew();
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (execute != null && capture != null)
		{
			execute.Stop();
			capture.ExecuteReaderAwaitMs = execute.ElapsedMilliseconds;
		}

		var bundle = await ReadAsync(reader, cancellationToken, capture)
			?? throw new AnchorProfileContractException(
				"The profile procedure did not return the six-result-set contract. Install scripts/sqlserver/Members/008_profile_supporting_reads.sql before using this API.");
		if (total != null && capture != null)
		{
			total.Stop();
			capture.TotalReaderMs = total.ElapsedMilliseconds;
		}

		return bundle;
	}

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

	/// <summary>
	/// Copies every result set from the open reader and maps the six-set contract.
	/// </summary>
	/// <param name="reader">Reader positioned at the first profile result set. It is advanced through every following set.</param>
	/// <param name="cancellationToken">Cancels reading a row or moving to the next result set.</param>
	/// <param name="capture">The workspace-load capture. Null does not time the drain or the mapping.</param>
	/// <returns>The mapped bundle when the task completes. The current contract does not return null from MapBundle.</returns>
	/// <exception cref="AnchorProfileContractException">The sets are not the six-set contract, or a row cannot be read.</exception>
	internal static async Task<AnchorProfileBundle?> ReadAsync(
		SqlDataReader reader,
		CancellationToken cancellationToken,
		AnchorProfileReadCapture? capture = null)
	{
		var resultSets = new List<AnchorMemberProfileResultSet>();
		var index = 0;
		while (true)
		{
			var drain = capture == null ? null : Stopwatch.StartNew();
			var columns = new AnchorMemberProfileColumn[reader.FieldCount];
			for (var column = 0; column < reader.FieldCount; column++)
			{
				columns[column] = new AnchorMemberProfileColumn(
					reader.GetName(column),
					reader.GetDataTypeName(column));
			}

			var rows = new List<IReadOnlyList<object>>();
			while (await reader.ReadAsync(cancellationToken))
			{
				var values = new object[reader.FieldCount];
				reader.GetValues(values);
				rows.Add(values);
			}

			var more = await reader.NextResultAsync(cancellationToken);
			if (drain != null)
			{
				drain.Stop();
				capture?.AddDrain(index, rows.Count, drain.ElapsedMilliseconds);
			}

			resultSets.Add(new AnchorMemberProfileResultSet
			{
				Columns = columns,
				Rows = rows
			});
			index++;
			if (!more)
			{
				break;
			}
		}

		return MapBundle(resultSets, capture);
	}

	/// <summary>
	/// Maps the six profile result sets without merging a missing supporting row into another set.
	/// </summary>
	/// <param name="resultSets">Profiles, BranchValid, branch anchors, placements, member-level fronting, then avatar timestamps, in that order.</param>
	/// <param name="capture">The workspace-load capture. Null does not time mapping.</param>
	/// <returns>The checked bundle. An empty placement, fronting, or avatar set is a valid empty list.</returns>
	/// <remarks>
	/// The three supporting sets are independent: an empty placement set does not drop fronting or avatars.
	/// Duplicate member IDs are rejected within placements, within fronting, and within avatars. Map does not reject duplicate profile member IDs, and supporting IDs are not cross-checked against the profile list.
	/// BranchValid and anchor row order come from the procedure. This method preserves that anchor order. It does not validate topology, enforce Ordinal order, or require an empty anchor list when BranchValid is false.
	/// UpdatedAtUtc stays a DateTime so the caller formats the URL version.
	/// </remarks>
	/// <exception cref="AnchorProfileContractException">The set count, columns, BranchValid cell, or a member row does not match the installed procedure.</exception>
	internal static AnchorProfileBundle MapBundle(
		IReadOnlyList<AnchorMemberProfileResultSet> resultSets,
		AnchorProfileReadCapture? capture = null)
	{
		if (resultSets.Count != 6)
		{
			throw new AnchorProfileContractException(
				"The profile procedure returned "
				+ resultSets.Count
				+ " result sets. Install scripts/sqlserver/Members/008_profile_supporting_reads.sql before using this API.");
		}

		T Time<T>(int index, Func<T> action)
		{
			if (capture == null)
			{
				return action();
			}

			var watch = Stopwatch.StartNew();
			try
			{
				return action();
			}
			finally
			{
				watch.Stop();
				capture.AddMap(index, watch.ElapsedMilliseconds);
			}
		}

		// Profile rows use the strict nine-column map. A mismatch fails the whole bundle.
		var profiles = Time(0, () => Map([resultSets[0]]));
		var branchColumns = Time(1, () => ColumnsMatch(resultSets[1], BranchValidColumns));
		var anchorColumns = Time(2, () => ColumnsMatch(resultSets[2], BranchAnchorColumns));
		var placementColumns = Time(3, () => ColumnsMatch(resultSets[3], PlacementColumns));
		var frontingColumns = Time(4, () => ColumnsMatch(resultSets[4], FrontingColumns));
		var avatarColumns = Time(5, () => ColumnsMatch(resultSets[5], AvatarColumns));
		if (profiles is null
			|| !branchColumns
			|| !anchorColumns
			|| !placementColumns
			|| !frontingColumns
			|| !avatarColumns)
		{
			throw new AnchorProfileContractException(
				"The profile procedure result sets do not match the installed contract. Install scripts/sqlserver/Members/008_profile_supporting_reads.sql before using this API.");
		}

		var branchValid = Time(1, () =>
		{
			if (resultSets[1].Rows.Count != 1 || resultSets[1].Rows[0].Count != 1 || resultSets[1].Rows[0][0] is not bool valid)
			{
				throw new AnchorProfileContractException(
					"The profile procedure did not return BranchValid. Install scripts/sqlserver/Members/008_profile_supporting_reads.sql before using this API.");
			}

			return valid;
		});

		var anchors = Time(2, () =>
		{
		var mapped = new List<AnchorBranchAnchor>(resultSets[2].Rows.Count);
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

			mapped.Add(new AnchorBranchAnchor(
				objectType,
				objectId,
				visibilityMode,
				visibilityTypeId,
				permissionMode,
				permissionCode));
		}

		return mapped;
		});

		// Each supporting set keeps its own member identity. A duplicate in one set does not select a row.
		var seenMembers = new HashSet<Guid>();
		var placements = Time(3, () =>
		{
		var mapped = new List<AnchorMemberPlacement>(resultSets[3].Rows.Count);
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

			mapped.Add(new AnchorMemberPlacement(
				memberId,
				visibilityMode,
				visibilityTypeId,
				permissionMode,
				permissionCode));
		}

		return mapped;
		});

		seenMembers.Clear();
		var fronting = Time(4, () =>
		{
		var mapped = new List<AnchorMemberFronting>(resultSets[4].Rows.Count);
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

			mapped.Add(new AnchorMemberFronting(memberId, isFronting));
		}

		return mapped;
		});

		seenMembers.Clear();
		var avatars = Time(5, () =>
		{
		var mapped = new List<AnchorMemberAvatar>(resultSets[5].Rows.Count);
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

			mapped.Add(new AnchorMemberAvatar(memberId, updatedAtUtc));
		}

		return mapped;
		});

		return new AnchorProfileBundle(profiles, branchValid, anchors, placements, fronting, avatars);
	}

	/// <summary>
	/// Maps the single nine-column profile set used when the branch parameter is absent from the procedure.
	/// </summary>
	/// <param name="resultSets">Exactly one set whose columns match the profile contract, in order.</param>
	/// <returns>The profiles in row order, including repeated member IDs. Null when the set count, columns, or any row does not match. Null is a contract failure, not an empty anchor.</returns>
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

	/// <summary>
	/// Compares column names and SQL types by ordinal.
	/// </summary>
	/// <param name="resultSet">The set returned by the procedure.</param>
	/// <param name="expected">The contract columns. Names are case-sensitive. SQL type names are not.</param>
	/// <returns>True when both lists have the same length and each position matches.</returns>
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

	/// <summary>
	/// Reads one nine-column profile row. A null fronting bit fails the row.
	/// </summary>
	/// <param name="row">Cells in profile column order. DBNull is accepted for the nullable text and visibility columns.</param>
	/// <param name="profile">The profile when the method returns true. Unset when it returns false.</param>
	/// <returns>True when the member id, display name, and fronting bit are present and the optional cells are null or the expected type.</returns>
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

	/// <summary>
	/// Accepts a SQL int or database null.
	/// </summary>
	/// <param name="value">The cell. DBNull becomes a null number. Any other non-int fails.</param>
	/// <param name="number">The integer, or null when <paramref name="value"/> is DBNull. Unset on failure.</param>
	/// <returns>True for DBNull or an int.</returns>
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

	/// <summary>
	/// Accepts only a SQL bit. Database null is a contract failure.
	/// </summary>
	/// <param name="value">The cell. Only a bool is accepted.</param>
	/// <param name="flag">The bit when the method returns true. False when it returns false.</param>
	/// <returns>True when <paramref name="value"/> is a bool.</returns>
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

	/// <summary>
	/// Accepts a SQL string or database null.
	/// </summary>
	/// <param name="value">The cell. DBNull becomes a null string. Any other non-string fails.</param>
	/// <param name="text">The string, or null when <paramref name="value"/> is DBNull. Unset on failure.</param>
	/// <returns>True for DBNull or a string.</returns>
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
