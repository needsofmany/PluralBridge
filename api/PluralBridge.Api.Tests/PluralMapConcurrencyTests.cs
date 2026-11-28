// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using Microsoft.Data.SqlClient;
using PluralBridge.Api.Tests.Account;

namespace PluralBridge.Api.Tests;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class PluralMapConcurrencyTests
{
	private const int MaxAttempts = 5;
	private const int ProcedureResultSetCount = 4;
	private const string Example1Email = "example-1@pluralbridge.org";
	private const string Example11Email = "example-11@pluralbridge.org";

	private const string ProcedureSql = """
		EXEC dbo.pb_show_tree_step4_members_by_email @Email = @Email;
		""";

	private const string BracketedProcedureSql = """
		DECLARE @StartedAt datetime2(7) = SYSUTCDATETIME();
		EXEC dbo.pb_show_tree_step4_members_by_email @Email = @Email;
		DECLARE @EndedAt datetime2(7) = SYSUTCDATETIME();
		SELECT
			@StartedAt AS StartedAt,
			@EndedAt AS EndedAt,
			@@SPID AS SessionId;
		""";

	[Fact]
	public async Task ExampleAccounts_MatchIsolatedBaselines_WhenServerInvocationBracketsOverlap()
	{
		var connectionString = AccountTestDatabase.GetConnectionString();

		await using var example1Connection = new SqlConnection(connectionString);
		await using var example11Connection = new SqlConnection(connectionString);
		await example1Connection.OpenAsync();
		await example11Connection.OpenAsync();

		var example1SessionId = await ReadSessionIdAsync(example1Connection);
		var example11SessionId = await ReadSessionIdAsync(example11Connection);
		Assert.NotEqual(example1SessionId, example11SessionId);

		var example1Baseline = await ExecuteProcedureAsync(example1Connection, Example1Email);
		var example11Baseline = await ExecuteProcedureAsync(example11Connection, Example11Email);

		string? bracketReport = null;

		for (var attempt = 1; attempt <= MaxAttempts; attempt++)
		{
			var startGate = new AsyncStartGate(2);
			var example1Run = ExecuteBracketedProcedureAsync(
				example1Connection,
				Example1Email,
				example1SessionId,
				startGate);
			var example11Run = ExecuteBracketedProcedureAsync(
				example11Connection,
				Example11Email,
				example11SessionId,
				startGate);

			BracketedExecution example1Execution;
			BracketedExecution example11Execution;

			try
			{
				var executions = await Task.WhenAll(example1Run, example11Run);
				example1Execution = executions[0];
				example11Execution = executions[1];
			}
			catch (SqlException exception)
			{
				Assert.Fail(
					$"SQL error on attempt {attempt.ToString(CultureInfo.InvariantCulture)}: " +
					$"{exception.Number.ToString(CultureInfo.InvariantCulture)} {exception.Message}");
				return;
			}

			AssertSameProcedureResults(example1Baseline, example1Execution.ProcedureResults, "example-1", attempt);
			AssertSameProcedureResults(example11Baseline, example11Execution.ProcedureResults, "example-11", attempt);

			if (ServerBracketsOverlap(example1Execution.Bracket, example11Execution.Bracket))
			{
				return;
			}

			bracketReport = FormatBrackets(attempt, example1Execution.Bracket, example11Execution.Bracket);
		}

		Assert.Fail(
			"Inconclusive: the server-side invocation brackets did not overlap " +
			$"after {MaxAttempts.ToString(CultureInfo.InvariantCulture)} attempts. " +
			$"{bracketReport}");
	}

	private static async Task<int> ReadSessionIdAsync(SqlConnection connection)
	{
		await using var command = new SqlCommand("SELECT @@SPID;", connection);
		var value = await command.ExecuteScalarAsync();
		return Convert.ToInt32(value, CultureInfo.InvariantCulture);
	}

	private static async Task<IReadOnlyList<ProcedureResultSet>> ExecuteProcedureAsync(
		SqlConnection connection,
		string email)
	{
		await using var command = CreateProcedureCommand(connection, ProcedureSql, email);
		await using var reader = await command.ExecuteReaderAsync();
		var procedureResults = await ReadProcedureResultSetsAsync(reader);

		if (await reader.NextResultAsync())
		{
			Assert.Fail("The procedure returned more than four result sets.");
		}

		return procedureResults;
	}

