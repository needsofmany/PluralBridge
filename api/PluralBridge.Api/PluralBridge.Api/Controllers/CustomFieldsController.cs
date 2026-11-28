// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the read-only custom fields endpoint for the Phase 3 protected API surface.
/// The requested system must be the current protected system. The rows belong to that system's account.
/// </summary>
[ApiController]
[Authorize]
[Route(Globals.customFieldsRoute)]
public sealed class CustomFieldsController(
	IConfiguration configuration,
	ILogger<CustomFieldsController> logger) : ControllerBase
{
	/// <summary>
	/// Returns the current account's custom fields when the requested system matches
	/// the current protected AccessContext system.
	/// </summary>
	/// <param name="systemId">The PluralBridge system identifier from the route.</param>
	/// <returns>
	/// HTTP 200 with custom field rows, total count, and read-only capability metadata when authorized.
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
				User,
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
					endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.customFieldsEndpointSegment}",
					canWrite = false,
					systemId,
					error = Globals.cantResolveAccess
				});
			}

			if (!AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext)
			    || accessContext.CurrentSystem is not { } currentSystem
			    || currentSystem.SystemId != systemId)
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

			List<CustomField> customFields;
			
			try
			{
				customFields = await ReadCustomFieldsAsync(
					connection,
					accessContext.CurrentAccount.AccountId);

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
				endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.customFieldsEndpointSegment}",
				canWrite = false,
				systemId = currentSystem.SystemId,
				count = customFields.Count,
				customFields
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
	/// Reads custom field rows for one account from the validated proof database.
	/// The selected fields expose imported custom-field metadata without exposing raw source payloads.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <param name="accountId">The authorized account. This is not the route system id.</param>
	/// <returns>The custom field rows ordered by display order, field name, and custom field identifier.</returns>
	private static async Task<List<CustomField>> ReadCustomFieldsAsync(SqlConnection connection, Guid accountId)
	{
		const string sql = """
		                   SELECT
		                       CustomFieldId,
		                       FieldName,
		                       Description,
		                       FieldTypeCode,
		                       DisplayOrderText,
		                       SupportsMarkdown,
		                       ImportedAtUtc,
		                       CreatedAtUtc,
		                       UpdatedAtUtc
		                   FROM dbo.pb_custom_fields
		                   WHERE AccountId = @AccountId
		                   ORDER BY DisplayOrderText, FieldName, CustomFieldId;
		                   """;

		var customFields = new List<CustomField>();

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.Add("@AccountId", System.Data.SqlDbType.UniqueIdentifier).Value = accountId;

		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			customFields.Add(new CustomField(
				reader.GetGuid(0),
				reader.GetString(1),
				reader.IsDBNull(2) ? null : reader.GetString(2),
				reader.IsDBNull(3) ? null : reader.GetInt32(3),
				reader.IsDBNull(4) ? null : reader.GetString(4),
				reader.IsDBNull(5) ? null : reader.GetBoolean(5),
				reader.GetDateTime(6),
				reader.GetDateTime(7),
				reader.IsDBNull(8) ? null : reader.GetDateTime(8)));
		}

		return customFields;
	}

	private sealed record CustomField(
		Guid CustomFieldId,
		string FieldName,
		string? Description,
		int? FieldTypeCode,
		string? DisplayOrderText,
		bool? SupportsMarkdown,
		DateTime ImportedAtUtc,
		DateTime CreatedAtUtc,
		DateTime? UpdatedAtUtc);
}
