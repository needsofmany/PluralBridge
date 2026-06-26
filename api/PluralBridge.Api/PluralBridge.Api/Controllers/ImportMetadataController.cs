using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the read-only import metadata endpoint for the Phase 2B proof surface.
/// Import metadata summarizes source-system, batch, source-record, and source-ID mapping state for the proof route.
/// </summary>
[ApiController]
[Route(Globals.importMetadataRoute)]
public sealed class ImportMetadataController(IConfiguration configuration) : ControllerBase
{
	/// <summary>
	/// Returns import metadata for the requested proof system route.
	/// The response summarizes import counts and latest batch metadata without exposing raw source payloads.
	/// </summary>
	/// <param name="systemId">The PluralBridge system identifier used to scope the proof route.</param>
	/// <returns>
	/// HTTP 200 with import metadata, aggregate counts, latest batch information, and read-only capability metadata.
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
				endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.importMetadataEndpointSegment}",
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

		var importMetadata = await ReadImportMetadataAsync(
			connection,
			accessContext.CurrentSystem.SystemId);

		return Ok(new
		{
			api = Globals.apiName,
			phase = Globals.projectPhase,
			endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.importMetadataEndpointSegment}",
			canWrite = false,
			systemId = accessContext.CurrentSystem.SystemId,
			importMetadata
		});
	}

	/// <summary>
	/// Reads aggregate import metadata from the validated proof database.
	/// The selected values give the browser a compact view of the import surface without returning private source JSON.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <param name="systemId">The PluralBridge system identifier used to confirm the proof system route.</param>
	/// <returns>Aggregate import metadata and latest import batch details.</returns>
	private static async Task<ImportMetadata> ReadImportMetadataAsync(SqlConnection connection, Guid systemId)
	{
		const string sql = """
		                   SELECT
		                       SystemExists = CAST(CASE WHEN EXISTS
		                       (
		                           SELECT 1
		                           FROM dbo.pb_systems
		                           WHERE SystemId = @SystemId
		                       )
		                       THEN 1 ELSE 0 END AS bit),
		                       SourceSystemCount = CAST((SELECT COUNT_BIG(*) FROM dbo.pb_source_systems) AS bigint),
		                       ImportBatchCount = CAST((SELECT COUNT_BIG(*) FROM dbo.pb_import_batches) AS bigint),
		                       SourceRecordCount = CAST((SELECT COUNT_BIG(*) FROM dbo.pb_source_records) AS bigint),
		                       SourceIdMappingCount = CAST((SELECT COUNT_BIG(*) FROM dbo.pb_source_id_map) AS bigint);

		                   SELECT TOP (1)
		                       ImportBatchId,
		                       SourceSystemCode,
		                       ImportStartedAtUtc,
		                       ImportCompletedAtUtc,
		                       ImportToolName,
		                       ImportToolVersion,
		                       SourceExportName,
		                       CreatedAtUtc
		                   FROM dbo.pb_import_batches
		                   ORDER BY ImportStartedAtUtc DESC, ImportBatchId;
		                   """;

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@SystemId", systemId);

		await using var reader = await command.ExecuteReaderAsync();

		await reader.ReadAsync();

		var systemExists = reader.GetBoolean(0);
		var sourceSystemCount = reader.GetInt64(1);
		var importBatchCount = reader.GetInt64(2);
		var sourceRecordCount = reader.GetInt64(3);
		var sourceIdMappingCount = reader.GetInt64(4);

		LatestImportBatch? latestImportBatch = null;

		await reader.NextResultAsync();

		if (await reader.ReadAsync())
		{
			latestImportBatch = new LatestImportBatch(
				reader.GetGuid(0),
				reader.GetString(1),
				reader.GetDateTime(2),
				reader.IsDBNull(3) ? null : reader.GetDateTime(3),
				reader.IsDBNull(4) ? null : reader.GetString(4),
				reader.IsDBNull(5) ? null : reader.GetString(5),
				reader.IsDBNull(6) ? null : reader.GetString(6),
				reader.GetDateTime(7));
		}

		return new ImportMetadata(
			systemExists,
			sourceSystemCount,
			importBatchCount,
			sourceRecordCount,
			sourceIdMappingCount,
			latestImportBatch);
	}

	private sealed record ImportMetadata(
		bool SystemExists,
		long SourceSystemCount,
		long ImportBatchCount,
		long SourceRecordCount,
		long SourceIdMappingCount,
		LatestImportBatch? LatestImportBatch);

	private sealed record LatestImportBatch(
		Guid ImportBatchId,
		string SourceSystemCode,
		DateTime ImportStartedAtUtc,
		DateTime? ImportCompletedAtUtc,
		string? ImportToolName,
		string? ImportToolVersion,
		string? SourceExportName,
		DateTime CreatedAtUtc);
}