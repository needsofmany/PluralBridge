// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

internal sealed record PluralMapResponse(
	IReadOnlyList<PluralMapContextRow> Context,
	IReadOnlyList<PluralMapNodeCountRow> GraphNodeCounts,
	IReadOnlyList<PluralMapEdgeRow> GraphEdges,
	IReadOnlyList<PluralMapPreviewRow> HierarchyPreview);

internal sealed record PluralMapContextRow(
	string Section,
	string Email,
	Guid AccountId);

internal sealed record PluralMapNodeCountRow(
	string Section,
	string ObjectType,
	int NodeCount);

internal sealed record PluralMapEdgeRow(
	string Section,
	string ParentObjectType,
	Guid ParentObjectId,
	string ChildObjectType,
	Guid ChildObjectId,
	int EdgeRank,
	string EdgeSource);

internal sealed record PluralMapPreviewRow(
	string Section,
	string TreeLine,
	string PathKey,
	string? NodeLabel,
	string? ObjectType,
	Guid? ObjectId,
	string? ParentPathKey,
	bool Header);

internal sealed class PluralMapRawResultSet
{
	public required IReadOnlyList<PluralMapRawColumn> Columns { get; init; }

	public required IReadOnlyList<IReadOnlyList<object>> Rows { get; init; }
}

internal readonly record struct PluralMapRawColumn(string Name, string SqlTypeName);

internal static class PluralMapReader
{
	private const int ResultSetCount = 4;

	private static readonly PluralMapRawColumn[] ContextColumns =
	[
		new("Section", "nvarchar"),
		new("Email", "nvarchar"),
		new("AccountId", "uniqueidentifier")
	];

	private static readonly PluralMapRawColumn[] NodeCountColumns =
	[
		new("Section", "nvarchar"),
		new("ObjectType", "nvarchar"),
		new("NodeCount", "int")
	];

	private static readonly PluralMapRawColumn[] EdgeColumns =
	[
		new("Section", "nvarchar"),
		new("ParentObjectType", "nvarchar"),
		new("ParentObjectId", "uniqueidentifier"),
		new("ChildObjectType", "nvarchar"),
		new("ChildObjectId", "uniqueidentifier"),
		new("EdgeRank", "int"),
		new("EdgeSource", "nvarchar")
	];

	private static readonly PluralMapRawColumn[] PreviewColumns =
	[
		new("Section", "nvarchar"),
		new("TreeLine", "nvarchar"),
		new("PathKey", "nvarchar")
	];

