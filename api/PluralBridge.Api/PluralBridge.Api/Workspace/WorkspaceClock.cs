// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Workspace;

/// <summary>
/// UTC clock for workspace-context idle time.
/// </summary>
/// <remarks>
/// This contract is public because <see cref="WorkspaceContextStore"/> has a public constructor.
/// The system and manual clocks remain internal. This is not the messaging clock and it does not measure database freshness.
/// Idle expiration is elapsed UTC time since the last account-bound use.
/// </remarks>
public interface IWorkspaceClock
{
	/// <summary>The current UTC instant.</summary>
	DateTime UtcNow { get; }
}

/// <summary>The process UTC clock used by the hosted cleanup and live requests.</summary>
internal sealed class SystemWorkspaceClock : IWorkspaceClock
{
	/// <summary>The current UTC instant.</summary>
	public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>A clock tests advance without waiting for wall time.</summary>
internal sealed class ManualWorkspaceClock : IWorkspaceClock
{
	/// <summary>The instant the next store operation observes.</summary>
	public DateTime UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	/// <summary>Moves the clock forward. A negative move is rejected.</summary>
	/// <param name="interval">The elapsed time to add.</param>
	public void Advance(TimeSpan interval)
	{
		if (interval < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(interval));
		}

		UtcNow += interval;
	}
}
