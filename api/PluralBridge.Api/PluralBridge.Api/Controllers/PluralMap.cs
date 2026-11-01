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
	string PathKey);

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

		return new PluralMapResponse([context], nodeCounts, edges, preview);
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
			preview = new PluralMapPreviewRow(section, treeLine, pathKey);
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
}
