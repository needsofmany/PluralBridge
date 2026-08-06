using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the read-only source ID mappings endpoint for the Phase 3 proof surface.
/// Source ID mappings connect external source identifiers to PluralBridge entity identifiers.
/// </summary>
[ApiController]
[Route(Globals.sourceIdMappingsRoute)]
public sealed class SourceIdMappingsController(
	IConfiguration configuration,
	ILogger<SourceIdMappingsController> logger) : ControllerBase
{
	/// <summary>
	/// Returns source ID mapping rows for the requested system route.
	/// The response includes count metadata and keeps write capability explicitly disabled.
	/// </summary>
	/// <param name="systemId">The PluralBridge system identifier used to scope the protected route.</param>
	/// <returns>
	/// HTTP 200 with source ID mapping rows, total count, and read-only capability metadata.
	/// </returns>
	[HttpGet]
	public async Task<IActionResult> Get(Guid systemId)
	{
		var requestTrace = RequestTraceContext.Create(
			HttpContext.TraceIdentifier,
			HttpContext.Request.Headers.TryGetValue(Globals.correlationID, out var correlationId)
				? correlationId.ToString()
				: null);

		try
		{
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

			await using var connection = new SqlConnection(connectionString);
			await connection.OpenAsync();

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

				return Unauthorized(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.sourceIdMappingsEndpointSegment}",
					canWrite = false,
					systemId,
					error = Globals.cantResolveAccess
				});
			}

			if (!AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext)
			    || accessContext.CurrentSystem.SystemId != systemId)
			{
				requestTrace.LogStage(
					logger,
					nameof(LogStageParts.error_path),
					nameof(LogStageParts.reached));

				return Forbid();
			}

			var dataAccessStopwatch = Stopwatch.StartNew();

			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.data_access),
				nameof(LogStageParts.started));

			List<SourceIdMapping> sourceIdMappings;

			try
			{
				sourceIdMappings = await ReadSourceIdMappingsAsync(connection);

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
				endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.sourceIdMappingsEndpointSegment}",
				canWrite = false,
				systemId = accessContext.CurrentSystem.SystemId,
				count = sourceIdMappings.Count,
				sourceIdMappings
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
	/// Reads source ID mapping rows from the validated proof database.
	/// These rows provide traceability from source-system identifiers to PluralBridge identifiers.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <returns>The source ID mapping rows ordered by source entity type, source identifier, and mapping identifier.</returns>
	private static async Task<List<SourceIdMapping>> ReadSourceIdMappingsAsync(SqlConnection connection)
	{
		const string sql = """
		                   SELECT
		                       SourceIdMapId,
		                       SourceSystemCode,
		                       SourceEntityTypeCode,
		                       SourceId,
		                       PluralBridgeEntityTypeCode,
		                       PluralBridgeId,
		                       ImportBatchId,
		                       CreatedAtUtc
		                   FROM dbo.pb_source_id_map
		                   ORDER BY SourceEntityTypeCode, SourceId, SourceIdMapId;
		                   """;

		var sourceIdMappings = new List<SourceIdMapping>();

		await using var command = new SqlCommand(sql, connection);
		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			sourceIdMappings.Add(new SourceIdMapping(
				reader.GetGuid(0),
				reader.GetString(1),
				reader.GetString(2),
				reader.GetString(3),
				reader.GetString(4),
				reader.GetGuid(5),
				reader.GetGuid(6),
				reader.GetDateTime(7)));
		}

		return sourceIdMappings;
	}

	private sealed record SourceIdMapping(
		Guid SourceIdMapId,
		string SourceSystemCode,
		string SourceEntityTypeCode,
		string SourceId,
		string PluralBridgeEntityTypeCode,
		Guid PluralBridgeId,
		Guid ImportBatchId,
		DateTime CreatedAtUtc);
}