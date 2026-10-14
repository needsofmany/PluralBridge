// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Linq;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the read-only systems endpoint for the Phase 3 proof surface.
/// Systems represent imported PluralBridge system records from the validated proof database.
/// </summary>
[ApiController]
[Authorize]
[Route(Globals.systemsRouteRoot)]
public sealed class SystemsController(
	IConfiguration configuration,
	ILogger<SystemsController> logger) : ControllerBase
{
	/// <summary>
	/// Returns the current system resolved from the active access context.
	/// The response includes count metadata and keeps write capability explicitly disabled.
	/// </summary>
	/// <returns>
	/// HTTP 200 with system rows, total count, and read-only capability metadata.
	/// </returns>
	[HttpGet]
	public async Task<IActionResult> Get()
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
					endpoint = Globals.systemsEndpointRoot,
					canWrite = false,
					error = Globals.cantResolveAccess
				});
			}

			if (!AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext))
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

			List<SystemRecord> systems;
			try
			{
				systems = await ReadSystemsAsync(connection, accessContext.MembershipAccess);

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
				endpoint = Globals.systemsEndpointRoot,
				canWrite = false,
				systemId = accessContext.CurrentSystem.SystemId,
				count = systems.Count,
				systems
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
	/// Reads the current system row from the validated proof database.
	/// The selected fields expose imported system metadata needed by the browser proof surface.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <param name="memberships">The authorized active memberships for the current account.</param>
	/// <returns>The authorized system rows ordered by creation time and system identifier.</returns>
	private static async Task<List<SystemRecord>> ReadSystemsAsync(
		SqlConnection connection,
		IReadOnlyList<AccessContextHelper.SystemMembership> memberships)
	{
		var systemIds = memberships
			.Select(membership => membership.SystemId)
			.Distinct()
			.ToArray();

		if (systemIds.Length == 0)
		{
			return [];
		}

		var systemIdInClause = BuildSystemIdInClause(systemIds);

		var sql = new StringBuilder(
			"""
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
			WHERE SystemId IN (
			""")
			.Append(systemIdInClause)
			.AppendLine(")")
			.AppendLine("ORDER BY CreatedAtUtc, SystemId;");

		var systems = new List<SystemRecord>();

		await using var command = new SqlCommand(sql.ToString(), connection);

		for (var index = 0; index < systemIds.Length; index++)
		{
			command.Parameters.AddWithValue($"@SystemId{index}", systemIds[index]);
		}

		await using (var reader = await command.ExecuteReaderAsync())
		{
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
					reader.IsDBNull(10) ? null : reader.GetDateTime(10),
					[]));
			}
		}

		var groupsBySystemId = await ReadGroupsBySystemIdAsync(connection, systemIds);

		return systems
			.Select(system =>
				system with
				{
					Groups = groupsBySystemId.TryGetValue(system.SystemId, out var groups)
						? groups
						: []
				})
			.ToList();
	}

	private static string BuildSystemIdInClause(Guid[] systemIds)
	{
		var idListBuilder = new StringBuilder();

		for (var index = 0; index < systemIds.Length; index++)
		{
			if (index > 0)
			{
				idListBuilder.Append(", ");
			}

			idListBuilder.Append("@SystemId").Append(index);
		}

		return idListBuilder.ToString();
	}

	private static async Task<Dictionary<Guid, IReadOnlyList<GroupRecord>>> ReadGroupsBySystemIdAsync(
		SqlConnection connection,
		Guid[] systemIds)
	{
		if (systemIds.Length == 0)
		{
			return [];
		}

		var hasGroupsTable = await TableExistsAsync(connection, "dbo.pb_groups");
		if (!hasGroupsTable)
		{
			return [];
		}

		var systemIdInClause = BuildSystemIdInClause(systemIds);
		var memberIdsByGroup = new Dictionary<(Guid SystemId, Guid GroupId), List<Guid>>();

		var hasGroupMembersTable = await TableExistsAsync(connection, "dbo.pb_group_members");
		if (hasGroupMembersTable)
		{
			var membershipsSql =
				$"""
				  SELECT
				      SystemId,
				      GroupId,
				      MemberId
				  FROM dbo.pb_group_members
				  WHERE SystemId IN ({systemIdInClause});
				  """;

			await using var membershipCommand = new SqlCommand(membershipsSql, connection);
			for (var index = 0; index < systemIds.Length; index++)
			{
				membershipCommand.Parameters.AddWithValue($"@SystemId{index}", systemIds[index]);
			}

			await using var membershipReader = await membershipCommand.ExecuteReaderAsync();
			while (await membershipReader.ReadAsync())
			{
				var key = (membershipReader.GetGuid(0), membershipReader.GetGuid(1));
				if (!memberIdsByGroup.TryGetValue(key, out var memberIds))
				{
					memberIds = [];
					memberIdsByGroup[key] = memberIds;
				}

				memberIds.Add(membershipReader.GetGuid(2));
			}
		}

		var groupsSql =
			$"""
			  SELECT
			      GroupId,
			      SystemId,
			      SourceGroupId,
			      ParentGroupId,
			      ParentSourceGroupId,
			      GroupName
			  FROM dbo.pb_groups
			  WHERE SystemId IN ({systemIdInClause})
			  ORDER BY SystemId, GroupName, GroupId;
			  """;

		var groupsBySystemId = new Dictionary<Guid, List<GroupRecord>>();

		await using (var groupCommand = new SqlCommand(groupsSql, connection))
		{
			for (var index = 0; index < systemIds.Length; index++)
			{
				groupCommand.Parameters.AddWithValue($"@SystemId{index}", systemIds[index]);
			}

			await using var groupReader = await groupCommand.ExecuteReaderAsync();
			while (await groupReader.ReadAsync())
			{
				var groupId = groupReader.GetGuid(0);
				var systemId = groupReader.GetGuid(1);
				var key = (systemId, groupId);

				var group = new GroupRecord(
					groupId,
					systemId,
					groupReader.IsDBNull(2) ? null : groupReader.GetString(2),
					groupReader.IsDBNull(3) ? null : groupReader.GetGuid(3),
					groupReader.IsDBNull(4) ? null : groupReader.GetString(4),
					groupReader.GetString(5),
					memberIdsByGroup.TryGetValue(key, out var memberIds)
						? memberIds
						: []);

				if (!groupsBySystemId.TryGetValue(systemId, out var groups))
				{
					groups = [];
					groupsBySystemId[systemId] = groups;
				}

				groups.Add(group);
			}
		}

		return groupsBySystemId.ToDictionary(
			entry => entry.Key,
			entry => (IReadOnlyList<GroupRecord>)entry.Value);
	}

	private static async Task<bool> TableExistsAsync(SqlConnection connection, string tableName)
	{
		const string sql = """
		                   SELECT CASE WHEN OBJECT_ID(@TableName, 'U') IS NULL THEN 0 ELSE 1 END;
		                   """;
		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@TableName", tableName);
		var result = await command.ExecuteScalarAsync();

		return result switch
		{
			int asInt => asInt == 1,
			long asLong => asLong == 1,
			decimal asDecimal => asDecimal == 1,
			_ => false
		};
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
		DateTime? UpdatedAtUtc,
		IReadOnlyList<GroupRecord> Groups);

	private sealed record GroupRecord(
		Guid GroupId,
		Guid SystemId,
		string? SourceGroupId,
		Guid? ParentGroupId,
		string? ParentSourceGroupId,
		string GroupName,
		IReadOnlyList<Guid> MemberIds);
}
