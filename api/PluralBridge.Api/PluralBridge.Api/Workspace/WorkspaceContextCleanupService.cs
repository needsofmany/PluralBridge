// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Workspace;

/// <summary>
/// Removes idle workspace contexts without waiting for another workspace request.
/// </summary>
/// <remarks>
/// One periodic loop owns this pass. The store also ignores a second pass that arrives while the first is still inside its gate.
/// The interval is housekeeping. It is not a messaging TTL and it does not detect database freshness.
/// </remarks>
internal sealed class WorkspaceContextCleanupService(
	WorkspaceContextStore store,
	ILogger<WorkspaceContextCleanupService> logger) : BackgroundService
{
	/// <summary>The hosted pass interval. Overlapping passes are skipped by the store, not by a second timer.</summary>
	private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

	/// <summary>Reclaims expired contexts once a minute until the host stops.</summary>
	/// <param name="stoppingToken">The host shutdown signal.</param>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(Interval);
		try
		{
			while (await timer.WaitForNextTickAsync(stoppingToken))
			{
				store.ReclaimExpired();
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			logger.LogInformation("Workspace context cleanup stopped.");
		}
	}
}
