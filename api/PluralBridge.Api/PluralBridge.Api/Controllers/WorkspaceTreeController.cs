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
/// The constructor receives the public opening service and context store. It does not load member details or avatars and it does not call the messaging core.
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
}
