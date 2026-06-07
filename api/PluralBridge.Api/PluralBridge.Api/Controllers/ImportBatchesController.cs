using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the read-only import batches endpoint for the Phase 2B proof surface.
/// Import batches describe when source data was imported and which source system produced it.
/// </summary>
[ApiController]
[Route("api/import-batches")]
public sealed class ImportBatchesController(IConfiguration configuration) : ControllerBase
{
	/// <summary>
	/// Returns all import batches present in the validated proof database.
	/// The response includes count metadata and excludes raw source payloads.
	/// </summary>
	/// <returns>
	/// HTTP 200 with import batch rows, total count, and read-only capability metadata.
	/// </returns>
	[HttpGet]
	public async Task<IActionResult> Get()
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

		var importBatches = await ReadImportBatchesAsync(connection);

		return Ok(new
		{
			api = "PluralBridge.Api",
			phase = "Phase 2B",
			endpoint = "/api/import-batches",
			canWrite = false,
			count = importBatches.Count,
			importBatches
		});
	}

	/// <summary>
	/// Reads import batch rows from the validated proof database.
	/// The selected fields expose operational import metadata without returning private source JSON.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <returns>The import batch rows ordered by import start time and batch identifier.</returns>
	private static async Task<List<ImportBatch>> ReadImportBatchesAsync(SqlConnection connection)
	{
		const string sql = """
		                   SELECT
		                       ImportBatchId,
		                       SourceSystemCode,
		                       ImportStartedAtUtc,
		                       ImportCompletedAtUtc,
		                       ImportToolName,
		                       ImportToolVersion,
		                       SourceExportName,
		                       SourceExportSha256,
		                       CreatedAtUtc
		                   FROM dbo.pb_import_batches
		                   ORDER BY ImportStartedAtUtc, ImportBatchId;
		                   """;

		var importBatches = new List<ImportBatch>();

		await using var command = new SqlCommand(sql, connection);
		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			importBatches.Add(new ImportBatch(
				reader.GetGuid(0),
				reader.GetString(1),
				reader.GetDateTime(2),
				reader.IsDBNull(3) ? null : reader.GetDateTime(3),
				reader.IsDBNull(4) ? null : reader.GetString(4),
				reader.IsDBNull(5) ? null : reader.GetString(5),
				reader.IsDBNull(6) ? null : reader.GetString(6),
				reader.IsDBNull(7) ? null : Convert.ToHexString((byte[])reader[7]),
				reader.GetDateTime(8)));
		}

		return importBatches;
	}

	private sealed record ImportBatch(
		Guid ImportBatchId,
		string SourceSystemCode,
		DateTime ImportStartedAtUtc,
		DateTime? ImportCompletedAtUtc,
		string? ImportToolName,
		string? ImportToolVersion,
		string? SourceExportName,
		string? SourceExportSha256Hex,
		DateTime CreatedAtUtc);
}