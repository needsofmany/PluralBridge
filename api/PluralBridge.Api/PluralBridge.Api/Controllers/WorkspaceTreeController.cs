// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PluralBridge.Api;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// The new opening tree. The existing plural-map route stays on <see cref="MeController"/>.
/// </summary>
/// <remarks>
/// GET builds one snapshot context and returns the navigation preview plus an opaque id.
/// DELETE releases that account's context. A repeated delete, a missing id, and another account's id
/// all return the same empty success after the caller is authenticated. They do not reveal which case it was.
/// The constructor receives the public opening service and context store.
/// Member details and member avatars use the retained context to identify the occurrence and the live profile procedure to authorize it.
/// This controller does not call the messaging core.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/me/workspace-tree")]
public sealed class WorkspaceTreeController(
	IConfiguration configuration,
	WorkspaceTreeService service,
	WorkspaceContextStore store,
	ILogger<WorkspaceTreeController> logger) : ControllerBase
{
	/// <summary>Builds and stores one workspace tree for the signed-in account.</summary>
	/// <param name="cancellationToken">Cancels the opening build before a context is published.</param>
	/// <returns>
	/// HTTP 200 with the context id, account id, and hierarchy preview.
	/// HTTP 401 when the session has no account. HTTP 500 when snapshot isolation or the render contract fails.
	/// </returns>
	[HttpGet]
	public async Task<IActionResult> Get(CancellationToken cancellationToken)
	{
		try
		{
			var response = await service.OpenAsync(User, cancellationToken);
			if (response is null)
			{
				return Unauthorized(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint = "/api/me/workspace-tree",
					canWrite = false,
					error = Globals.cantResolveAccess
				});
			}

			return Ok(response);
		}
		catch (WorkspaceSnapshotUnavailableException)
		{
			return Problem(
				title: "workspace_snapshot_unavailable",
				detail: "Snapshot isolation is not available. The workspace opening build was not retried.",
				statusCode: StatusCodes.Status500InternalServerError);
		}
		catch (WorkspaceContextTooLargeException exception)
		{
			return Problem(
				title: "workspace_context_too_large",
				detail: exception.Message,
				statusCode: StatusCodes.Status500InternalServerError);
		}
		catch (WorkspaceOpeningException exception)
		{
			logger.LogWarning("Workspace tree opening failed: {Reason}", exception.Message);
			return Problem(
				title: Globals.requestFailed,
				detail: "The workspace tree could not be read.",
				statusCode: StatusCodes.Status500InternalServerError);
		}
		catch (Exception) when (!cancellationToken.IsCancellationRequested)
		{
			return Problem(
				title: Globals.requestFailed,
				detail: "The workspace tree could not be read.",
				statusCode: StatusCodes.Status500InternalServerError);
		}
	}

	/// <summary>Releases one context for the signed-in account.</summary>
	/// <param name="contextId">The opaque id. The response does not say whether it matched.</param>
	/// <param name="cancellationToken">Cancels account resolution.</param>
	/// <returns>HTTP 204 after authentication. HTTP 401 when the session has no account.</returns>
	[HttpDelete("{contextId}")]
	public async Task<IActionResult> Release(string contextId, CancellationToken cancellationToken)
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
		await connection.OpenAsync(cancellationToken);
		var account = await AccessContextHelper.ResolveCurrentAccountAsync(connection, User, cancellationToken);
		if (account is null)
		{
			return Unauthorized(new
			{
				api = Globals.apiName,
				phase = Globals.projectPhase,
				endpoint = "/api/me/workspace-tree",
				canWrite = false,
				error = Globals.cantResolveAccess
			});
		}

		store.Release(account.AccountId, contextId);
		return NoContent();
	}

	/// <summary>
	/// Returns the direct member cards for one anchor occurrence in the retained context.
	/// </summary>
	/// <param name="contextId">The opaque context id.</param>
	/// <param name="occurrence">The selected anchor path. The server derives the anchor and branch from the context.</param>
	/// <param name="skip">Unique direct members to pass over. Omitted or blank is the first page. A negative or non-integer value is rejected.</param>
	/// <param name="cancellationToken">Cancels account resolution or the profile procedure.</param>
	/// <returns>
	/// HTTP 200 with at most 20 direct member cards and no avatar URL. A skip past the last member returns an empty array.
	/// HTTP 401 when the session has no account.
	/// HTTP 404 when the context is unavailable, including a context owned by another account.
	/// HTTP 400 when the path is not an anchor in that context or the skip is not zero or greater.
	/// Profile-procedure failures use the same status mapping as the anchor profile route.
	/// </returns>
	[HttpGet("{contextId}/member-details")]
	public async Task<IActionResult> MemberDetails(
		string contextId,
		[FromQuery] string? occurrence,
		CancellationToken cancellationToken,
		[FromQuery] string? skip = null)
	{
		const string endpoint = "/api/me/workspace-tree/member-details";
		var details = (WorkspaceMemberDetailService?)HttpContext.RequestServices.GetService(typeof(WorkspaceMemberDetailService));
		if (details is null)
		{
			return Problem(
				title: Globals.requestFailed,
				detail: "The member profiles could not be read.",
				statusCode: StatusCodes.Status500InternalServerError);
		}

		if (!WorkspaceMemberDetailService.TryParseMemberSkip(skip, out var memberSkip, out var skipError))
		{
			return BadRequest(new
			{
				api = Globals.apiName,
				phase = Globals.projectPhase,
				endpoint,
				canWrite = false,
				error = skipError
			});
		}

		try
		{
			var result = await details.LoadAsync(User, contextId, occurrence, cancellationToken, memberSkip);
			if (result is WorkspaceMemberDetailResult.MissingAccount)
			{
				return Unauthorized(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint,
					canWrite = false,
					error = Globals.cantResolveAccess
				});
			}

			if (result is WorkspaceMemberDetailResult.ContextUnavailable)
			{
				return NotFound(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint,
					canWrite = false,
					error = WorkspaceContextResults.Unavailable
				});
			}

			if (result is WorkspaceMemberDetailResult.OccurrenceRejected rejected)
			{
				return BadRequest(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint,
					canWrite = false,
					error = rejected.Error
				});
			}

			if (result is not WorkspaceMemberDetailResult.Ready ready)
			{
				return Problem(
					title: Globals.requestFailed,
					detail: "The member profiles could not be read.",
					statusCode: StatusCodes.Status500InternalServerError);
			}

			return Ok(ready.Cards.Select(card => new
			{
				memberId = card.MemberId,
				displayName = card.DisplayName,
				pronouns = card.Pronouns,
				description = card.Description,
				color = card.Color,
				visibilityTypeId = card.VisibilityTypeId,
				visibilityTypeCode = card.VisibilityTypeCode,
				visibilityTypeName = card.VisibilityTypeName,
				isFronting = card.IsFronting,
				placementVisibilityMode = card.PlacementVisibilityMode,
				placementVisibilityTypeId = card.PlacementVisibilityTypeId,
				placementPermissionMode = card.PlacementPermissionMode,
				placementPermissionCode = card.PlacementPermissionCode,
				resolvedVisibilityMode = card.ResolvedVisibilityMode,
				resolvedVisibilityTypeId = card.ResolvedVisibilityTypeId,
				resolvedPermissionMode = card.ResolvedPermissionMode,
				resolvedPermissionCode = card.ResolvedPermissionCode
			}));
		}
		catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
		{
			return MapProfileProcedureError(endpoint, exception.Number);
		}
		catch (AnchorProfileContractException exception) when (!cancellationToken.IsCancellationRequested)
		{
			return StatusCode(StatusCodes.Status500InternalServerError, new
			{
				api = Globals.apiName,
				phase = Globals.projectPhase,
				endpoint,
				canWrite = false,
				error = exception.Message
			});
		}
		catch (Exception) when (!cancellationToken.IsCancellationRequested)
		{
			logger.LogWarning("Workspace member details could not be read.");
			return Problem(
				title: Globals.requestFailed,
				detail: "The member profiles could not be read.",
				statusCode: StatusCodes.Status500InternalServerError);
		}
	}

	/// <summary>
	/// Returns the avatar bytes for one page of direct members.
	/// </summary>
	/// <param name="contextId">The opaque context id.</param>
	/// <param name="occurrence">The selected anchor path. The server derives the anchor and branch from the context.</param>
	/// <param name="skip">Unique direct members to pass over. Omitted or blank is the first page. A negative or non-integer value is rejected.</param>
	/// <param name="cancellationToken">Cancels account resolution, the profile procedure, or an image read. Cancellation is not turned into an image result.</param>
	/// <returns>
	/// HTTP 200 with the authorized page in preview order. Each item has memberId, contentType, and imageBase64. An authorized member with no image has null image fields. A skip past the last member returns an empty array after authorization.
	/// HTTP 401 when the session has no account.
	/// HTTP 404 when the context is unavailable, including a context owned by another account, or when avatar storage is not installed.
	/// HTTP 400 when the path is not an anchor in that context or the skip is not zero or greater.
	/// Profile-procedure failures use the same status mapping as member details.
	/// </returns>
	/// <remarks>A page member the profile procedure does not return is omitted. Source selection is not a request value.</remarks>
	[HttpGet("{contextId}/member-avatars")]
	public async Task<IActionResult> MemberAvatars(
		string contextId,
		[FromQuery] string? occurrence,
		CancellationToken cancellationToken,
		[FromQuery] string? skip = null)
	{
		const string endpoint = "/api/me/workspace-tree/member-avatars";
		var batch = (WorkspaceAvatarBatchService?)HttpContext.RequestServices.GetService(typeof(WorkspaceAvatarBatchService));
		if (batch is null)
		{
			return Problem(
				title: Globals.requestFailed,
				detail: "The member avatars could not be read.",
				statusCode: StatusCodes.Status500InternalServerError);
		}

		if (!WorkspaceMemberDetailService.TryParseMemberSkip(skip, out var memberSkip, out var skipError))
		{
			return BadRequest(new
			{
				api = Globals.apiName,
				phase = Globals.projectPhase,
				endpoint,
				canWrite = false,
				error = skipError
			});
		}

		try
		{
			var result = await batch.LoadAsync(User, contextId, occurrence, cancellationToken, memberSkip);
			if (result is WorkspaceAvatarBatchResult.MissingAccount)
			{
				return Unauthorized(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint,
					canWrite = false,
					error = Globals.cantResolveAccess
				});
			}

			if (result is WorkspaceAvatarBatchResult.ContextUnavailable)
			{
				return NotFound(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint,
					canWrite = false,
					error = WorkspaceContextResults.Unavailable
				});
			}

			if (result is WorkspaceAvatarBatchResult.OccurrenceRejected rejected)
			{
				return BadRequest(new
				{
					api = Globals.apiName,
					phase = Globals.projectPhase,
					endpoint,
					canWrite = false,
					error = rejected.Error
				});
			}

			if (result is WorkspaceAvatarBatchResult.AvatarStorageMissing)
			{
				return NotFound();
			}

			if (result is not WorkspaceAvatarBatchResult.Ready ready)
			{
				return Problem(
					title: Globals.requestFailed,
					detail: "The member avatars could not be read.",
					statusCode: StatusCodes.Status500InternalServerError);
			}

			return Ok(ready.Images.Select(image => new
			{
				memberId = image.MemberId,
				contentType = image.ContentType,
				imageBase64 = image.Bytes is null ? null : Convert.ToBase64String(image.Bytes)
			}));
		}
		catch (SqlException exception) when (!cancellationToken.IsCancellationRequested)
		{
			return MapProfileProcedureError(endpoint, exception.Number);
		}
		catch (AnchorProfileContractException exception) when (!cancellationToken.IsCancellationRequested)
		{
			return StatusCode(StatusCodes.Status500InternalServerError, new
			{
				api = Globals.apiName,
				phase = Globals.projectPhase,
				endpoint,
				canWrite = false,
				error = exception.Message
			});
		}
		catch (Exception) when (!cancellationToken.IsCancellationRequested)
		{
			logger.LogWarning("Workspace member avatars could not be read.");
			return Problem(
				title: Globals.requestFailed,
				detail: "The member avatars could not be read.",
				statusCode: StatusCodes.Status500InternalServerError);
		}
	}

	/// <summary>Maps a profile-procedure error number. The numbers match the anchor profile route.</summary>
	/// <param name="endpoint">The endpoint name returned to the client.</param>
	/// <param name="errorNumber">SQL error number. 8144 means the installed procedure has no branch parameter.</param>
	/// <returns>The status result for that number. 50006 and 50008 are forbidden without a body.</returns>
	private IActionResult MapProfileProcedureError(string endpoint, int errorNumber)
	{
		if (errorNumber is 50006 or 50008)
		{
			return Forbid();
		}

		var (status, error) = errorNumber switch
		{
			50001 => (StatusCodes.Status400BadRequest, "Anchor object type and anchor name are required."),
			50002 => (StatusCodes.Status400BadRequest, "Anchor object type must be system, group, or place."),
			50004 => (StatusCodes.Status404NotFound, "The requested anchor was not found."),
			50005 => (StatusCodes.Status400BadRequest, "More than one anchor matches that type and name."),
			50007 => (StatusCodes.Status400BadRequest, "More than one access membership matches this anchor."),
			50009 => (StatusCodes.Status409Conflict, "More than one membership row matches a selected member on this anchor."),
			8144 => (StatusCodes.Status500InternalServerError, "The profile procedure does not accept branch context. Install scripts/sqlserver/Members/008_profile_supporting_reads.sql before using this API."),
			_ => (StatusCodes.Status500InternalServerError, "The member profiles could not be read.")
		};

		if (status == StatusCodes.Status500InternalServerError && errorNumber is not 8144)
		{
			return Problem(
				title: Globals.requestFailed,
				detail: "The anchor member profiles could not be read.",
				statusCode: status);
		}

		return StatusCode(status, new
		{
			api = Globals.apiName,
			phase = Globals.projectPhase,
			endpoint,
			canWrite = false,
			error
		});
	}
}
