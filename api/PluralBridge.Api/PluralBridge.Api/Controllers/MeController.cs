// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PluralBridge.Api.Workspace;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Authenticated account endpoints for the legacy access proof, the plural map, and member cards on a selected anchor.
/// </summary>
/// <remarks>
/// GET /api/me is the legacy proof. It returns the access context, a fixed proof system, and table counts, and it does not read or write the plural map.
/// The newer routes resolve the signed-in account and then read the plural map, anchor member profiles, avatar bytes, or persist a profile Save or an immediate Fronting change.
/// </remarks>
/// <param name="configuration">Application configuration. The database connection is read from the PluralBridge connection string.</param>
/// <param name="logger">Logger that receives request-trace stages for the proof, map, and profile routes, and one completion log for each avatar GET.</param>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class MeController(
	IConfiguration configuration,
	ILogger<MeController> logger,
	WorkspaceContextStore contexts) : ControllerBase
{
	/// <summary>
	/// Returns the legacy read-only access proof for the authenticated session.
	/// </summary>
	/// <returns>
	/// HTTP 200 with the access context, the earliest proof system, write capability set to false, and the proof table counts.
	/// HTTP 500 when the connection string, access context, or proof read is unavailable. This route does not load the plural map or a member profile.
	/// </returns>
	/// <remarks>
	/// This is the older proof endpoint. Account identity for the map and member routes is resolved separately and is not taken from the proof system.
	/// </remarks>
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
			// The proof read uses the same database as the member routes, but it does not accept a selected anchor.
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

			await using SqlConnection connection = new(connectionString);
			await connection.OpenAsync();
			var databaseName = connection.Database;

			// Access context is the legacy account, system memberships, and current system. A missing context is a proof failure, not an empty map.
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

				return Problem(
					title: Globals.currContextNotFound,
					detail: Globals.currConfiguredAccount,
					statusCode: StatusCodes.Status500InternalServerError);
			}

			var currentAccount = accessContext.CurrentAccount;
			var membershipAccess = accessContext.MembershipAccess;
			var currentSystem = accessContext.CurrentSystem;

			var dataAccessStopwatch = Stopwatch.StartNew();

			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.data_access),
				nameof(LogStageParts.started));

			Dictionary<string, long> counts;
			ProofSystem? proofSystem;

			try
			{
				counts = await ReadCountsAsync(
					connection,
					membershipAccess);
				proofSystem = await ReadProofSystemAsync(connection);

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
				mode = Globals.roProof,
				database = databaseName,
				canWrite = false,
				currentAccount,
				membershipAccess,
				currentSystem,
				proofSystem,
				counts
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
	/// Returns the signed-in account's plural map.
	/// </summary>
	/// <param name="cancellationToken">Cancels the account lookup, map procedure, or anchor-name read. A cancelled request is not reported as a map failure.</param>
	/// <returns>
	/// HTTP 200 with the mapped hierarchy when the task completes. HTTP 401 when the session has no account. HTTP 500 when the map procedure result cannot be mapped.
	/// </returns>
	/// <remarks>The map is not an authorization decision. Selecting an anchor later still requires that anchor's access membership.</remarks>
	[HttpGet("plural-map")]
	public async Task<IActionResult> GetPluralMap(CancellationToken cancellationToken)
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

			await using SqlConnection connection = new(connectionString);
			await connection.OpenAsync(cancellationToken);

			// The signed-in account is the map owner. The proof system is not consulted.
			var account = await AccessContextHelper.ResolveCurrentAccountAsync(connection, User);
			if (account is null)
			{
				requestTrace.LogStage(
					logger,
					nameof(LogStageParts.error_path),
					nameof(LogStageParts.reached));

				return Unauthorized(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint = "/api/me/plural-map",
					canWrite = false,
					error = Globals.cantResolveAccess
				});
			}

			await using var command = new SqlCommand(
				"dbo.pb_show_tree_step4_members_by_email",
				connection)
			{
				CommandType = CommandType.StoredProcedure
			};
			command.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = account.Email;

			PluralMapResponse? map;
			await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
			{
				var resultSets = await PluralMapReader.ReadAllAsync(reader, cancellationToken);
				map = PluralMapReader.Map(resultSets, account.AccountId);
			}

			if (map is null)
			{
				requestTrace.LogStage(
					logger,
					nameof(LogStageParts.error_path),
					nameof(LogStageParts.reached));

				return PluralMapProblem();
			}

			var anchorNames = await PluralMapReader.ReadAnchorNamesAsync(
				connection,
				map.HierarchyPreview,
				cancellationToken);
			map = map with
			{
				HierarchyPreview = PluralMapReader.ApplyAnchorNames(map.HierarchyPreview, anchorNames)
			};

			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.endpoint),
				nameof(LogStageParts.completed));

			return Ok(map);
		}
		catch (Exception) when (!cancellationToken.IsCancellationRequested)
		{
			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.error_path),
				nameof(LogStageParts.reached));

			return PluralMapProblem();
		}
	}

	/// <summary>
	/// Returns the generic plural-map failure without a database exception message.
	/// </summary>
	/// <returns>HTTP 500 whose detail says the plural map could not be read.</returns>
	private ObjectResult PluralMapProblem()
	{
		return Problem(
			title: Globals.requestFailed,
			detail: "The plural map could not be read.",
			statusCode: StatusCodes.Status500InternalServerError);
	}

	private const string AnchorMemberProfilesEndpoint = "/api/me/anchor-member-profiles";

	/// <summary>
	/// Saves the submitted profile fields, placement settings, and avatar for one member on the selected anchor.
	/// </summary>
	/// <param name="memberId">Member to update. The route accepts any GUID, including an empty GUID.</param>
	/// <param name="request">Save body. Null or an anchor that is not a system, group, or place id is HTTP 400. Omitted fields are not written.</param>
	/// <param name="cancellationToken">Cancels the connection, authorization, or write. A cancelled request is not mapped as a save failure.</param>
	/// <returns>
	/// HTTP 200 with the stored profile when the transaction commits. The profile object always includes the placement and avatar properties. Placement values are null for settings this response does not return as saved. avatarSaved is true when this request stored or removed an avatar. avatarUrl is the image route after a store and null after a removal or when the avatar was not part of the save.
	/// HTTP 401 when the session has no account. Validation and SQL authorization failures use their mapped status codes.
	/// </returns>
	/// <remarks>Profile, placement, and avatar commit in one transaction after authorization. Fronting is not part of this save.</remarks>
	[HttpPut("anchor-member-profiles/{memberId:guid}")]
	public async Task<IActionResult> SaveAnchorMemberProfile(
		Guid memberId,
		AnchorMemberProfileSaveRequest? request,
		CancellationToken cancellationToken)
	{
		var requestTrace = RequestTraceContext.Create(
			HttpContext.TraceIdentifier,
			HttpContext.Request.Headers.TryGetValue(Globals.correlationID, out var correlationId)
				? correlationId.ToString()
				: null);
		var endpoint = AnchorMemberProfilesEndpoint + "/" + memberId.ToString("D");

		// Reject the body before opening a connection. Placement and avatar are validated even when the profile text is accepted.
		if (!AnchorMemberProfileSave.TryPrepare(request, out var prepared, out var validationError) || prepared is null)
		{
			return AnchorProfileSaveError(
				StatusCodes.Status400BadRequest,
				endpoint,
				validationError ?? "The member profile could not be saved.",
				memberId);
		}

		if (!AnchorMemberProfileSave.TryPreparePlacement(request!, out var placement, out var placementError))
		{
			return AnchorProfileSaveError(
				StatusCodes.Status400BadRequest,
				endpoint,
				placementError ?? "The member profile could not be saved.",
				memberId);
		}

		if (!AnchorMemberProfileSave.TryPrepareAvatar(request, out var avatar, out var removeAvatar, out var avatarError))
		{
			return AnchorProfileSaveError(
				StatusCodes.Status400BadRequest,
				endpoint,
				avatarError ?? "The member profile could not be saved.",
				memberId);
		}

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

			await using SqlConnection connection = new(connectionString);
			await connection.OpenAsync(cancellationToken);

			var account = await AccessContextHelper.ResolveCurrentAccountAsync(connection, User);
			if (account is null)
			{
				requestTrace.LogStage(
					logger,
					nameof(LogStageParts.error_path),
					nameof(LogStageParts.reached));

				return Unauthorized(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint,
					canWrite = true,
					memberId,
					error = Globals.cantResolveAccess
				});
			}

			// Temporary graph tables must exist before the transaction. The write itself authorizes inside that transaction.
			await AnchorMemberProfileSave.CreateGraphTablesAsync(connection, cancellationToken);

			// Profile fields, placement settings, and avatar bytes commit
			// together. ExecuteAsync authorizes before any of those writes.
			await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
			try
			{
				var traceId = requestTrace.TraceId;
				if (traceId.Length > 100)
				{
					traceId = traceId[..100];
				}

				var profile = await AnchorMemberProfileSave.ExecuteAsync(
					connection,
					transaction,
					account.AccountId,
					memberId,
					prepared,
					request!.Branch,
					traceId,
					cancellationToken);

				AnchorMemberProfileSave.StoredPlacement? storedPlacement = null;
				if (placement is not null)
				{
					storedPlacement = await AnchorMemberProfileSave.ApplyPlacementAsync(
						connection,
						transaction,
						memberId,
						prepared,
						placement,
						cancellationToken);
				}

				if (avatar is not null)
				{
					await AnchorMemberProfileSave.ApplyAvatarAsync(
						connection,
						transaction,
						memberId,
						avatar,
						cancellationToken);
				}
				else if (removeAvatar)
				{
					await AnchorMemberProfileSave.RemoveAvatarAsync(
						connection,
						transaction,
						memberId,
						cancellationToken);
				}

				await transaction.CommitAsync(cancellationToken);
				if (removeAvatar)
				{
					contexts.ReleaseRetainedAvatar(account.AccountId, profile.MemberId);
				}

				return Ok(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint,
					canWrite = true,
					memberId = profile.MemberId,
					anchorObjectType = prepared.AnchorObjectType,
					anchorObjectId = prepared.AnchorObjectId,
					profile = new
					{
						memberId = profile.MemberId,
						displayName = profile.DisplayName,
						pronouns = profile.Pronouns,
						description = profile.Description,
						color = profile.Color,
						updatedAtUtc = profile.UpdatedAtUtc,
						placementVisibilityMode = storedPlacement is { SetVisibility: true }
							? storedPlacement.VisibilityMode
							: null,
						placementVisibilityTypeId = storedPlacement is { SetVisibility: true }
							? storedPlacement.VisibilityTypeId
							: null,
						placementPermissionMode = storedPlacement is { SetPermission: true }
							? storedPlacement.PermissionMode
							: null,
						placementPermissionCode = storedPlacement is { SetPermission: true }
							? storedPlacement.PermissionCode
							: null,
						avatarSaved = avatar is not null || removeAvatar,
						avatarUrl = avatar is not null
							? "/api/me/members/" + profile.MemberId.ToString("D") + "/avatar"
							: null
					}
				});
			}
			catch
			{
				try
				{
					await transaction.RollbackAsync(cancellationToken);
				}
				catch (Exception rollbackError) when (rollbackError is not OperationCanceledException)
				{
				}

				throw;
			}
		}
		catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
		{
			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.error_path),
				nameof(LogStageParts.reached));

			return MapAnchorProfileSaveSqlError(exception, endpoint, memberId);
		}
		catch (Exception) when (!cancellationToken.IsCancellationRequested)
		{
			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.error_path),
				nameof(LogStageParts.reached));

			return AnchorProfileSaveError(
				StatusCodes.Status500InternalServerError,
				endpoint,
				"The member profile could not be saved.");
		}
	}

	/// <summary>
	/// Maps a profile or Fronting SQL error number to the HTTP response for that failure.
	/// </summary>
	/// <param name="exception">SQL exception raised inside the write. Its number selects the message. The SQL text is not returned.</param>
	/// <param name="endpoint">Route written into the response.</param>
	/// <param name="memberId">Member named by the route.</param>
	/// <returns>The status and error body for a known number, or HTTP 500 for a number this method does not recognize.</returns>
	/// <remarks>
	/// A missing member is 404. A missing placement or a duplicate open front is 409. Missing storage and the unmapped numbers are 500. The remaining known authorization numbers are 403.
	/// </remarks>
	private IActionResult MapAnchorProfileSaveSqlError(SqlException exception, string endpoint, Guid memberId)
	{
		var message = exception.Number switch
		{
			AnchorMemberProfileSave.NoMembership => "No access membership exists for this account on the selected anchor.",
			AnchorMemberProfileSave.MultipleMemberships => "More than one access membership matches this account and anchor.",
			AnchorMemberProfileSave.InactiveMembership => "The access membership for this account on the selected anchor is not active.",
			AnchorMemberProfileSave.OwnerRequired => "An active Owner role is required on the selected anchor.",
			AnchorMemberProfileSave.InvalidBranch => "The selected branch is not valid.",
			AnchorMemberProfileSave.MemberMissing => "The member was not found.",
			AnchorMemberProfileSave.NotStructuralChild => "The member is not a structural child of the selected anchor.",
			AnchorMemberProfileSave.PlacementMissing => "The member has no placement row on the selected anchor.",
			AnchorMemberProfileSave.AvatarStorageMissing => "Avatar storage is not installed.",
			AnchorMemberProfileSave.FrontingStorageMissing => "Fronting storage is not installed.",
			AnchorMemberProfileSave.DuplicateOpenFront => "This member has more than one open fronting interval.",
			208 => "Visibility and Permissions storage is not installed.",
			515 => "Fronting cannot be saved for a systemless member until pb_front_history.SystemId allows NULL.",
			_ => null
		};

		if (message is null)
		{
			return AnchorProfileSaveError(
				StatusCodes.Status500InternalServerError,
				endpoint,
				"The member profile could not be saved.");
		}

		var status = exception.Number switch
		{
			AnchorMemberProfileSave.MemberMissing => StatusCodes.Status404NotFound,
			AnchorMemberProfileSave.PlacementMissing => StatusCodes.Status409Conflict,
			AnchorMemberProfileSave.AvatarStorageMissing => StatusCodes.Status500InternalServerError,
			AnchorMemberProfileSave.FrontingStorageMissing => StatusCodes.Status500InternalServerError,
			AnchorMemberProfileSave.DuplicateOpenFront => StatusCodes.Status409Conflict,
			208 => StatusCodes.Status500InternalServerError,
			515 => StatusCodes.Status500InternalServerError,
			_ => StatusCodes.Status403Forbidden
		};

		return AnchorProfileSaveError(status, endpoint, message, memberId);
	}

	/// <summary>
	/// Builds the save and Fronting error body. The response says writes are possible even when this attempt failed.
	/// </summary>
	/// <param name="statusCode">HTTP status for this failure.</param>
	/// <param name="endpoint">Route written into the body.</param>
	/// <param name="error">Client-facing message. It is not a SQL exception text.</param>
	/// <param name="memberId">Member named by the route, or null when the failure is not tied to one member.</param>
	/// <returns>A status-code result containing the error body.</returns>
	private ObjectResult AnchorProfileSaveError(int statusCode, string endpoint, string error, Guid? memberId = null)
	{
		return StatusCode(statusCode, new
		{
			api = Globals.apiName,
			phase = Globals.projectPhase,
			endpoint,
			canWrite = true,
			memberId,
			error
		});
	}

	/// <summary>
	/// Returns the member's stored avatar bytes when that member appears in the signed-in account's graph.
	/// </summary>
	/// <param name="memberId">Member whose image is requested.</param>
	/// <param name="cancellationToken">Cancels the account lookup or image read. A cancelled request is not reported as a missing image.</param>
	/// <returns>
	/// The image file when the task completes and a row exists. HTTP 404 when no row matches or avatar storage is absent. HTTP 401 when the session has no account.
	/// </returns>
	/// <remarks>
	/// Owner is not required. This read does not use the selected branch or the profile transaction.
	/// One completion log records the stage timings, the UTC time captured once beside the total stopwatch, and the SQL client connection id captured after the connection opens.
	/// A cancelled request is logged as cancelled and is not turned into an error response by this method.
	/// </remarks>
	[HttpGet("members/{memberId:guid}/avatar")]
	public async Task<IActionResult> GetMemberAvatar(Guid memberId, CancellationToken cancellationToken)
	{
		var total = Stopwatch.StartNew();
		var actionStartedAtUtc = DateTime.UtcNow.ToString(
			"yyyy-MM-ddTHH:mm:ss.fffffff'Z'",
			CultureInfo.InvariantCulture);
		var requestTrace = RequestTraceContext.Create(
			HttpContext.TraceIdentifier,
			HttpContext.Request.Headers.TryGetValue(Globals.correlationID, out var correlationId)
				? correlationId.ToString()
				: null);
		var timing = new AnchorMemberProfileSave.AvatarReadTiming();
		long? connectionOpenMilliseconds = null;
		long? accountResolutionMilliseconds = null;
		var activeStage = "connectionString";
		Guid? clientConnectionId = null;

		try
		{
			var connectionString = configuration.GetConnectionString(Globals.connectionString);
			if (string.IsNullOrWhiteSpace(connectionString))
			{
				total.Stop();
				LogAvatarGet(
					requestTrace,
					memberId,
					clientConnectionId,
					actionStartedAtUtc,
					StatusCodes.Status500InternalServerError,
					"failed",
					total.ElapsedMilliseconds,
					connectionOpenMilliseconds,
					accountResolutionMilliseconds,
					timing,
					activeStage,
					null,
					null);
				return Problem(
					title: Globals.missingConnectionString,
					detail: Globals.missingConnStringDetail,
					statusCode: StatusCodes.Status500InternalServerError);
			}

			activeStage = "connectionOpen";
			await using SqlConnection connection = new(connectionString);
			var connectionOpen = Stopwatch.StartNew();
			try
			{
				await connection.OpenAsync(cancellationToken);
			}
			finally
			{
				connectionOpen.Stop();
				connectionOpenMilliseconds = connectionOpen.ElapsedMilliseconds;
			}

			clientConnectionId = connection.ClientConnectionId;

			activeStage = "accountResolution";
			var accountResolution = Stopwatch.StartNew();
			AccessContextHelper.Account? account;
			try
			{
				account = await AccessContextHelper.ResolveCurrentAccountAsync(connection, User);
			}
			finally
			{
				accountResolution.Stop();
				accountResolutionMilliseconds = accountResolution.ElapsedMilliseconds;
			}
			if (account is null)
			{
				total.Stop();
				LogAvatarGet(
					requestTrace,
					memberId,
					clientConnectionId,
					actionStartedAtUtc,
					StatusCodes.Status401Unauthorized,
					"unauthorized",
					total.ElapsedMilliseconds,
					connectionOpenMilliseconds,
					accountResolutionMilliseconds,
					timing,
					null,
					null,
					null);
				return Unauthorized(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint = "/api/me/members/" + memberId.ToString("D") + "/avatar",
					canWrite = false,
					error = Globals.cantResolveAccess
				});
			}

			activeStage = AnchorMemberProfileSave.AvatarReadTiming.GraphTablesStage;
			// The action always uses the database reader. Source selection is not a request or configuration value.
			var image = await AnchorMemberProfileSave.LoadAvatarAsync(
				useDatabase: true,
				account.AccountId,
				memberId,
				cancellationToken,
				connection,
				timing);
			if (image is null)
			{
				total.Stop();
				LogAvatarGet(
					requestTrace,
					memberId,
					clientConnectionId,
					actionStartedAtUtc,
					StatusCodes.Status404NotFound,
					"notFound",
					total.ElapsedMilliseconds,
					connectionOpenMilliseconds,
					accountResolutionMilliseconds,
					timing,
					null,
					timing.ByteLength,
					null);
				return NotFound();
			}

			total.Stop();
			LogAvatarGet(
				requestTrace,
				memberId,
				clientConnectionId,
				actionStartedAtUtc,
				StatusCodes.Status200OK,
				"completed",
				total.ElapsedMilliseconds,
				connectionOpenMilliseconds,
				accountResolutionMilliseconds,
				timing,
				null,
				timing.ByteLength,
				null);
			return File(image.Value.Bytes, image.Value.ContentType);
		}
		catch (Exception exception)
		{
			total.Stop();
			var failedStage = timing.FailedStage ?? activeStage;
			if (exception is SqlException { Number: 208 })
			{
				LogAvatarGet(
					requestTrace,
					memberId,
					clientConnectionId,
					actionStartedAtUtc,
					StatusCodes.Status404NotFound,
					"failed",
					total.ElapsedMilliseconds,
					connectionOpenMilliseconds,
					accountResolutionMilliseconds,
					timing,
					failedStage,
					timing.ByteLength,
					exception);
				return NotFound();
			}

			if (cancellationToken.IsCancellationRequested)
			{
				LogAvatarGet(
					requestTrace,
					memberId,
					clientConnectionId,
					actionStartedAtUtc,
					null,
					"cancelled",
					total.ElapsedMilliseconds,
					connectionOpenMilliseconds,
					accountResolutionMilliseconds,
					timing,
					failedStage,
					timing.ByteLength,
					exception);
				throw;
			}

			LogAvatarGet(
				requestTrace,
				memberId,
				clientConnectionId,
				actionStartedAtUtc,
				StatusCodes.Status500InternalServerError,
				"failed",
				total.ElapsedMilliseconds,
				connectionOpenMilliseconds,
				accountResolutionMilliseconds,
				timing,
				failedStage,
				timing.ByteLength,
				exception);
			return Problem(
				title: Globals.requestFailed,
				detail: "The member avatar could not be read.",
				statusCode: StatusCodes.Status500InternalServerError);
		}
	}

	/// <summary>
	/// Writes one avatar GET completion log.
	/// </summary>
	/// <param name="requestTrace">Trace and correlation identifiers for this request.</param>
	/// <param name="memberId">Member whose image was requested.</param>
	/// <param name="clientConnectionId">SQL client connection id captured after open succeeds. Null when opening never succeeds. Every path for this request logs this same captured value, formatted as uppercase hexadecimal with hyphens.</param>
	/// <param name="actionStartedAtUtc">UTC time captured once from <see cref="DateTime.UtcNow"/> at the start of the action, beside the total stopwatch, formatted yyyy-MM-ddTHH:mm:ss.fffffffZ. Every path for this request logs this same value.</param>
	/// <param name="httpStatus">Status prepared for the response. Null when the request was cancelled before this method produced a response.</param>
	/// <param name="outcome">completed, notFound, unauthorized, failed, or cancelled.</param>
	/// <param name="totalMilliseconds">Elapsed time from entering the action through preparing the response.</param>
	/// <param name="connectionOpenMilliseconds">Connection open time. Null when the connection was not opened.</param>
	/// <param name="accountResolutionMilliseconds">Account resolution time. Null when resolution did not run.</param>
	/// <param name="timing">Graph and avatar-query timings. Stages that did not run are null.</param>
	/// <param name="failedStage">Stage that failed or was cancelled. Null when the request finished without one.</param>
	/// <param name="byteLength">Materialized image length. Null when no image bytes were read.</param>
	/// <param name="exception">The failure or cancellation exception. Null for a completed, unauthorized, or ordinary not-found result.</param>
	/// <returns>This method does not return a value.</returns>
	/// <remarks>
	/// ClientConnectionId is the value read from the open connection. It is not read again on a later path. ActionStartedAtUtc is the captured <see cref="DateTime.UtcNow"/> value, not the request-trace start and not the completion time.
	/// The log does not include credentials, the connection string, image bytes, profile text, or an account email.
	/// A failure is logged as an error and includes the SQL error number when the exception is a SQL exception. Cancellation is logged as information and includes the exception without being classified as a failure.
	/// TotalMs ends when the action has prepared its result. It does not include the time spent sending the image bytes to the browser.
	/// </remarks>
	private void LogAvatarGet(
		RequestTraceContext requestTrace,
		Guid memberId,
		Guid? clientConnectionId,
		string actionStartedAtUtc,
		int? httpStatus,
		string outcome,
		long totalMilliseconds,
		long? connectionOpenMilliseconds,
		long? accountResolutionMilliseconds,
		AnchorMemberProfileSave.AvatarReadTiming timing,
		string? failedStage,
		int? byteLength,
		Exception? exception)
	{
		var sqlErrorNumber = exception is SqlException sqlException ? sqlException.Number : (int?)null;
		var clientConnectionIdText = clientConnectionId?.ToString("D").ToUpperInvariant();
		if (outcome == "failed")
		{
			logger.LogError(
				exception,
				"AvatarGet TraceId={TraceId} CorrelationId={CorrelationId} ClientConnectionId={ClientConnectionId} ActionStartedAtUtc={ActionStartedAtUtc} MemberId={MemberId} HttpStatus={HttpStatus} Outcome={Outcome} FailedStage={FailedStage} SqlErrorNumber={SqlErrorNumber} ConnectionOpenMs={ConnectionOpenMs} AccountResolutionMs={AccountResolutionMs} GraphTablesMs={GraphTablesMs} TopologySeedMs={TopologySeedMs} TopologyWalkerMs={TopologyWalkerMs} GraphExpansionMs={GraphExpansionMs} AvatarQueryMs={AvatarQueryMs} TotalMs={TotalMs} ByteLength={ByteLength}",
				requestTrace.TraceId,
				requestTrace.CorrelationId,
				clientConnectionIdText,
				actionStartedAtUtc,
				memberId,
				httpStatus,
				outcome,
				failedStage,
				sqlErrorNumber,
				connectionOpenMilliseconds,
				accountResolutionMilliseconds,
				timing.GraphTablesMilliseconds,
				timing.TopologySeedMilliseconds,
				timing.TopologyWalkerMilliseconds,
				timing.GraphExpansionMilliseconds,
				timing.AvatarQueryMilliseconds,
				totalMilliseconds,
				byteLength);
			return;
		}

		logger.LogInformation(
			exception,
			"AvatarGet TraceId={TraceId} CorrelationId={CorrelationId} ClientConnectionId={ClientConnectionId} ActionStartedAtUtc={ActionStartedAtUtc} MemberId={MemberId} HttpStatus={HttpStatus} Outcome={Outcome} FailedStage={FailedStage} SqlErrorNumber={SqlErrorNumber} ConnectionOpenMs={ConnectionOpenMs} AccountResolutionMs={AccountResolutionMs} GraphTablesMs={GraphTablesMs} TopologySeedMs={TopologySeedMs} TopologyWalkerMs={TopologyWalkerMs} GraphExpansionMs={GraphExpansionMs} AvatarQueryMs={AvatarQueryMs} TotalMs={TotalMs} ByteLength={ByteLength}",
			requestTrace.TraceId,
			requestTrace.CorrelationId,
			clientConnectionIdText,
			actionStartedAtUtc,
			memberId,
			httpStatus,
			outcome,
			failedStage,
			sqlErrorNumber,
			connectionOpenMilliseconds,
			accountResolutionMilliseconds,
			timing.GraphTablesMilliseconds,
			timing.TopologySeedMilliseconds,
			timing.TopologyWalkerMilliseconds,
			timing.GraphExpansionMilliseconds,
			timing.AvatarQueryMilliseconds,
			totalMilliseconds,
			byteLength);
	}

	/// <summary>
	/// Persists Fronting immediately for one member on the selected anchor.
	/// </summary>
	/// <param name="memberId">Member whose open interval is changed.</param>
	/// <param name="request">Fronting body. Null, a null state, or an anchor that is not a system, group, or place id is HTTP 400.</param>
	/// <param name="cancellationToken">Cancels the account lookup or the fronting transaction. A cancelled request is not mapped as a save failure.</param>
	/// <returns>
	/// HTTP 200 with the stored fronting bit when the transaction commits. The same state does not open or close an interval.
	/// HTTP 401 when the session has no account. Authorization and storage failures use the shared SQL map.
	/// </returns>
	/// <remarks>This transaction does not update profile text, placement, or avatar.</remarks>
	[HttpPut("members/{memberId:guid}/fronting")]
	public async Task<IActionResult> SaveMemberFronting(
		Guid memberId,
		MemberFrontingSaveRequest? request,
		CancellationToken cancellationToken)
	{
		var endpoint = "/api/me/members/" + memberId.ToString("D") + "/fronting";
		// The selected occurrence and an explicit true or false are required before a transaction starts.
		if (request is null
			|| request.Fronting is null
			|| request.AnchorObjectType is not ("system" or "group" or "place")
			|| !Guid.TryParse(request.AnchorObjectId, out var anchorObjectId)
			|| anchorObjectId == Guid.Empty)
		{
			return AnchorProfileSaveError(
				StatusCodes.Status400BadRequest,
				endpoint,
				"Fronting requires the selected anchor and a true or false state.",
				memberId);
		}

		try
		{
			var connectionString = configuration.GetConnectionString(Globals.connectionString);
			if (string.IsNullOrWhiteSpace(connectionString))
			{
				return Problem(
					title: Globals.missingConnectionString,
					detail: Globals.missingConnStringDetail,
					statusCode: StatusCodes.Status500InternalServerError);
			}

			await using SqlConnection connection = new(connectionString);
			await connection.OpenAsync(cancellationToken);
			var account = await AccessContextHelper.ResolveCurrentAccountAsync(connection, User);
			if (account is null)
			{
				return Unauthorized(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint,
					canWrite = true,
					memberId,
					error = Globals.cantResolveAccess
				});
			}

			var requestTrace = RequestTraceContext.Create(
				HttpContext.TraceIdentifier,
				HttpContext.Request.Headers.TryGetValue(Globals.correlationID, out var correlationId)
					? correlationId.ToString()
					: null);
			var traceId = requestTrace.TraceId;
			if (traceId.Length > 100)
			{
				traceId = traceId[..100];
			}

			// Fronting commits alone. Profile Save does not call this transaction.
			await AnchorMemberProfileSave.CreateGraphTablesAsync(connection, cancellationToken);
			await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
			try
			{
				var fronting = await AnchorMemberProfileSave.ApplyFrontingAsync(
					connection,
					transaction,
					account.AccountId,
					memberId,
					request.AnchorObjectType,
					anchorObjectId,
					request.Fronting.Value,
					request.Branch,
					traceId,
					cancellationToken);
				await transaction.CommitAsync(cancellationToken);
				return Ok(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint,
					canWrite = true,
					memberId,
					fronting
				});
			}
			catch
			{
				try
				{
					await transaction.RollbackAsync(cancellationToken);
				}
				catch (Exception rollbackError) when (rollbackError is not OperationCanceledException)
				{
				}

				throw;
			}
		}
		catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
		{
			return MapAnchorProfileSaveSqlError(exception, endpoint, memberId);
		}
		catch (Exception) when (!cancellationToken.IsCancellationRequested)
		{
			return AnchorProfileSaveError(
				StatusCodes.Status500InternalServerError,
				endpoint,
				"Fronting could not be saved.",
				memberId);
		}
	}

	/// <summary>
	/// Returns member profiles for one named anchor on the signed-in account.
	/// </summary>
	/// <param name="objectType">system, group, or place. Null, blank, or any other type is HTTP 400.</param>
	/// <param name="anchorName">Anchor display name. Null, blank, or longer than 510 characters is HTTP 400.</param>
	/// <param name="objectId">Selected anchor id. Null, blank, or an empty guid returns the profile rows without placement, fronting, or avatar overlay.</param>
	/// <param name="branch">Selected occurrence chain. Null is sent as an empty branch and is not treated as a valid chain. The procedure still must return six sets.</param>
	/// <param name="cancellationToken">Cancels the account lookup or procedure. A cancelled request is not mapped as a profile failure.</param>
	/// <returns>
	/// HTTP 200 with the profile rows, or with placement, resolved display settings, fronting, and avatar URLs when <paramref name="objectId"/> is a guid.
	/// HTTP 401 when the session has no account. Procedure and contract failures use their mapped status codes.
	/// </returns>
	/// <remarks>The branch is always passed. A procedure that does not accept it is an installation error and is not retried without the branch.</remarks>
	[HttpGet("anchor-member-profiles")]
	public async Task<IActionResult> GetAnchorMemberProfiles(
		[FromQuery] string? objectType,
		[FromQuery] string? anchorName,
		[FromQuery] string? objectId,
		[FromQuery] string? branch,
		CancellationToken cancellationToken)
	{
		var requestTrace = RequestTraceContext.Create(
			HttpContext.TraceIdentifier,
			HttpContext.Request.Headers.TryGetValue(Globals.correlationID, out var correlationId)
				? correlationId.ToString()
				: null);

		// Type and name are required before the account or procedure is touched.
		if (string.IsNullOrWhiteSpace(objectType) || string.IsNullOrWhiteSpace(anchorName))
		{
			return AnchorProfileBadRequest("Anchor object type and anchor name are required.");
		}

		if (objectType is not ("system" or "group" or "place"))
		{
			return AnchorProfileBadRequest("Anchor object type must be system, group, or place.");
		}

		if (anchorName.Length > 510)
		{
			return AnchorProfileBadRequest("Anchor name must be 510 characters or fewer.");
		}

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

			await using SqlConnection connection = new(connectionString);
			await connection.OpenAsync(cancellationToken);

			var account = await AccessContextHelper.ResolveCurrentAccountAsync(connection, User);
			if (account is null)
			{
				requestTrace.LogStage(
					logger,
					nameof(LogStageParts.error_path),
					nameof(LogStageParts.reached));

				return Unauthorized(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint = AnchorMemberProfilesEndpoint,
					canWrite = false,
					error = Globals.cantResolveAccess
				});
			}

			// @BranchJson is always sent, so a one-set or three-set result is
			// an installation failure rather than a profile list without
			// placement, fronting, and avatar.
			var bundle = await ReadAnchorProfilesAsync(connection, account.Email, objectType, anchorName, branch, cancellationToken);
			var profiles = bundle.Profiles;

			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.endpoint),
				nameof(LogStageParts.completed));

			// Without an anchor id the response stays the profile rows. The six
			// sets were already required. Placement, fronting, and avatar are
			// overlaid only when the id is present, each from its own set.
			if (!Guid.TryParse(objectId, out var anchorObjectId) || anchorObjectId == Guid.Empty)
			{
				return Ok(profiles);
			}

			return Ok(AnchorMemberProfileMapper.Project(bundle, null, includeAvatar: true));
		}
		catch (AnchorProfileContractException exception) when (!cancellationToken.IsCancellationRequested)
		{
			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.error_path),
				nameof(LogStageParts.reached));

			return AnchorProfileInstallationError(exception.Message);
		}
		catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
		{
			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.error_path),
				nameof(LogStageParts.reached));

			return MapAnchorProfileProcedureError(exception.Number);
		}
		catch (Exception) when (!cancellationToken.IsCancellationRequested)
		{
			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.error_path),
				nameof(LogStageParts.reached));

			return AnchorProfileProblem();
		}
	}

	/// <summary>
	/// Calls the profile procedure with the account email and the selected branch, then requires the six-set contract.
	/// </summary>
	/// <param name="connection">Open connection for the signed-in account's database.</param>
	/// <param name="email">Account email used by the procedure to resolve the account. It is not taken from the query string.</param>
	/// <param name="objectType">system, group, or place. The caller has already rejected other values.</param>
	/// <param name="anchorName">Anchor display name, already limited to 510 characters.</param>
	/// <param name="branch">Browser branch. Null is passed as an empty string, which BranchJson turns into an empty array.</param>
	/// <param name="cancellationToken">Cancels the procedure or the result-set read.</param>
	/// <returns>The six-set bundle when the task completes.</returns>
	/// <exception cref="AnchorProfileContractException">The reader did not produce the six-set bundle.</exception>
	private static Task<AnchorProfileBundle> ReadAnchorProfilesAsync(
		SqlConnection connection,
		string email,
		string objectType,
		string anchorName,
		string? branch,
		CancellationToken cancellationToken)
	{
		return AnchorMemberProfileReader.ExecuteAsync(connection, email, objectType, anchorName, branch, cancellationToken);
	}

	/// <summary>
	/// Builds the avatar URL and cache version from the timestamp returned with the profile.
	/// </summary>
	/// <param name="memberId">Member whose image route is addressed.</param>
	/// <param name="updatedAtUtc">Avatar timestamp. It is formatted to three fractional digits so a later read can change the version.</param>
	/// <returns>The avatar route with an escaped v query value. The profile result carries no image bytes.</returns>
	/// <remarks>The format matches the three-digit timestamp the old SQL CONVERT style 126 put in the query string.</remarks>
	internal static string AvatarReferenceUrl(Guid memberId, DateTime updatedAtUtc)
	{
		return "/api/me/members/"
			+ memberId.ToString("D")
			+ "/avatar?v="
			+ Uri.EscapeDataString(updatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
	}

	/// <summary>
	/// Maps a profile-procedure error number to the read response.
	/// </summary>
	/// <param name="errorNumber">SQL error number. 8144 means the installed procedure has no branch parameter and is not retried without one.</param>
	/// <returns>The bad-request, not-found, forbidden, conflict, installation, or generic problem result for that number.</returns>
	private IActionResult MapAnchorProfileProcedureError(int errorNumber)
	{
		return errorNumber switch
		{
			50001 => AnchorProfileBadRequest("Anchor object type and anchor name are required."),
			50002 => AnchorProfileBadRequest("Anchor object type must be system, group, or place."),
			50004 => AnchorProfileNotFound(),
			50005 => AnchorProfileBadRequest("More than one anchor matches that type and name."),
			50006 => Forbid(),
			50007 => AnchorProfileBadRequest("More than one access membership matches this anchor."),
			50008 => Forbid(),
			50009 => AnchorProfileConflict("More than one membership row matches a selected member on this anchor."),
			// The installed procedure has no @BranchJson parameter. Do not
			// retry the read without it.
			8144 => AnchorProfileInstallationError(
				"The profile procedure does not accept branch context. Install scripts/sqlserver/Members/008_profile_supporting_reads.sql before using this API."),
			_ => AnchorProfileProblem()
		};
	}

	/// <summary>
	/// Returns HTTP 500 for a profile procedure that is missing or does not match the six-set contract.
	/// </summary>
	/// <param name="error">Installation message. It names the required script when the caller has one.</param>
	/// <returns>HTTP 500 with writes reported as false.</returns>
	private ObjectResult AnchorProfileInstallationError(string error)
	{
		return StatusCode(StatusCodes.Status500InternalServerError, new
		{
			api = Globals.apiName,
			phase = Globals.projectPhase,
			endpoint = AnchorMemberProfilesEndpoint,
			canWrite = false,
			error
		});
	}

	/// <summary>
	/// Returns HTTP 409 when one selected member has more than one membership row on the anchor.
	/// </summary>
	/// <param name="error">Conflict message returned to the client.</param>
	/// <returns>HTTP 409 with writes reported as false.</returns>
	private ConflictObjectResult AnchorProfileConflict(string error)
	{
		return Conflict(new
		{
			api = Globals.apiName,
			phase = Globals.projectPhase,
			endpoint = AnchorMemberProfilesEndpoint,
			canWrite = false,
			error
		});
	}

	/// <summary>
	/// Returns HTTP 400 for an anchor type, name, or procedure validation failure.
	/// </summary>
	/// <param name="error">Validation message returned to the client.</param>
	/// <returns>HTTP 400 with writes reported as false.</returns>
	private BadRequestObjectResult AnchorProfileBadRequest(string error)
	{
		return BadRequest(new
		{
			api = Globals.apiName,
			phase = Globals.projectPhase,
			endpoint = AnchorMemberProfilesEndpoint,
			canWrite = false,
			error
		});
	}

	/// <summary>
	/// Returns HTTP 404 when the named anchor is not on the signed-in account's map.
	/// </summary>
	/// <returns>HTTP 404 whose error says the requested anchor was not found.</returns>
	private NotFoundObjectResult AnchorProfileNotFound()
	{
		return NotFound(new
		{
			api = Globals.apiName,
			phase = Globals.projectPhase,
			endpoint = AnchorMemberProfilesEndpoint,
			canWrite = false,
			error = "The requested anchor was not found."
		});
	}

	/// <summary>
	/// Returns the generic profile-read failure without a database exception message.
	/// </summary>
	/// <returns>HTTP 500 whose detail says the anchor member profiles could not be read.</returns>
	private ObjectResult AnchorProfileProblem()
	{
		return Problem(
			title: Globals.requestFailed,
			detail: "The anchor member profiles could not be read.",
			statusCode: StatusCodes.Status500InternalServerError);
	}

	/// <summary>
	/// Counts the legacy proof tables for the supplied memberships.
	/// </summary>
	/// <param name="connection">Open connection. The count commands run on it without a transaction.</param>
	/// <param name="memberships">System memberships from the access context. An empty list returns zero counts and does not query.</param>
	/// <returns>The proof count names and their totals when the task completes. A scalar that is not a long, int, or decimal becomes zero.</returns>
	/// <remarks>
	/// Systems, members, privacy buckets, and front history are counted by <c>SystemId</c>.
	/// Custom fields belong to <c>pb_accounts</c> through <c>AccountId</c>, so that count uses the membership account ids.
	/// These totals belong to the legacy proof response. The plural map and member routes do not call this method.
	/// </remarks>
	private static async Task<Dictionary<string, long>> ReadCountsAsync(
		SqlConnection connection,
		IReadOnlyList<AccessContextHelper.SystemMembership> memberships)
	{
		var systemIds = memberships
			.Select(membership => membership.SystemId)
			.Distinct()
			.ToArray();

		Dictionary<string, long> counts = new()
		{
			[nameof(CountKeys.sourceSystems)] = 0,
			[nameof(CountKeys.importBatches)] = 0,
			[nameof(CountKeys.systems)] = 0,
			[nameof(CountKeys.members)] = 0,
			[nameof(CountKeys.privacyBuckets)] = 0,
			[nameof(CountKeys.customFields)] = 0,
			[nameof(CountKeys.frontHistory)] = 0,
			[nameof(CountKeys.sourceRecords)] = 0,
			[nameof(CountKeys.sourceIdMappings)] = 0
		};

		// No memberships means there is no system list to count. The names are still returned.
		if (systemIds.Length == 0)
		{
			return counts;
		}

		var idListBuilder = new StringBuilder();

		for (var index = 0; index < systemIds.Length; index++)
		{
			if (index > 0)
			{
				idListBuilder.Append(", ");
			}

			idListBuilder.Append("@SystemId").Append(index);
		}

		var systemIdInClause = idListBuilder.ToString();
		var accountIds = memberships
			.Select(membership => membership.AccountId)
			.Distinct()
			.ToArray();
		var accountIdListBuilder = new StringBuilder();

		for (var index = 0; index < accountIds.Length; index++)
		{
			if (index > 0)
			{
				accountIdListBuilder.Append(", ");
			}

			accountIdListBuilder.Append("@AccountId").Append(index);
		}

		var accountIdInClause = accountIdListBuilder.ToString();
		var sourceIdMapFilter = $"TRY_CONVERT(uniqueidentifier, sim.PluralBridgeId) IN ({systemIdInClause})";
		var sourceBatchFilter = $"SELECT DISTINCT sim.ImportBatchId FROM dbo.pb_source_id_map AS sim WHERE {sourceIdMapFilter}";

		/// <summary>
		/// Runs one proof count for the membership system and account ids.
		/// </summary>
		/// <param name="sql">A single SELECT that returns one numeric count. It may refer to the @SystemId and @AccountId parameters added here.</param>
		/// <returns>The count when the task completes. Zero when the scalar is null or not a long, int, or decimal.</returns>
		async Task<long> ReadCountAsync(string sql)
		{
			await using var command = new SqlCommand(sql, connection);

			for (var index = 0; index < systemIds.Length; index++)
			{
				command.Parameters.AddWithValue($"@SystemId{index}", systemIds[index]);
			}

			for (var index = 0; index < accountIds.Length; index++)
			{
				command.Parameters.AddWithValue($"@AccountId{index}", accountIds[index]);
			}

			var value = await command.ExecuteScalarAsync();

			return value switch
			{
				long asLong => asLong,
				int asInt => asInt,
				decimal asDecimal => (long)asDecimal,
				_ => 0
			};
		}

		counts[nameof(CountKeys.systems)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_systems WHERE SystemId IN ({systemIdInClause});");
		counts[nameof(CountKeys.members)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_members WHERE SystemId IN ({systemIdInClause});");
		counts[nameof(CountKeys.privacyBuckets)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_privacy_buckets WHERE SystemId IN ({systemIdInClause});");
		counts[nameof(CountKeys.customFields)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_custom_fields WHERE AccountId IN ({accountIdInClause});");
		counts[nameof(CountKeys.frontHistory)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_front_history WHERE SystemId IN ({systemIdInClause});");
		counts[nameof(CountKeys.sourceIdMappings)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_source_id_map AS sim WHERE {sourceIdMapFilter};");
		counts[nameof(CountKeys.importBatches)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM ({sourceBatchFilter}) AS batches;");
		counts[nameof(CountKeys.sourceRecords)] =
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_source_records AS sr WHERE sr.ImportBatchId IN ({sourceBatchFilter});");
		counts[nameof(CountKeys.sourceSystems)] =
			await ReadCountAsync(
				$"""
				SELECT CAST(COUNT_BIG(*) AS bigint)
				FROM dbo.pb_source_systems AS ss
				WHERE ss.SourceSystemCode IN
				(
				    SELECT DISTINCT sim.SourceSystemCode
				    FROM dbo.pb_source_id_map AS sim
				    WHERE {sourceIdMapFilter}
				      AND sim.SourceSystemCode IS NOT NULL
				);
				""");

		return counts;
	}

	/// <summary>
	/// Reads the earliest system row for the legacy proof response.
	/// </summary>
	/// <param name="connection">Open connection. The read is not filtered by the signed-in account.</param>
	/// <returns>The earliest system by creation time and id when the task completes, or null when pb_systems has no row.</returns>
	/// <remarks>Only the legacy proof GET uses this row. Map and member routes resolve the authenticated account instead.</remarks>
	private static async Task<ProofSystem?> ReadProofSystemAsync(SqlConnection connection)
	{
		const string sql = """
		                   SELECT TOP (1)
		                       SystemId,
		                       SystemName
		                   FROM dbo.pb_systems
		                   ORDER BY CreatedAtUtc, SystemId;
		                   """;

		await using SqlCommand command = new(sql, connection);
		await using var reader = await command.ExecuteReaderAsync();

		if (!await reader.ReadAsync())
		{
			return null;
		}

		return new ProofSystem(
			reader.GetGuid(0),
			reader.IsDBNull(1) ? null : reader.GetString(1));
	}

	/// <summary>
	/// The earliest system row returned by the legacy proof. It is not the account's selected anchor.
	/// </summary>
	/// <param name="SystemId">System id from pb_systems.</param>
	/// <param name="SystemName">System name, or null when the stored name is null.</param>
	private sealed record ProofSystem(
		Guid SystemId, 
		string? SystemName);
}
