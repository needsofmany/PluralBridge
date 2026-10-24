// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the /api/me endpoint for the Chapter 2 access context and authorization-gated proof data.
/// This controller verifies that the API can reach the validated PluralBridge cloud proof database
/// and returns the current access context, resolved system, authorization-gated proof data, and table counts.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class MeController(
	IConfiguration configuration,
	ILogger<MeController> logger) : ControllerBase
{
	/// <summary>
	/// Provides the /api/me endpoint for the Chapter 2 access context and authorization-gated proof data.
	/// The response is intentionally read-only and includes the proof system plus validated table counts
	/// so the browser can confirm that it is receiving real database-backed data.
	/// </summary>
	/// <returns>
	/// HTTP 200 with proof metadata, the selected proof system, write capability set to false,
	/// and counts for the Phase 2B source and 1-6 slice tables.
	/// </returns>
	[HttpGet]
	public async Task<IActionResult> Get()
	{
		var requestTrace = RequestTraceContext.Create(
			HttpContext.TraceIdentifier,
			HttpContext.Request.Headers.TryGetValue(Globals.correlationID, out var correlationId)
				? correlationId.ToString()
				: null);

		try
		{
			// get info to connect to the database
			var connectionString = configuration.GetConnectionString(Globals.connectionString);
			if (string.IsNullOrWhiteSpace(connectionString))
			{
				requestTrace.LogStage(
					logger,
					nameof(LogStageParts.error_path),
					nameof(LogStageParts.reached));

				return Problem(
					title: Globals.missingConnectionString,
					detail: Globals.missingConnStringDetail,
					statusCode: StatusCodes.Status500InternalServerError);
			}

			// get connection to the database
			await using SqlConnection connection = new(connectionString);
			await connection.OpenAsync();
			var databaseName = connection.Database;

			// should contain the account and it's available System memberships
			var accessContext = await AccessContextHelper.ResolveCurrentAccessAsync(
				connection,
				User,
				requestTrace,
				logger);

			if (accessContext is null)
			{
				requestTrace.LogStage(
					logger,
					nameof(LogStageParts.error_path),
					nameof(LogStageParts.reached));

				return Problem(
					title: Globals.currContextNotFound,
					detail: Globals.currConfiguredAccount,
					statusCode: StatusCodes.Status500InternalServerError);
			}

			// retrieve everything we need to know for this System
			var currentAccount = accessContext.CurrentAccount;
			var membershipAccess = accessContext.MembershipAccess;
			var currentSystem = accessContext.CurrentSystem;

			var dataAccessStopwatch = Stopwatch.StartNew();

			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.data_access),
				nameof(LogStageParts.started));

			Dictionary<string, long> counts;
			ProofSystem? proofSystem;

			try
			{
				counts = await ReadCountsAsync(
					connection,
					membershipAccess);
				proofSystem = await ReadProofSystemAsync(connection);

				dataAccessStopwatch.Stop();

				requestTrace.LogStage(
					logger,
					nameof(LogStageParts.data_access),
					nameof(LogStageParts.completed),
					dataAccessStopwatch.Elapsed);
			}
			catch
			{
				dataAccessStopwatch.Stop();

				requestTrace.LogStage(
					logger,
					nameof(LogStageParts.data_access),
					nameof(LogStageParts.failed),
					dataAccessStopwatch.Elapsed);

				requestTrace.LogStage(
					logger,
					nameof(LogStageParts.error_path),
					nameof(LogStageParts.reached));

				throw;
			}

			return Ok(new
			{
				api = Globals.apiName,
				phase = Globals.projectPhase,
				mode = Globals.roProof,
				database = databaseName,
				canWrite = false,
				currentAccount,
				membershipAccess,
				currentSystem,
				proofSystem,
				counts
			});
		}
		catch
		{
			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.error_path),
				nameof(LogStageParts.reached));

			return Problem(
				title: Globals.requestFailed,
				detail: Globals.currConfiguredAccount,
				statusCode: StatusCodes.Status500InternalServerError);
		}
	}

	/// <summary>
	/// Reads count totals from the source and 1-6 proof tables in the validated database.
	/// These totals are used as a compact integrity check for the Phase 2B browser proof.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <returns>A dictionary keyed by API-facing count names.</returns>
	private static async Task<Dictionary<string, long>> ReadCountsAsync(
		SqlConnection connection,
		IReadOnlyList<AccessContextHelper.SystemMembership> memberships)
	{
		var systemIds = memberships
			.Select(membership => membership.SystemId)
			.Distinct()
			.ToArray();

		Dictionary<string, long> counts = new()
		{
			[nameof(CountKeys.sourceSystems)] = 0,
			[nameof(CountKeys.importBatches)] = 0,
			[nameof(CountKeys.systems)] = 0,
			[nameof(CountKeys.members)] = 0,
			[nameof(CountKeys.privacyBuckets)] = 0,
			[nameof(CountKeys.customFields)] = 0,
			[nameof(CountKeys.frontHistory)] = 0,
			[nameof(CountKeys.sourceRecords)] = 0,
			[nameof(CountKeys.sourceIdMappings)] = 0
		};

		if (systemIds.Length == 0)
		{
			return counts;
		}

		var idListBuilder = new StringBuilder();

		for (var index = 0; index < systemIds.Length; index++)
		{
			if (index > 0)
			{
				idListBuilder.Append(", ");
			}

			idListBuilder.Append("@SystemId").Append(index);
		}

		var systemIdInClause = idListBuilder.ToString();
		var sourceIdMapFilter = $"TRY_CONVERT(uniqueidentifier, sim.PluralBridgeId) IN ({systemIdInClause})";
		var sourceBatchFilter = $"SELECT DISTINCT sim.ImportBatchId FROM dbo.pb_source_id_map AS sim WHERE {sourceIdMapFilter}";

		async Task<long> ReadCountAsync(string sql)
		{
			await using var command = new SqlCommand(sql, connection);

			for (var index = 0; index < systemIds.Length; index++)
			{
				command.Parameters.AddWithValue($"@SystemId{index}", systemIds[index]);
			}

			var value = await command.ExecuteScalarAsync();

			return value switch
			{
				long asLong => asLong,
				int asInt => asInt,
				decimal asDecimal => (long)asDecimal,
				_ => 0
			};
		}

		counts[nameof(CountKeys.systems)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_systems WHERE SystemId IN ({systemIdInClause});");
		counts[nameof(CountKeys.members)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_members WHERE SystemId IN ({systemIdInClause});");
		counts[nameof(CountKeys.privacyBuckets)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_privacy_buckets WHERE SystemId IN ({systemIdInClause});");
		counts[nameof(CountKeys.customFields)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_custom_fields WHERE SystemId IN ({systemIdInClause});");
		counts[nameof(CountKeys.frontHistory)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_front_history WHERE SystemId IN ({systemIdInClause});");
		counts[nameof(CountKeys.sourceIdMappings)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_source_id_map AS sim WHERE {sourceIdMapFilter};");
		counts[nameof(CountKeys.importBatches)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM ({sourceBatchFilter}) AS batches;");
		counts[nameof(CountKeys.sourceRecords)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_source_records AS sr WHERE sr.ImportBatchId IN ({sourceBatchFilter});");
		counts[nameof(CountKeys.sourceSystems)] =
			await ReadCountAsync(
				$"""
				SELECT CAST(COUNT_BIG(*) AS bigint)
				FROM dbo.pb_source_systems AS ss
				WHERE ss.SourceSystemCode IN
				(
				    SELECT DISTINCT sim.SourceSystemCode
				    FROM dbo.pb_source_id_map AS sim
				    WHERE {sourceIdMapFilter}
				      AND sim.SourceSystemCode IS NOT NULL
				);
				""");

		return counts;
	}

	/// <summary>
	/// Reads the first proof system from the validated database.
	/// Phase 2B uses this fixed proof context until Phase 3 replaces it with login/session mapping.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <returns>The selected proof system, or null when the proof database has no system row.</returns>
	private static async Task<ProofSystem?> ReadProofSystemAsync(SqlConnection connection)
	{
		const string sql = """
		                   SELECT TOP (1)
		                       SystemId,
		                       SystemName
		                   FROM dbo.pb_systems
		                   ORDER BY CreatedAtUtc, SystemId;
		                   """;

		await using SqlCommand command = new(sql, connection);
		await using var reader = await command.ExecuteReaderAsync();

		if (!await reader.ReadAsync())
		{
			return null;
		}

		return new ProofSystem(
			reader.GetGuid(0),
			reader.IsDBNull(1) ? null : reader.GetString(1));
	}

	/// <summary>
	/// Current System name and id token
	/// </summary>
	/// <param name="SystemId"></param>
	/// <param name="SystemName"></param>
	private sealed record ProofSystem(
		Guid SystemId, 
		string? SystemName);
}
