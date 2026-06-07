using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the read-only source ID mappings endpoint for the Phase 2B proof surface.
/// Source ID mappings connect external source identifiers to PluralBridge entity identifiers.
/// </summary>
[ApiController]
[Route("api/systems/{systemId:guid}/source-id-mappings")]
public sealed class SourceIdMappingsController(IConfiguration configuration) : ControllerBase
{
	/// <summary>
	/// Returns source ID mapping rows for the requested proof system route.
	/// The response includes count metadata and keeps write capability explicitly disabled.
	/// </summary>
	/// <param name="systemId">The PluralBridge system identifier used to scope the proof route.</param>
	/// <returns>
	/// HTTP 200 with source ID mapping rows, total count, and read-only capability metadata.
	/// </returns>
	[HttpGet]
	public async Task<IActionResult> Get(Guid systemId)
	{
		var connectionString = configuration.GetConnectionString("PluralBridgeProof");

		if (string.IsNullOrWhiteSpace(connectionString))
		{
			return Problem(
				title: "Missing connection string",
				detail: "ConnectionStrings:PluralBridgeProof was not found.",
				statusCode: StatusCodes.Status500InternalServerError);
		}

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		var sourceIdMappings = await ReadSourceIdMappingsAsync(connection);

		return Ok(new
		{
			api = "PluralBridge.Api",
			phase = "Phase 2B",
			endpoint = $"/api/systems/{systemId}/source-id-mappings",
			canWrite = false,
			systemId,
			count = sourceIdMappings.Count,
			sourceIdMappings
		});
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