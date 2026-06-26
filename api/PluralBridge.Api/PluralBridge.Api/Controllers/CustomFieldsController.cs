using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the read-only custom fields endpoint for the Phase 3 protected API surface.
/// Custom fields are returned only for the current system resolved from AccessContext.
/// </summary>
[ApiController]
[Route(Globals.customFieldsRoute)]
public sealed class CustomFieldsController(IConfiguration configuration) : ControllerBase
{
	/// <summary>
	/// Returns all custom fields for the requested system when the requested system matches
	/// the current protected AccessContext system.
	/// </summary>
	/// <param name="systemId">The PluralBridge system identifier from the route.</param>
	/// <returns>
	/// HTTP 200 with custom field rows, total count, and read-only capability metadata when authorized.
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
				endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.customFieldsEndpointSegment}",
				canWrite = false,
				systemId,
				error = Globals.cantResolveAccess
			});
		}

		if (!AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext) || accessContext.CurrentSystem.SystemId != systemId)
		{
			return Forbid();
		}

		var customFields = await ReadCustomFieldsAsync(
			connection,
			accessContext.CurrentSystem.SystemId);

		return Ok(new
		{
			api = Globals.apiName,
			phase = Globals.projectPhase,
			endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.customFieldsEndpointSegment}",
			canWrite = false,
			systemId = accessContext.CurrentSystem.SystemId,
			count = customFields.Count,
			customFields
		});
	}

	/// <summary>
	/// Reads custom field rows for one system from the validated proof database.
	/// The selected fields expose imported custom-field metadata without exposing raw source payloads.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <param name="systemId">The PluralBridge system identifier used to scope the custom field query.</param>
	/// <returns>The custom field rows ordered by display order, field name, and custom field identifier.</returns>
	private static async Task<List<CustomField>> ReadCustomFieldsAsync(SqlConnection connection, Guid systemId)
	{
		const string sql = """
		                   SELECT
		                       CustomFieldId,
		                       SystemId,
		                       FieldName,
		                       Description,
		                       FieldTypeCode,
		                       DisplayOrderText,
		                       SupportsMarkdown,
		                       ImportedAtUtc,
		                       CreatedAtUtc,
		                       UpdatedAtUtc
		                   FROM dbo.pb_custom_fields
		                   WHERE SystemId = @SystemId
		                   ORDER BY DisplayOrderText, FieldName, CustomFieldId;
		                   """;

		var customFields = new List<CustomField>();

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@SystemId", systemId);

		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			customFields.Add(new CustomField(
				reader.GetGuid(0),
				reader.GetGuid(1),
				reader.GetString(2),
				reader.IsDBNull(3) ? null : reader.GetString(3),
				reader.IsDBNull(4) ? null : reader.GetInt32(4),
				reader.IsDBNull(5) ? null : reader.GetString(5),
				reader.IsDBNull(6) ? null : reader.GetBoolean(6),
				reader.GetDateTime(7),
				reader.GetDateTime(8),
				reader.IsDBNull(9) ? null : reader.GetDateTime(9)));
		}

		return customFields;
	}

	private sealed record CustomField(
		Guid CustomFieldId,
		Guid SystemId,
		string FieldName,
		string? Description,
		int? FieldTypeCode,
		string? DisplayOrderText,
		bool? SupportsMarkdown,
		DateTime ImportedAtUtc,
		DateTime CreatedAtUtc,
		DateTime? UpdatedAtUtc);
}