	private static async Task<BracketedExecution> ExecuteBracketedProcedureAsync(
		SqlConnection connection,
		string email,
		int expectedSessionId,
		AsyncStartGate startGate)
	{
		await startGate.WaitAsync();

		await using var command = CreateProcedureCommand(connection, BracketedProcedureSql, email);
		await using var reader = await command.ExecuteReaderAsync();
		var procedureResults = await ReadProcedureResultSetsAsync(reader);

		if (!await reader.NextResultAsync())
		{
			Assert.Fail("The bracketed call did not return its timing result set.");
		}

		var bracket = await ReadInvocationBracketAsync(reader, expectedSessionId);

		if (await reader.NextResultAsync())
		{
			Assert.Fail("The bracketed call returned more than the timing result set after the procedure results.");
		}

		return new BracketedExecution(procedureResults, bracket);
	}

	private static SqlCommand CreateProcedureCommand(SqlConnection connection, string sql, string email)
	{
		var command = new SqlCommand(sql, connection)
		{
			CommandTimeout = 120
		};
		command.Parameters.Add("@Email", System.Data.SqlDbType.NVarChar, 320).Value = email;
		return command;
	}

	private static async Task<IReadOnlyList<ProcedureResultSet>> ReadProcedureResultSetsAsync(SqlDataReader reader)
	{
		var resultSets = new List<ProcedureResultSet>(ProcedureResultSetCount);

		for (var index = 0; index < ProcedureResultSetCount; index++)
		{
			if (index > 0 && !await reader.NextResultAsync())
			{
				Assert.Fail(
					$"Expected {ProcedureResultSetCount.ToString(CultureInfo.InvariantCulture)} procedure result sets " +
					$"and received {index.ToString(CultureInfo.InvariantCulture)}.");
			}

			resultSets.Add(await ReadResultSetAsync(reader));
		}

		return resultSets;
	}

	private static async Task<ProcedureResultSet> ReadResultSetAsync(SqlDataReader reader)
	{
		var columns = new ResultColumn[reader.FieldCount];

		for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
		{
			columns[ordinal] = new ResultColumn(reader.GetName(ordinal), reader.GetDataTypeName(ordinal));
		}

		var rows = new List<IReadOnlyList<object?>>();

		while (await reader.ReadAsync())
		{
			var cells = new object?[reader.FieldCount];

			for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
			{
				cells[ordinal] = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
			}

			rows.Add(cells);
		}

		return new ProcedureResultSet(columns, rows);
	}

