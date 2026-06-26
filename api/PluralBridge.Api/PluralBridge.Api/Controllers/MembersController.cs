using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the read-only members endpoint for the Phase 2B proof surface.
/// Members are returned for a specific imported PluralBridge system.
/// </summary>
[ApiController]
[Route(Globals.membersRoute)]
public sealed class MembersController(IConfiguration configuration) : ControllerBase
{
	/// <summary>
	/// Returns all members for the requested system from the validated proof database.
	/// The response includes count metadata and keeps write capability explicitly disabled.
	/// </summary>
	/// <param name="systemId">The PluralBridge system identifier used to scope the member query.</param>
	/// <returns>
	/// HTTP 200 with member rows, total count, and read-only capability metadata.
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
				endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}",
				canWrite = false,
				systemId,
				error = Globals.cantResolveAccess
			});
		}

		if (!AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext) || accessContext.CurrentSystem.SystemId != systemId)
		{
			return Forbid();
		}

		var members = await ReadMembersAsync(
			connection,
			accessContext.CurrentSystem.SystemId);

		return Ok(new
		{
			api = Globals.apiName,
			phase = Globals.projectPhase,
			endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}",
			canWrite = false,
			systemId = accessContext.CurrentSystem.SystemId,
			count = members.Count,
			members
		});
	}

	/// <summary>
	/// Reads member rows for one system from the validated proof database.
	/// The selected fields expose imported member metadata without exposing raw source payloads.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <param name="systemId">The PluralBridge system identifier used to scope the member query.</param>
	/// <returns>The member rows ordered by display name and member identifier.</returns>
	private static async Task<List<Member>> ReadMembersAsync(SqlConnection connection, Guid systemId)
	{
		const string sql = """
		                   SELECT
		                       MemberId,
		                       SystemId,
		                       DisplayName,
		                       Pronouns,
		                       Description,
		                       Color,
		                       IsArchived,
		                       ArchivedReason,
		                       IsPrivate,
		                       PreventTrusted,
		                       PreventsFrontNotifications,
		                       ReceiveMessageBoardNotifications,
		                       SupportsDescriptionMarkdown,
		                       LastOperationTimeMs,
		                       ImportedAtUtc,
		                       CreatedAtUtc,
		                       UpdatedAtUtc
		                   FROM dbo.pb_members
		                   WHERE SystemId = @SystemId
		                   ORDER BY DisplayName, MemberId;
		                   """;

		var members = new List<Member>();

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@SystemId", systemId);

		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			members.Add(new Member(
				reader.GetGuid(0),
				reader.GetGuid(1),
				reader.GetString(2),
				reader.IsDBNull(3) ? null : reader.GetString(3),
				reader.IsDBNull(4) ? null : reader.GetString(4),
				reader.IsDBNull(5) ? null : reader.GetString(5),
				reader.IsDBNull(6) ? null : reader.GetBoolean(6),
				reader.IsDBNull(7) ? null : reader.GetString(7),
				reader.IsDBNull(8) ? null : reader.GetBoolean(8),
				reader.IsDBNull(9) ? null : reader.GetBoolean(9),
				reader.IsDBNull(10) ? null : reader.GetBoolean(10),
				reader.IsDBNull(11) ? null : reader.GetBoolean(11),
				reader.IsDBNull(12) ? null : reader.GetBoolean(12),
				reader.IsDBNull(13) ? null : reader.GetInt64(13),
				reader.GetDateTime(14),
				reader.GetDateTime(15),
				reader.IsDBNull(16) ? null : reader.GetDateTime(16)));
		}

		return members;
	}

	private sealed record Member(
		Guid MemberId,
		Guid SystemId,
		string DisplayName,
		string? Pronouns,
		string? Description,
		string? Color,
		bool? IsArchived,
		string? ArchivedReason,
		bool? IsPrivate,
		bool? PreventTrusted,
		bool? PreventsFrontNotifications,
		bool? ReceiveMessageBoardNotifications,
		bool? SupportsDescriptionMarkdown,
		long? LastOperationTimeMs,
		DateTime ImportedAtUtc,
		DateTime CreatedAtUtc,
		DateTime? UpdatedAtUtc);
}