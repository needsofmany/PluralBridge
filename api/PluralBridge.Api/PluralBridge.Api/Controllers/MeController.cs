// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the /api/me endpoint for the Chapter 2 access context and authorization-gated proof data.
/// This controller verifies that the API can reach the validated PluralBridge cloud proof database
/// and returns the current access context, resolved system, authorization-gated proof data, and table counts.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class MeController(
	IConfiguration configuration,
	ILogger<MeController> logger) : ControllerBase
{
	/// <summary>
	/// Provides the /api/me endpoint for the Chapter 2 access context and authorization-gated proof data.
	/// The response is intentionally read-only and includes the proof system plus validated table counts
	/// so the browser can confirm that it is receiving real database-backed data.
	/// </summary>
	/// <returns>
	/// HTTP 200 with proof metadata, the selected proof system, write capability set to false,
	/// and counts for the Phase 2B source and 1-6 slice tables.
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
			// get info to connect to the database
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

			// get connection to the database
			await using SqlConnection connection = new(connectionString);
			await connection.OpenAsync();
			var databaseName = connection.Database;

			// should contain the account and it's available System memberships
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

			// retrieve everything we need to know for this System
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

	private ObjectResult PluralMapProblem()
	{
		return Problem(
			title: Globals.requestFailed,
			detail: "The plural map could not be read.",
			statusCode: StatusCodes.Status500InternalServerError);
	}

	private const string AnchorMemberProfilesEndpoint = "/api/me/anchor-member-profiles";

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

		if (!AnchorMemberProfileSave.TryPrepareAvatar(request, out var avatar, out var avatarError))
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

			await AnchorMemberProfileSave.CreateGraphTablesAsync(connection, cancellationToken);

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

				await transaction.CommitAsync(cancellationToken);

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
						avatarSaved = avatar is not null,
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

	[HttpGet("members/{memberId:guid}/avatar")]
	public async Task<IActionResult> GetMemberAvatar(Guid memberId, CancellationToken cancellationToken)
	{
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
					endpoint = "/api/me/members/" + memberId.ToString("D") + "/avatar",
					canWrite = false,
					error = Globals.cantResolveAccess
				});
			}

			var image = await AnchorMemberProfileSave.ReadAvatarAsync(
				connection,
				account.AccountId,
				memberId,
				cancellationToken);
			if (image is null)
			{
				return NotFound();
			}

			return File(image.Value.Bytes, image.Value.ContentType);
		}
		catch (SqlException exception) when (exception.Number == 208)
		{
			return NotFound();
		}
		catch (Exception) when (!cancellationToken.IsCancellationRequested)
		{
			return Problem(
				title: Globals.requestFailed,
				detail: "The member avatar could not be read.",
				statusCode: StatusCodes.Status500InternalServerError);
		}
	}

	[HttpPut("members/{memberId:guid}/fronting")]
	public async Task<IActionResult> SaveMemberFronting(
		Guid memberId,
		MemberFrontingSaveRequest? request,
		CancellationToken cancellationToken)
	{
		var endpoint = "/api/me/members/" + memberId.ToString("D") + "/fronting";
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

			var bundle = await ReadAnchorProfilesAsync(connection, account.Email, objectType, anchorName, branch, cancellationToken);
			var profiles = bundle.Profiles;

			requestTrace.LogStage(
				logger,
				nameof(LogStageParts.endpoint),
				nameof(LogStageParts.completed));

			if (!Guid.TryParse(objectId, out var anchorObjectId) || anchorObjectId == Guid.Empty)
			{
				return Ok(profiles);
			}

			var placementByMember = bundle.Placements.ToDictionary(
				row => row.MemberId,
				row => new AnchorMemberProfileSave.MemberPlacementRow(
					row.MemberId,
					row.VisibilityMode,
					row.VisibilityTypeId,
					row.PermissionMode,
					row.PermissionCode));
			var openFronting = bundle.Fronting.ToDictionary(row => row.MemberId, row => row.IsFronting);
			var avatarPaths = bundle.Avatars.ToDictionary(
				row => row.MemberId,
				row => AvatarReferenceUrl(row.MemberId, row.UpdatedAtUtc));

			var branchNodes = bundle.BranchValid
				? bundle.BranchAnchors
					.Select(anchor => new AnchorMemberProfileSave.BranchNode(anchor.ObjectType, anchor.ObjectId))
					.ToArray()
				: Array.Empty<AnchorMemberProfileSave.BranchNode>();
			var anchorSettings = bundle.BranchValid
				? bundle.BranchAnchors
					.Select(anchor => new AnchorMemberProfileSave.AnchorSetting(
						anchor.ObjectType,
						anchor.ObjectId,
						anchor.VisibilityMode ?? "unset",
						anchor.VisibilityTypeId,
						anchor.PermissionMode ?? "unset",
						anchor.PermissionCode))
					.ToArray()
				: Array.Empty<AnchorMemberProfileSave.AnchorSetting>();

			return Ok(profiles.Select(profile =>
			{
				placementByMember.TryGetValue(profile.MemberId, out var placement);
				var resolved = AnchorMemberProfileSave.ResolvePlacement(
					placement,
					branchNodes,
					anchorSettings,
					bundle.BranchValid);
				var isFronting = openFronting.TryGetValue(profile.MemberId, out var openInterval)
					? openInterval
					: profile.IsFronting;
				avatarPaths.TryGetValue(profile.MemberId, out var avatarUrl);

				return new
				{
					memberId = profile.MemberId,
					displayName = profile.DisplayName,
					pronouns = profile.Pronouns,
					description = profile.Description,
					color = profile.Color,
					visibilityTypeId = profile.VisibilityTypeId,
					visibilityTypeCode = profile.VisibilityTypeCode,
					visibilityTypeName = profile.VisibilityTypeName,
					isFronting,
					placementVisibilityMode = placement?.VisibilityMode,
					placementVisibilityTypeId = placement?.VisibilityTypeId,
					placementPermissionMode = placement?.PermissionMode,
					placementPermissionCode = placement?.PermissionCode,
					resolvedVisibilityMode = resolved.VisibilityMode,
					resolvedVisibilityTypeId = resolved.VisibilityTypeId,
					resolvedPermissionMode = resolved.PermissionMode,
					resolvedPermissionCode = resolved.PermissionCode,
					avatarUrl
				};
			}));
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

	private static async Task<AnchorProfileBundle> ReadAnchorProfilesAsync(
		SqlConnection connection,
		string email,
		string objectType,
		string anchorName,
		string? branch,
		CancellationToken cancellationToken)
	{
		await using var command = new SqlCommand(
			"dbo.pb_get_anchor_member_profiles_by_email",
			connection)
		{
			CommandType = CommandType.StoredProcedure
		};
		command.Parameters.Add("@Email", SqlDbType.NVarChar, 320).Value = email;
		command.Parameters.Add("@AnchorObjectType", SqlDbType.NVarChar, 64).Value = objectType;
		command.Parameters.Add("@AnchorName", SqlDbType.NVarChar, 510).Value = anchorName;
		command.Parameters.Add("@BranchJson", SqlDbType.NVarChar, -1).Value =
			AnchorMemberProfileSave.BranchJson(branch ?? "");

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		return await AnchorMemberProfileReader.ReadAsync(reader, cancellationToken)
			?? throw new AnchorProfileContractException(
				"The profile procedure did not return the six-result-set contract. Install scripts/sqlserver/Members/008_profile_supporting_reads.sql before using this API.");
	}

	internal static string AvatarReferenceUrl(Guid memberId, DateTime updatedAtUtc)
	{
		return "/api/me/members/"
			+ memberId.ToString("D")
			+ "/avatar?v="
			+ Uri.EscapeDataString(updatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
	}

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
			8144 => AnchorProfileInstallationError(
				"The profile procedure does not accept branch context. Install scripts/sqlserver/Members/008_profile_supporting_reads.sql before using this API."),
			_ => AnchorProfileProblem()
		};
	}

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

	private ObjectResult AnchorProfileProblem()
	{
		return Problem(
			title: Globals.requestFailed,
			detail: "The anchor member profiles could not be read.",
			statusCode: StatusCodes.Status500InternalServerError);
	}

	/// <summary>
	/// Reads count totals from the source and 1-6 proof tables in the validated database.
	/// These totals are used as a compact integrity check for the Phase 2B browser proof.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <returns>A dictionary keyed by API-facing count names.</returns>
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
		var sourceIdMapFilter = $"TRY_CONVERT(uniqueidentifier, sim.PluralBridgeId) IN ({systemIdInClause})";
		var sourceBatchFilter = $"SELECT DISTINCT sim.ImportBatchId FROM dbo.pb_source_id_map AS sim WHERE {sourceIdMapFilter}";

		async Task<long> ReadCountAsync(string sql)
		{
			await using var command = new SqlCommand(sql, connection);

			for (var index = 0; index < systemIds.Length; index++)
			{
				command.Parameters.AddWithValue($"@SystemId{index}", systemIds[index]);
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
			await ReadCountAsync($"SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_custom_fields WHERE SystemId IN ({systemIdInClause});");
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
	/// Reads the first proof system from the validated database.
	/// Phase 2B uses this fixed proof context until Phase 3 replaces it with login/session mapping.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <returns>The selected proof system, or null when the proof database has no system row.</returns>
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
	/// Current System name and id token
	/// </summary>
	/// <param name="SystemId"></param>
	/// <param name="SystemName"></param>
	private sealed record ProofSystem(
		Guid SystemId, 
		string? SystemName);
}