	private static async Task<InvocationBracket> ReadInvocationBracketAsync(
		SqlDataReader reader,
		int expectedSessionId)
	{
		if (!await reader.ReadAsync())
		{
			Assert.Fail("The timing result set did not contain a row.");
		}

		var startedAt = reader.GetFieldValue<DateTime>(reader.GetOrdinal("StartedAt"));
		var endedAt = reader.GetFieldValue<DateTime>(reader.GetOrdinal("EndedAt"));
		var sessionId = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("SessionId")), CultureInfo.InvariantCulture);

		if (await reader.ReadAsync())
		{
			Assert.Fail("The timing result set contained more than one row.");
		}

		Assert.Equal(expectedSessionId, sessionId);
		Assert.True(startedAt <= endedAt, "The server-side invocation bracket ended before it started.");

		return new InvocationBracket(sessionId, startedAt, endedAt);
	}

	private static void AssertSameProcedureResults(
		IReadOnlyList<ProcedureResultSet> expected,
		IReadOnlyList<ProcedureResultSet> actual,
		string accountLabel,
		int attempt)
	{
		Assert.Equal(ProcedureResultSetCount, expected.Count);
		Assert.Equal(expected.Count, actual.Count);

		for (var setIndex = 0; setIndex < expected.Count; setIndex++)
		{
			var expectedSet = expected[setIndex];
			var actualSet = actual[setIndex];
			var setLabel = (setIndex + 1).ToString(CultureInfo.InvariantCulture);

			if (expectedSet.Columns.Count != actualSet.Columns.Count)
			{
				Assert.Fail(
					$"{accountLabel} attempt {attempt.ToString(CultureInfo.InvariantCulture)} result set {setLabel} " +
					$"has {actualSet.Columns.Count.ToString(CultureInfo.InvariantCulture)} columns " +
					$"instead of {expectedSet.Columns.Count.ToString(CultureInfo.InvariantCulture)}.");
			}

			for (var columnIndex = 0; columnIndex < expectedSet.Columns.Count; columnIndex++)
			{
				var expectedColumn = expectedSet.Columns[columnIndex];
				var actualColumn = actualSet.Columns[columnIndex];

				if (!string.Equals(expectedColumn.Name, actualColumn.Name, StringComparison.Ordinal)
					|| !string.Equals(expectedColumn.SqlTypeName, actualColumn.SqlTypeName, StringComparison.OrdinalIgnoreCase))
				{
					Assert.Fail(
						$"{accountLabel} attempt {attempt.ToString(CultureInfo.InvariantCulture)} result set {setLabel} " +
						$"column {(columnIndex + 1).ToString(CultureInfo.InvariantCulture)} was " +
						$"{actualColumn.Name} {actualColumn.SqlTypeName} instead of {expectedColumn.Name} {expectedColumn.SqlTypeName}.");
				}
			}

			if (expectedSet.Rows.Count != actualSet.Rows.Count)
			{
				Assert.Fail(
					$"{accountLabel} attempt {attempt.ToString(CultureInfo.InvariantCulture)} result set {setLabel} " +
					$"has {actualSet.Rows.Count.ToString(CultureInfo.InvariantCulture)} rows " +
					$"instead of {expectedSet.Rows.Count.ToString(CultureInfo.InvariantCulture)}.");
			}

			for (var rowIndex = 0; rowIndex < expectedSet.Rows.Count; rowIndex++)
			{
				var expectedRow = expectedSet.Rows[rowIndex];
				var actualRow = actualSet.Rows[rowIndex];

				for (var columnIndex = 0; columnIndex < expectedRow.Count; columnIndex++)
				{
					if (CellsEqual(expectedRow[columnIndex], actualRow[columnIndex]))
					{
						continue;
					}

					Assert.Fail(
						$"{accountLabel} attempt {attempt.ToString(CultureInfo.InvariantCulture)} result set {setLabel} " +
						$"row {(rowIndex + 1).ToString(CultureInfo.InvariantCulture)} column {expectedSet.Columns[columnIndex].Name} " +
						$"was '{FormatCell(actualRow[columnIndex])}' instead of '{FormatCell(expectedRow[columnIndex])}'.");
				}
			}
		}
	}

	private static bool CellsEqual(object? expected, object? actual)
	{
		if (expected is byte[] expectedBytes && actual is byte[] actualBytes)
		{
			return expectedBytes.AsSpan().SequenceEqual(actualBytes);
		}

		return Equals(expected, actual);
	}

	private static string FormatCell(object? value)
	{
		return value switch
		{
			null => "NULL",
			byte[] bytes => Convert.ToHexString(bytes),
			IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
			_ => value.ToString() ?? string.Empty
		};
	}

	private static bool ServerBracketsOverlap(InvocationBracket first, InvocationBracket second)
	{
		return first.SessionId != second.SessionId
			&& first.StartedAt < second.EndedAt
			&& second.StartedAt < first.EndedAt;
	}

	private static string FormatBrackets(int attempt, InvocationBracket example1, InvocationBracket example11)
	{
		return
			$"Attempt {attempt.ToString(CultureInfo.InvariantCulture)} server-side invocation brackets: " +
			$"example-1 session {example1.SessionId.ToString(CultureInfo.InvariantCulture)} " +
			$"[{example1.StartedAt.ToString("O", CultureInfo.InvariantCulture)}, {example1.EndedAt.ToString("O", CultureInfo.InvariantCulture)}], " +
			$"example-11 session {example11.SessionId.ToString(CultureInfo.InvariantCulture)} " +
			$"[{example11.StartedAt.ToString("O", CultureInfo.InvariantCulture)}, {example11.EndedAt.ToString("O", CultureInfo.InvariantCulture)}].";
	}

	private sealed class AsyncStartGate
	{
		private readonly int _participantCount;
		private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _arrived;

		public AsyncStartGate(int participantCount)
		{
			_participantCount = participantCount;
		}

		public async Task WaitAsync()
		{
			if (Interlocked.Increment(ref _arrived) == _participantCount)
			{
				_release.TrySetResult();
			}

			await _release.Task.WaitAsync(TimeSpan.FromSeconds(30));
		}
	}

	private sealed record ResultColumn(string Name, string SqlTypeName);

	private sealed record ProcedureResultSet(
		IReadOnlyList<ResultColumn> Columns,
		IReadOnlyList<IReadOnlyList<object?>> Rows);

	private sealed record InvocationBracket(int SessionId, DateTime StartedAt, DateTime EndedAt);

	private sealed record BracketedExecution(
		IReadOnlyList<ProcedureResultSet> ProcedureResults,
		InvocationBracket Bracket);
}
