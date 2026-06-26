using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the read-only front history endpoint for the Phase 3 protected API surface.
/// Front history rows are returned only for the current system resolved from AccessContext.
/// </summary>
[ApiController]
[Route(Globals.frontHistoryRoute)]
public sealed class FrontHistoryController(IConfiguration configuration) : ControllerBase
{
	/// <summary>
	/// Returns all front history rows for the requested system when the requested system matches
	/// the current protected AccessContext system.
	/// </summary>
	/// <param name="systemId">The PluralBridge system identifier from the route.</param>
	/// <returns>
	/// HTTP 200 with front history rows, total count, and read-only capability metadata when authorized.
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
				endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.frontHistoryEndpointSegment}",
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

		var frontHistory = await ReadFrontHistoryAsync(
			connection,
			accessContext.CurrentSystem.SystemId);

		return Ok(new
		{
			api = Globals.apiName,
			phase = Globals.projectPhase,
			endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.frontHistoryEndpointSegment}",
			canWrite = false,
			systemId = accessContext.CurrentSystem.SystemId,
			count = frontHistory.Count,
			frontHistory
		});
	}

	/// <summary>
	/// Reads front history rows for one system from the validated proof database.
	/// The selected fields expose imported fronting timeline data and include member display names for browser readability.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <param name="systemId">The PluralBridge system identifier used to scope the front history query.</param>
	/// <returns>The front history rows ordered by start time and front history identifier.</returns>
	private static async Task<List<FrontHistoryRecord>> ReadFrontHistoryAsync(SqlConnection connection, Guid systemId)
	{
		const string sql = """
		                   SELECT
		                       fh.FrontHistoryId,
		                       fh.SystemId,
		                       fh.MemberId,
		                       m.DisplayName,
		                       fh.StartTimeMs,
		                       fh.EndTimeMs,
		                       fh.IsLive,
		                       fh.IsCustom,
		                       fh.CustomStatus,
		                       fh.LastOperationTimeMs,
		                       fh.ImportedAtUtc,
		                       fh.CreatedAtUtc,
		                       fh.UpdatedAtUtc
		                   FROM dbo.pb_front_history AS fh
		                   LEFT JOIN dbo.pb_members AS m
		                       ON m.MemberId = fh.MemberId
		                   WHERE fh.SystemId = @SystemId
		                   ORDER BY fh.StartTimeMs, fh.FrontHistoryId;
		                   """;

		var frontHistory = new List<FrontHistoryRecord>();

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@SystemId", systemId);

		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			frontHistory.Add(new FrontHistoryRecord(
				reader.GetGuid(0),
				reader.GetGuid(1),
				reader.GetGuid(2),
				reader.IsDBNull(3) ? null : reader.GetString(3),
				reader.GetInt64(4),
				reader.IsDBNull(5) ? null : reader.GetInt64(5),
				reader.IsDBNull(6) ? null : reader.GetBoolean(6),
				reader.IsDBNull(7) ? null : reader.GetBoolean(7),
				reader.IsDBNull(8) ? null : reader.GetString(8),
				reader.IsDBNull(9) ? null : reader.GetInt64(9),
				reader.GetDateTime(10),
				reader.GetDateTime(11),
				reader.IsDBNull(12) ? null : reader.GetDateTime(12)));
		}

		return frontHistory;
	}

	private sealed record FrontHistoryRecord(
		Guid FrontHistoryId,
		Guid SystemId,
		Guid MemberId,
		string? MemberDisplayName,
		long StartTimeMs,
		long? EndTimeMs,
		bool? IsLive,
		bool? IsCustom,
		string? CustomStatus,
		long? LastOperationTimeMs,
		DateTime ImportedAtUtc,
		DateTime CreatedAtUtc,
		DateTime? UpdatedAtUtc);
}