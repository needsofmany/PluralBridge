using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the /api/me endpoint for the Chapter 2 access context and authorization-gated proof data.
/// This controller verifies that the API can reach the validated PluralBridge cloud proof database
/// and returns the current access context, resolved system, authorization-gated proof data, and table counts.
/// </summary>
[ApiController]
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

			// Check whether the current account has an active membership for the current system.
			var isAuthorizedForCurrentSystem = AccessContextHelper.IsAuthorizedForCurrentSystem(
				accessContext,
				requestTrace,
				logger);

			if (!isAuthorizedForCurrentSystem)
			{
				requestTrace.LogStage(
					logger,
					nameof(LogStageParts.error_path),
					nameof(LogStageParts.reached));

				return Problem(
					title: Globals.currentSystemNoAuth,
					detail: Globals.noActiveMembershipAccess,
					statusCode: StatusCodes.Status403Forbidden);
			}

			var dataAccessStopwatch = Stopwatch.StartNew();

			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.data_access),
				nameof(LogStageParts.started));

			Dictionary<string, long> counts;
			ProofSystem? proofSystem;

			try
			{
				counts = await ReadCountsAsync(connection);
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
	private static async Task<Dictionary<string, long>> ReadCountsAsync(SqlConnection connection)
	{
		const string sql = """
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_source_systems;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_import_batches;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_systems;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_members;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_privacy_buckets;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_custom_fields;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_front_history;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_source_records;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_source_id_map;
		                   """;

		string[] names =
		[
			nameof(CountKeys.sourceSystems),
			nameof(CountKeys.importBatches),
			nameof(CountKeys.systems),
			nameof(CountKeys.members),
			nameof(CountKeys.privacyBuckets),
			nameof(CountKeys.customFields),
			nameof(CountKeys.frontHistory),
			nameof(CountKeys.sourceRecords),
			nameof(CountKeys.sourceIdMappings)
		];

		Dictionary<string, long> counts = new();

		await using SqlCommand command = new(sql, connection);
		await using var reader = await command.ExecuteReaderAsync();

		for (var index = 0; index < names.Length; index++)
		{
			if (await reader.ReadAsync())
			{
				counts[names[index]] = reader.GetInt64(0);
			}

			if (index < names.Length - 1)
			{
				await reader.NextResultAsync();
			}
		}

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
