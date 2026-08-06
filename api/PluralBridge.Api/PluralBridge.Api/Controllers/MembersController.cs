using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides protected member endpoints for the Phase 3 proof surface.
/// Members are returned for a specific imported PluralBridge system.
/// </summary>
[ApiController]
[Route(Globals.membersRoute)]
public sealed class MembersController(
	IConfiguration configuration,
	ILogger<MembersController> logger) : ControllerBase
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
		var requestTrace = RequestTraceContext.Create(
			HttpContext.TraceIdentifier,
			HttpContext.Request.Headers.TryGetValue(Globals.correlationID, out var correlationId)
				? correlationId.ToString()
				: null);

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

		try
		{
			await using var connection = new SqlConnection(connectionString);
			await connection.OpenAsync();

			var accessContext = await AccessContextHelper.ResolveCurrentAccessAsync(
				connection,
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
					endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}",
					canWrite = false,
					systemId,
					error = Globals.cantResolveAccess
				});
			}

			if (!AccessContextHelper.IsAuthorizedForCurrentSystem(
					accessContext,
					requestTrace,
					logger)
				|| accessContext.CurrentSystem.SystemId != systemId)
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

			List<Member>? members = null;
			var count = 0;

			try
			{
				members = await ReadMembersAsync(
					connection,
					accessContext.CurrentSystem.SystemId);
				count = members.Count;

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
				endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}",
				canWrite = false,
				systemId = accessContext.CurrentSystem.SystemId,
				count = count,
				members
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
	/// Returns one member for the requested system from the validated proof database.
	/// The response includes read-only capability metadata for the selected member.
	/// </summary>
	/// <param name="systemId">The PluralBridge system identifier used to scope the member query.</param>
	/// <param name="memberId">The PluralBridge member identifier.</param>
	/// <returns>
	/// HTTP 200 with the selected member and read-only capability metadata.
	/// </returns>
	[HttpGet(Globals.routeMemberId)]
	public async Task<IActionResult> Get(
		Guid systemId,
		Guid memberId)
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
					endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}/{memberId}",
					canWrite = false,
					systemId,
					memberId,
					error = Globals.cantResolveAccess
				});
			}

			if (!AccessContextHelper.IsAuthorizedForCurrentSystem(
					accessContext,
					requestTrace,
					logger)
				|| accessContext.CurrentSystem.SystemId != systemId)
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

			Member? member;

			try
			{
				member = await ReadMemberAsync(
					connection,
					accessContext.CurrentSystem.SystemId,
					memberId);

				if (member is null)
				{
					return NotFound(new
					{
						api = Globals.apiName,
						phase = Globals.projectPhase,
						endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}/{memberId}",
						canWrite = false,
						systemId = accessContext.CurrentSystem.SystemId,
						memberId
					});
				}
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
				endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}/{memberId}",
				canWrite = false,
				systemId = accessContext.CurrentSystem.SystemId,
				memberId,
				member
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
	/// Creates one member in the current protected system.
	/// The route system must match the current AccessContext system.
	/// </summary>
	/// <param name="systemId">The PluralBridge system identifier used to scope the member create request.</param>
	/// <param name="request">The approved member create request fields.</param>
	/// <returns>
	/// HTTP 201 with the created member and read-only capability metadata.
	/// </returns>
	[HttpPost]
	public async Task<IActionResult> Create(
		Guid systemId,
		AddMemberRequest? request)
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

			if (request is null || string.IsNullOrWhiteSpace(request.DisplayName))
			{
				return BadRequest(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}",
					canWrite = true,
					systemId,
					error = "DisplayName is required."
				});
			}

			await using var connection = new SqlConnection(connectionString);
			await connection.OpenAsync();

			var accessContext = await AccessContextHelper.ResolveCurrentAccessAsync(
				connection,
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
					endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}",
					canWrite = true,
					systemId,
					error = Globals.cantResolveAccess
				});
			}

			if (!AccessContextHelper.IsAuthorizedForCurrentSystem(
					accessContext,
					requestTrace,
					logger)
				|| accessContext.CurrentSystem.SystemId != systemId)
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

			Member member;

			try
			{
				member = await CreateMemberAsync(
					connection,
					accessContext.CurrentSystem.SystemId,
					request);

				await MemberWriteAuditWriter.WriteAsync(
					connection,
					new MemberWriteAuditWriter.MemberWriteAuditInput(
						accessContext.CurrentSystem.SystemId,
						accessContext.CurrentAccount.AccountId,
						accessContext.CurrentSystem.SystemMembershipId,
						member.MemberId,
						MemberWriteAuditWriter.MemberAddOperation,
						requestTrace.TraceId));

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

			return Created(
				$"{Globals.systemsEndpointRoot}/{accessContext.CurrentSystem.SystemId}/{Globals.membersEndpointSegment}/{member.MemberId}",
				new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}/{member.MemberId}",
					canWrite = true,
					systemId = accessContext.CurrentSystem.SystemId,
					memberId = member.MemberId,
					member
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

	/// <summary>
	/// Updates one existing member in the current protected system.
	/// The route system must match the current AccessContext system.
	/// </summary>
	/// <param name="systemId">The PluralBridge system identifier used to scope the member edit request.</param>
	/// <param name="memberId">The PluralBridge member identifier.</param>
	/// <param name="request">The approved member edit request fields.</param>
	/// <returns>
	/// HTTP 200 with the updated member and read-only capability metadata.
	/// </returns>
	[HttpPut(Globals.routeMemberId)]
	public async Task<IActionResult> Edit(
		Guid systemId,
		Guid memberId,
		EditMemberRequest? request)
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

			if (request is null || string.IsNullOrWhiteSpace(request.DisplayName))
			{
				return BadRequest(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}/{memberId}",
					canWrite = true,
					systemId,
					memberId,
					error = "DisplayName is required."
				});
			}

			await using var connection = new SqlConnection(connectionString);
			await connection.OpenAsync();

			var accessContext = await AccessContextHelper.ResolveCurrentAccessAsync(
				connection,
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
					endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}/{memberId}",
					canWrite = true,
					systemId,
					memberId,
					error = Globals.cantResolveAccess
				});
			}

			if (!AccessContextHelper.IsAuthorizedForCurrentSystem(
					accessContext,
					requestTrace,
					logger)
				|| accessContext.CurrentSystem.SystemId != systemId)
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

			Member? member;

			try
			{
				member = await UpdateMemberAsync(
					connection,
					accessContext.CurrentSystem.SystemId,
					memberId,
					request);

				if (member is null)
				{
					dataAccessStopwatch.Stop();

					requestTrace.LogStage(
						logger,
						nameof(LogStageParts.data_access),
						nameof(LogStageParts.completed),
						dataAccessStopwatch.Elapsed);

					return NotFound(new
					{
						api = Globals.apiName,
						phase = Globals.projectPhase,
						endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}/{memberId}",
						canWrite = true,
						systemId = accessContext.CurrentSystem.SystemId,
						memberId
					});
				}

				await MemberWriteAuditWriter.WriteAsync(
					connection,
					new MemberWriteAuditWriter.MemberWriteAuditInput(
						accessContext.CurrentSystem.SystemId,
						accessContext.CurrentAccount.AccountId,
						accessContext.CurrentSystem.SystemMembershipId,
						member.MemberId,
						MemberWriteAuditWriter.MemberEditOperation,
						requestTrace.TraceId));

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
				endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.membersEndpointSegment}/{memberId}",
				canWrite = true,
				systemId = accessContext.CurrentSystem.SystemId,
				memberId,
				member
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
	/// Reads one member row for one system from the validated proof database.
	/// The selected fields expose imported member metadata without exposing raw source payloads.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <param name="systemId">The PluralBridge system identifier used to scope the member query.</param>
	/// <param name="memberId">The PluralBridge member identifier.</param>
	/// <returns>The selected member row, or null when no matching row exists.</returns>
	private static async Task<Member?> ReadMemberAsync(
		SqlConnection connection,
		Guid systemId,
		Guid memberId)
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
	                     AND MemberId = @MemberId;
	                   """;

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@SystemId", systemId);
		command.Parameters.AddWithValue("@MemberId", memberId);

		await using var reader = await command.ExecuteReaderAsync();

		if (!await reader.ReadAsync())
		{
			return null;
		}

		return new Member(
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
			reader.IsDBNull(16) ? null : reader.GetDateTime(16));
	}

	/// <summary>
	/// Creates one member row for one system in the validated proof database.
	/// System authority comes from the protected AccessContext, not from the request body.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <param name="systemId">The current protected PluralBridge system identifier.</param>
	/// <param name="request">The approved member create request fields.</param>
	/// <returns>The created member row.</returns>
	private static async Task<Member> CreateMemberAsync(
		SqlConnection connection,
		Guid systemId,
		AddMemberRequest? request)
	{
		var memberId = Guid.NewGuid();
		var nowUtc = DateTime.UtcNow;

		const string sql = """
	                   INSERT INTO dbo.pb_members
	                   (
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
	                   )
	                   VALUES
	                   (
	                       @MemberId,
	                       @SystemId,
	                       @DisplayName,
	                       @Pronouns,
	                       @Description,
	                       @Color,
	                       @IsArchived,
	                       @ArchivedReason,
	                       @IsPrivate,
	                       @PreventTrusted,
	                       @PreventsFrontNotifications,
	                       @ReceiveMessageBoardNotifications,
	                       @SupportsDescriptionMarkdown,
	                       @LastOperationTimeMs,
	                       @ImportedAtUtc,
	                       @CreatedAtUtc,
	                       @UpdatedAtUtc
	                   );
	                   """;

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@MemberId", memberId);
		command.Parameters.AddWithValue("@SystemId", systemId);
		command.Parameters.AddWithValue("@DisplayName", request?.DisplayName.Trim());
		command.Parameters.AddWithValue("@Pronouns", string.IsNullOrWhiteSpace(request?.Pronouns) ? DBNull.Value : request.Pronouns.Trim());
		command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(request?.Description) ? DBNull.Value : request.Description.Trim());
		command.Parameters.AddWithValue("@Color", string.IsNullOrWhiteSpace(request?.Color) ? DBNull.Value : request.Color.Trim());
		command.Parameters.AddWithValue("@IsArchived", false);
		command.Parameters.AddWithValue("@ArchivedReason", DBNull.Value);
		command.Parameters.AddWithValue("@IsPrivate", request?.IsPrivate ?? false);
		command.Parameters.AddWithValue("@PreventTrusted", request?.PreventTrusted ?? false);
		command.Parameters.AddWithValue("@PreventsFrontNotifications", request?.PreventsFrontNotifications ?? false);
		command.Parameters.AddWithValue("@ReceiveMessageBoardNotifications", request?.ReceiveMessageBoardNotifications ?? false);
		command.Parameters.AddWithValue("@SupportsDescriptionMarkdown", request?.SupportsDescriptionMarkdown ?? false);
		command.Parameters.AddWithValue("@LastOperationTimeMs", DBNull.Value);
		command.Parameters.AddWithValue("@ImportedAtUtc", nowUtc);
		command.Parameters.AddWithValue("@CreatedAtUtc", nowUtc);
		command.Parameters.AddWithValue("@UpdatedAtUtc", nowUtc);

		await command.ExecuteNonQueryAsync();

		return new Member(
			memberId,
			systemId,
			request?.DisplayName.Trim(),
			string.IsNullOrWhiteSpace(request?.Pronouns) ? null : request?.Pronouns.Trim(),
			string.IsNullOrWhiteSpace(request?.Description) ? null : request?.Description.Trim(),
			string.IsNullOrWhiteSpace(request?.Color) ? null : request?.Color.Trim(),
			false,
			null,
			request?.IsPrivate ?? false,
			request?.PreventTrusted ?? false,
			request?.PreventsFrontNotifications ?? false,
			request?.ReceiveMessageBoardNotifications ?? false,
			request?.SupportsDescriptionMarkdown ?? false,
			null,
			nowUtc,
			nowUtc,
			nowUtc);
	}

	/// <summary>
	/// Updates one member row for one system in the validated proof database.
	/// System authority comes from the protected AccessContext, not from the request body.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <param name="systemId">The current protected PluralBridge system identifier.</param>
	/// <param name="memberId">The PluralBridge member identifier.</param>
	/// <param name="request">The approved member edit request fields.</param>
	/// <returns>The updated member row, or null when no matching current-system member exists.</returns>
	private static async Task<Member?> UpdateMemberAsync(
		SqlConnection connection,
		Guid systemId,
		Guid memberId,
		EditMemberRequest? request)
	{
		var nowUtc = DateTime.UtcNow;

		const string sql = """
	                   UPDATE dbo.pb_members
	                   SET
	                       DisplayName = @DisplayName,
	                       Pronouns = @Pronouns,
	                       Description = @Description,
	                       Color = @Color,
	                       IsPrivate = @IsPrivate,
	                       PreventTrusted = @PreventTrusted,
	                       PreventsFrontNotifications = @PreventsFrontNotifications,
	                       ReceiveMessageBoardNotifications = @ReceiveMessageBoardNotifications,
	                       SupportsDescriptionMarkdown = @SupportsDescriptionMarkdown,
	                       UpdatedAtUtc = @UpdatedAtUtc
	                   WHERE SystemId = @SystemId
	                     AND MemberId = @MemberId;
	                   """;

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@SystemId", systemId);
		command.Parameters.AddWithValue("@MemberId", memberId);
		command.Parameters.AddWithValue("@DisplayName", request?.DisplayName.Trim());
		command.Parameters.AddWithValue("@Pronouns", string.IsNullOrWhiteSpace(request?.Pronouns) ? DBNull.Value : request.Pronouns.Trim());
		command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(request?.Description) ? DBNull.Value : request.Description.Trim());
		command.Parameters.AddWithValue("@Color", string.IsNullOrWhiteSpace(request?.Color) ? DBNull.Value : request.Color.Trim());
		command.Parameters.AddWithValue("@IsPrivate", request?.IsPrivate ?? false);
		command.Parameters.AddWithValue("@PreventTrusted", request?.PreventTrusted ?? false);
		command.Parameters.AddWithValue("@PreventsFrontNotifications", request?.PreventsFrontNotifications ?? false);
		command.Parameters.AddWithValue("@ReceiveMessageBoardNotifications", request?.ReceiveMessageBoardNotifications ?? false);
		command.Parameters.AddWithValue("@SupportsDescriptionMarkdown", request?.SupportsDescriptionMarkdown ?? false);
		command.Parameters.AddWithValue("@UpdatedAtUtc", nowUtc);

		var rowsAffected = await command.ExecuteNonQueryAsync();

		if (rowsAffected == 0)
		{
			return null;
		}

		return await ReadMemberAsync(
			connection,
			systemId,
			memberId);
	}

	private sealed record Member(
		Guid MemberId,
		Guid SystemId,
		string? DisplayName,
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

	public sealed record AddMemberRequest(
		string DisplayName,
		string? Pronouns,
		string? Description,
		string? Color,
		bool? IsPrivate,
		bool? PreventTrusted,
		bool? PreventsFrontNotifications,
		bool? ReceiveMessageBoardNotifications,
		bool? SupportsDescriptionMarkdown);

	public sealed record EditMemberRequest(
		string DisplayName,
		string? Pronouns,
		string? Description,
		string? Color,
		bool? IsPrivate,
		bool? PreventTrusted,
		bool? PreventsFrontNotifications,
		bool? ReceiveMessageBoardNotifications,
		bool? SupportsDescriptionMarkdown);
}