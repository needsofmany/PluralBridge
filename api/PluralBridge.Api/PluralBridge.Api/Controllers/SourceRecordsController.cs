using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the read-only source records endpoint for the Phase 3 proof surface.
/// Source records are returned as inventory metadata for a specific protected system route.
/// </summary>
[ApiController]
[Route(Globals.sourceRecordsRoute)]
public sealed class SourceRecordsController(IConfiguration configuration) : ControllerBase
{
	/// <summary>
	/// Returns source record inventory rows for the requested system route.
	/// The response includes count metadata and excludes raw imported JSON payloads.
	/// </summary>
	/// <param name="systemId">The PluralBridge system identifier used to scope the protected route.</param>
	/// <returns>
	/// HTTP 200 with source record inventory rows, total count, and read-only capability metadata.
	/// </returns>
	[HttpGet]
	public async Task<IActionResult> Get(Guid systemId)
	{
		var connectionString = configuration.GetConnectionString(Globals.connectionString);

		if (string.IsNullOrWhiteSpace(connectionString))
		{
			return Problem(
				title: Globals.missingConnectionString,
				detail: Globals.missingConnStringDetail,
				statusCode: StatusCodes.Status500InternalServerError);
		}

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		var accessContext = await AccessContextHelper.ResolveCurrentAccessAsync(connection);

		if (accessContext is null)
		{
			return Unauthorized(new
			{
				api = Globals.apiName,
				phase = Globals.projectPhase,
				endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.sourceRecordsEndpointSegment}",
				canWrite = false,
				systemId,
				error = Globals.cantResolveAccess
			});
		}

		if (!AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext)
			|| accessContext.CurrentSystem.SystemId != systemId)
		{
			return Forbid();
		}

		var sourceRecords = await ReadSourceRecordsAsync(connection);

		return Ok(new
		{
			api = Globals.apiName,
			phase = Globals.projectPhase,
			endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.sourceRecordsEndpointSegment}",
			canWrite = false,
			systemId = accessContext.CurrentSystem.SystemId,
			count = sourceRecords.Count,
			sourceRecords
		});
	}

	/// <summary>
	/// Reads source record inventory rows from the validated proof database.
	/// RawJson stays excluded so the endpoint exposes provenance and integrity metadata without private source payloads.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <returns>The source record inventory rows ordered by source entity type, source identifier, and source record identifier.</returns>
	private static async Task<List<SourceRecord>> ReadSourceRecordsAsync(SqlConnection connection)
	{
		const string sql = """
		                   SELECT
		                       SourceRecordId,
		                       ImportBatchId,
		                       SourceSystemCode,
		                       SourceEntityTypeCode,
		                       SourceId,
		                       SourceEndpoint,
		                       RawJsonSha256,
		                       ImportedAtUtc
		                   FROM dbo.pb_source_records
		                   ORDER BY SourceEntityTypeCode, SourceId, SourceRecordId;
		                   """;

		var sourceRecords = new List<SourceRecord>();

		await using var command = new SqlCommand(sql, connection);
		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			sourceRecords.Add(new SourceRecord(
				reader.GetGuid(0),
				reader.GetGuid(1),
				reader.GetString(2),
				reader.GetString(3),
				reader.IsDBNull(4) ? null : reader.GetString(4),
				reader.IsDBNull(5) ? null : reader.GetString(5),
				reader.IsDBNull(6) ? null : Convert.ToHexString((byte[])reader[6]),
				reader.GetDateTime(7)));
		}

		return sourceRecords;
	}

	private sealed record SourceRecord(
		Guid SourceRecordId,
		Guid ImportBatchId,
		string SourceSystemCode,
		string SourceEntityTypeCode,
		string? SourceId,
		string? SourceEndpoint,
		string? RawJsonSha256Hex,
		DateTime ImportedAtUtc);
}