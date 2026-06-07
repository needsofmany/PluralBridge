using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the read-only systems endpoint for the Phase 2B proof surface.
/// Systems represent imported PluralBridge system records from the validated proof database.
/// </summary>
[ApiController]
[Route("api/systems")]
public sealed class SystemsController(IConfiguration configuration) : ControllerBase
{
	/// <summary>
	/// Returns all systems present in the validated proof database.
	/// The response includes count metadata and keeps write capability explicitly disabled.
	/// </summary>
	/// <returns>
	/// HTTP 200 with system rows, total count, and read-only capability metadata.
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

		var systems = await ReadSystemsAsync(connection);

		return Ok(new
		{
			api = "PluralBridge.Api",
			phase = "Phase 2B",
			endpoint = "/api/systems",
			canWrite = false,
			count = systems.Count,
			systems
		});
	}

	/// <summary>
	/// Reads system rows from the validated proof database.
	/// The selected fields expose imported system metadata needed by the browser proof surface.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <returns>The system rows ordered by creation time and system identifier.</returns>
	private static async Task<List<SystemRecord>> ReadSystemsAsync(SqlConnection connection)
	{
		const string sql = """
		                   SELECT
		                       SystemId,
		                       SystemName,
		                       Description,
		                       Color,
		                       AvatarUrl,
		                       AvatarUuid,
		                       SourceCreatedAtMs,
		                       LastOperationTimeMs,
		                       ImportedAtUtc,
		                       CreatedAtUtc,
		                       UpdatedAtUtc
		                   FROM dbo.pb_systems
		                   ORDER BY CreatedAtUtc, SystemId;
		                   """;

		var systems = new List<SystemRecord>();

		await using var command = new SqlCommand(sql, connection);
		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			systems.Add(new SystemRecord(
				reader.GetGuid(0),
				reader.IsDBNull(1) ? null : reader.GetString(1),
				reader.IsDBNull(2) ? null : reader.GetString(2),
				reader.IsDBNull(3) ? null : reader.GetString(3),
				reader.IsDBNull(4) ? null : reader.GetString(4),
				reader.IsDBNull(5) ? null : reader.GetString(5),
				reader.IsDBNull(6) ? null : reader.GetInt64(6),
				reader.IsDBNull(7) ? null : reader.GetInt64(7),
				reader.GetDateTime(8),
				reader.GetDateTime(9),
				reader.IsDBNull(10) ? null : reader.GetDateTime(10)));
		}

		return systems;
	}

	private sealed record SystemRecord(
		Guid SystemId,
		string? SystemName,
		string? Description,
		string? Color,
		string? AvatarUrl,
		string? AvatarUuid,
		long? SourceCreatedAtMs,
		long? LastOperationTimeMs,
		DateTime ImportedAtUtc,
		DateTime CreatedAtUtc,
		DateTime? UpdatedAtUtc);
}