	internal static async Task<IReadOnlyList<PluralMapRawResultSet>> ReadAllAsync(
		SqlDataReader reader,
		CancellationToken cancellationToken)
	{
		var resultSets = new List<PluralMapRawResultSet>();

		do
		{
			var columns = new PluralMapRawColumn[reader.FieldCount];
			for (var index = 0; index < reader.FieldCount; index++)
			{
				columns[index] = new PluralMapRawColumn(
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

			resultSets.Add(new PluralMapRawResultSet
			{
				Columns = columns,
				Rows = rows
			});
		}
		while (await reader.NextResultAsync(cancellationToken));

		return resultSets;
	}

	internal static PluralMapResponse? Map(
		IReadOnlyList<PluralMapRawResultSet> resultSets,
		Guid authenticatedAccountId)
	{
		if (resultSets.Count != ResultSetCount
			|| !ColumnsMatch(resultSets[0], ContextColumns)
			|| !ColumnsMatch(resultSets[1], NodeCountColumns)
			|| !ColumnsMatch(resultSets[2], EdgeColumns)
			|| !ColumnsMatch(resultSets[3], PreviewColumns)
			|| resultSets[0].Rows.Count != 1)
		{
			return null;
		}

		if (!TryContextRow(resultSets[0].Rows[0], out var context)
			|| context.AccountId != authenticatedAccountId)
		{
			return null;
		}

		var nodeCounts = new List<PluralMapNodeCountRow>(resultSets[1].Rows.Count);
		foreach (var row in resultSets[1].Rows)
		{
			if (!TryNodeCountRow(row, out var nodeCount))
			{
				return null;
			}

			nodeCounts.Add(nodeCount);
		}

		var edges = new List<PluralMapEdgeRow>(resultSets[2].Rows.Count);
		foreach (var row in resultSets[2].Rows)
		{
			if (!TryEdgeRow(row, out var edge))
			{
				return null;
			}

			edges.Add(edge);
		}

		var preview = new List<PluralMapPreviewRow>(resultSets[3].Rows.Count);
		foreach (var row in resultSets[3].Rows)
		{
			if (!TryPreviewRow(row, out var previewRow))
			{
				return null;
			}

			preview.Add(previewRow);
		}

		return new PluralMapResponse([context], nodeCounts, edges, DerivePreview(preview));
	}

	internal static IReadOnlyList<PluralMapPreviewRow> DerivePreview(
		IReadOnlyList<PluralMapPreviewRow> rows)
	{
		var derived = new List<PluralMapPreviewRow>(rows.Count);
		foreach (var row in rows)
		{
			derived.Add(DerivePreviewRow(row));
		}

		var pathKeys = new HashSet<string>(derived.Select(row => row.PathKey), StringComparer.Ordinal);
		for (var index = 0; index < derived.Count; index++)
		{
			var row = derived[index];
			if (row.Header
				|| row.ParentPathKey is null
				|| !string.Equals(row.ObjectType, "group", StringComparison.Ordinal)
				|| !TryParsePath(row.PathKey, out var segments, out _)
				|| segments.Length < 2
				|| !string.Equals(segments[^2].ObjectType, "system", StringComparison.Ordinal))
			{
				continue;
			}

			var headerPathKey = row.ParentPathKey + "." + GroupsHeaderSegment;
			if (pathKeys.Contains(headerPathKey))
			{
				derived[index] = row with { ParentPathKey = headerPathKey };
			}
		}

		return derived;
	}

	internal static PluralMapPreviewRow? SelectFirstAnchor(
		IReadOnlyList<PluralMapPreviewRow> rows)
	{
		foreach (var row in rows)
		{
			if (!row.Header
				&& row.ParentPathKey is null
				&& IsAnchorType(row.ObjectType))
			{
				return row;
			}
		}

		return null;
	}

	internal static IReadOnlyList<PluralMapPreviewRow> ImmediateChildren(
		IReadOnlyList<PluralMapPreviewRow> rows,
		string pathKey)
	{
		var children = new List<PluralMapPreviewRow>();
		foreach (var row in rows)
		{
			if (string.Equals(row.ParentPathKey, pathKey, StringComparison.Ordinal))
			{
				children.Add(row);
			}
		}

		return children;
	}

	private static bool ColumnsMatch(
		PluralMapRawResultSet resultSet,
		IReadOnlyList<PluralMapRawColumn> expected)
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

	private static bool TryContextRow(
		IReadOnlyList<object> row,
		out PluralMapContextRow context)
	{
		if (TryText(row, 0, out var section)
			&& TryText(row, 1, out var email)
			&& TryGuid(row, 2, out var accountId))
		{
			context = new PluralMapContextRow(section, email, accountId);
			return true;
		}

		context = null!;
		return false;
	}

	private static bool TryNodeCountRow(
		IReadOnlyList<object> row,
		out PluralMapNodeCountRow nodeCount)
	{
		if (TryText(row, 0, out var section)
			&& TryText(row, 1, out var objectType)
			&& TryInt(row, 2, out var count))
		{
			nodeCount = new PluralMapNodeCountRow(section, objectType, count);
			return true;
		}

		nodeCount = null!;
		return false;
	}

	private static bool TryEdgeRow(
		IReadOnlyList<object> row,
		out PluralMapEdgeRow edge)
	{
		if (TryText(row, 0, out var section)
			&& TryText(row, 1, out var parentObjectType)
			&& TryGuid(row, 2, out var parentObjectId)
			&& TryText(row, 3, out var childObjectType)
			&& TryGuid(row, 4, out var childObjectId)
			&& TryInt(row, 5, out var edgeRank)
			&& TryText(row, 6, out var edgeSource))
		{
			edge = new PluralMapEdgeRow(
				section,
				parentObjectType,
				parentObjectId,
				childObjectType,
				childObjectId,
				edgeRank,
				edgeSource);
			return true;
		}

		edge = null!;
		return false;
	}

	private static bool TryPreviewRow(
		IReadOnlyList<object> row,
		out PluralMapPreviewRow preview)
	{
		if (TryText(row, 0, out var section)
			&& TryText(row, 1, out var treeLine)
			&& TryText(row, 2, out var pathKey))
		{
			preview = new PluralMapPreviewRow(
				section,
				treeLine,
				pathKey,
				null,
				null,
				null,
				null,
				false);
			return true;
		}

		preview = null!;
		return false;
	}

	private static bool TryText(
		IReadOnlyList<object> row,
		int index,
		out string text)
	{
		if (index < row.Count && row[index] is string value)
		{
			text = value;
			return true;
		}

		text = "";
		return false;
	}

	private static bool TryGuid(
		IReadOnlyList<object> row,
		int index,
		out Guid value)
	{
		if (index < row.Count && row[index] is Guid guid)
		{
			value = guid;
			return true;
		}

		value = Guid.Empty;
		return false;
	}

	private static bool TryInt(
		IReadOnlyList<object> row,
		int index,
		out int value)
	{
		if (index < row.Count && row[index] is int number)
		{
			value = number;
			return true;
		}

		value = 0;
		return false;
	}

	private const string GroupsHeaderSegment =
		"15|000000|groups|00000000-0000-0000-0000-000000000000";

	private readonly record struct PathSegment(
		string TypeCode,
		string Rank,
		string ObjectType,
		Guid ObjectId);

	private static PluralMapPreviewRow DerivePreviewRow(PluralMapPreviewRow row)
	{
		var nodeLabel = ReadNodeLabel(row.TreeLine);
		if (!TryParsePath(row.PathKey, out var segments, out var parentPathKey))
		{
			return row with
			{
				NodeLabel = nodeLabel,
				ObjectType = null,
				ObjectId = null,
				ParentPathKey = null,
				Header = false
			};
		}

		var identity = segments[^1];
		var header = identity.TypeCode == "15"
			&& identity.Rank == "000000"
			&& string.Equals(identity.ObjectType, "groups", StringComparison.Ordinal)
			&& identity.ObjectId == Guid.Empty;

		return row with
		{
			NodeLabel = nodeLabel,
			ObjectType = identity.ObjectType,
			ObjectId = identity.ObjectId,
			ParentPathKey = parentPathKey,
			Header = header
		};
	}

	private static bool TryParsePath(
		string pathKey,
		out PathSegment[] segments,
		out string? parentPathKey)
	{
		var parts = pathKey.Split('.');
		segments = new PathSegment[parts.Length];
		for (var index = 0; index < parts.Length; index++)
		{
			if (!TryParseSegment(parts[index], out segments[index]))
			{
				parentPathKey = null;
				return false;
			}
		}

		parentPathKey = parts.Length == 1
			? null
			: string.Join('.', parts[..^1]);
		return parts.Length > 0;
	}

	private static bool TryParseSegment(string segment, out PathSegment parsed)
	{
		var parts = segment.Split('|');
		if (parts.Length == 4
			&& parts[0].Length == 2
			&& IsDigits(parts[0])
			&& parts[1].Length == 6
			&& IsDigits(parts[1])
			&& parts[2].Length > 0
			&& Guid.TryParseExact(parts[3], "D", out var objectId))
		{
			parsed = new PathSegment(parts[0], parts[1], parts[2], objectId);
			return true;
		}

		parsed = default;
		return false;
	}

	private static bool IsDigits(string value)
	{
		foreach (var character in value)
		{
			if (character < '0' || character > '9')
			{
				return false;
			}
		}

		return value.Length > 0;
	}

	private static string? ReadNodeLabel(string treeLine)
	{
		const string branch = "\u251c\u2500\u2500 ";
		const string last = "\u2514\u2500\u2500 ";
		var index = 0;
		while (index + 4 <= treeLine.Length
			&& treeLine.AsSpan(index, 4).SequenceEqual("    "))
		{
			index += 4;
		}

		if (treeLine.AsSpan(index).StartsWith(branch))
		{
			return treeLine[(index + branch.Length)..];
		}

		if (treeLine.AsSpan(index).StartsWith(last))
		{
			return treeLine[(index + last.Length)..];
		}

		return null;
	}

	private static bool IsAnchorType(string? objectType)
	{
		return objectType is "system" or "group" or "place";
	}
}
