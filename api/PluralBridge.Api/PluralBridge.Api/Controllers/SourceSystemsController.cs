using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the read-only source systems endpoint for the Phase 3 proof surface.
/// Source systems identify the external application families represented in the import data.
/// </summary>
[ApiController]
[Route(Globals.sourceSystemsRoute)]
public sealed class SourceSystemsController(IConfiguration configuration) : ControllerBase
{
	/// <summary>
	/// Returns all source systems present in the validated proof database.
	/// The response includes count metadata and keeps write capability explicitly disabled.
	/// </summary>
	/// <returns>
	/// HTTP 200 with source system rows, total count, and read-only capability metadata.
	/// </returns>
	[HttpGet]
	public async Task<IActionResult> Get()
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
				endpoint = Globals.sourceSystemsEndpoint,
				canWrite = false,
				error = Globals.cantResolveAccess
			});
		}

		if (!AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext))
		{
			return Forbid();
		}

		var sourceSystems = await ReadSourceSystemsAsync(connection);

		return Ok(new
		{
			api = Globals.apiName,
			phase = Globals.projectPhase,
			endpoint = Globals.sourceSystemsEndpoint,
			canWrite = false,
			systemId = accessContext.CurrentSystem.SystemId,
			count = sourceSystems.Count,
			sourceSystems
		});
	}

	/// <summary>
	/// Reads source system rows from the validated proof database.
	/// The selected fields expose descriptive source-system metadata without exposing private import payloads.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <returns>The source system rows ordered by source system code.</returns>
	private static async Task<List<SourceSystem>> ReadSourceSystemsAsync(SqlConnection connection)
	{
		const string sql = """
		                   SELECT
		                       SourceSystemCode,
		                       DisplayName,
		                       Description,
		                       ApiBaseUrl,
		                       CreatedAtUtc
		                   FROM dbo.pb_source_systems
		                   ORDER BY SourceSystemCode;
		                   """;

		var sourceSystems = new List<SourceSystem>();

		await using var command = new SqlCommand(sql, connection);
		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			sourceSystems.Add(new SourceSystem(
				reader.GetString(0),
				reader.GetString(1),
				reader.IsDBNull(2) ? null : reader.GetString(2),
				reader.IsDBNull(3) ? null : reader.GetString(3),
				reader.GetDateTime(4)));
		}

		return sourceSystems;
	}

	private sealed record SourceSystem(
		string SourceSystemCode,
		string DisplayName,
		string? Description,
		string? ApiBaseUrl,
		DateTime CreatedAtUtc);
}