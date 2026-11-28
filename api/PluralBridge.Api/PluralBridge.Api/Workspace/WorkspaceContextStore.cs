// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Workspace;

/// <summary>
/// In-process store for immutable workspace contexts.
/// </summary>
/// <remarks>
/// The class is public so the workspace controller can receive it. Publish and lookup stay internal
/// because their context types stay in this assembly. The lock covers accounting only. Callers finish SQL before they publish.
/// Each opening receives its own context. One tab's replacement does not remove another tab's context.
/// A context already returned to a caller remains readable after eviction. The next lookup fails.
/// Expiration uses elapsed UTC time. A longer idle setting does not clear the expired flag.
/// </remarks>
public sealed class WorkspaceContextStore
{
	private readonly object _gate = new();
	private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
	private readonly IWorkspaceClock _clock;
	private readonly IWorkspaceContextSettings _settings;
	private readonly ILogger<WorkspaceContextStore> _logger;
	private int _cleanupRunning;

	/// <summary>Creates the store.</summary>
	/// <param name="clock">The idle clock.</param>
	/// <param name="settings">The current timeout and budget.</param>
	/// <param name="logger">Receives counts and estimates. It must not receive context ids or tree text.</param>
	public WorkspaceContextStore(
		IWorkspaceClock clock,
		IWorkspaceContextSettings settings,
		ILogger<WorkspaceContextStore> logger)
	{
		_clock = clock;
		_settings = settings;
		_logger = logger;
	}

	/// <summary>How many contexts are currently stored.</summary>
	public int Count
	{
		get
		{
			lock (_gate)
			{
				return _entries.Count;
			}
		}
	}

	/// <summary>The current estimated total.</summary>
	public long EstimatedBytes
	{
		get
		{
			lock (_gate)
			{
				return _entries.Values.Sum(entry => entry.Context.EstimatedBytes);
			}
		}
	}

	/// <summary>
	/// Stores a complete context after the opening transaction has committed.
	/// </summary>
	/// <param name="context">The immutable context.</param>
	/// <returns>The same context when it was stored.</returns>
	/// <exception cref="WorkspaceContextTooLargeException">The context alone exceeds the budget.</exception>
	internal WorkspaceContext Publish(WorkspaceContext context)
	{
		var budget = _settings.BudgetBytes;
		if (context.EstimatedBytes > budget)
		{
			throw new WorkspaceContextTooLargeException(context.EstimatedBytes, budget);
		}

		lock (_gate)
		{
			ExpireDue(_clock.UtcNow, _settings.IdleTimeout);
			var timeout = _settings.IdleTimeout;
			_entries.Add(context.ContextId, new Entry(context, _clock.UtcNow, timeout));
			ReclaimLocked(budget, context.ContextId);
			LogAccounting("published");
			return context;
		}
	}

	/// <summary>
	/// Returns the context when the account matches and the idle time has not expired.
	/// </summary>
	/// <param name="accountId">The authenticated account.</param>
	/// <param name="contextId">The opaque id. A wrong-account id is unavailable.</param>
	/// <returns>The shared unavailable result, or the immutable copy. A successful lookup renews the idle period.</returns>
	internal WorkspaceContextAcquisition Acquire(Guid accountId, string contextId)
	{
		lock (_gate)
		{
			var now = _clock.UtcNow;
			var timeout = _settings.IdleTimeout;
			ExpireDue(now, timeout);
			if (!_entries.TryGetValue(contextId, out var entry) || entry.Expired)
			{
				return WorkspaceContextAcquisition.Unavailable();
			}

			if (entry.Context.AccountId != accountId)
			{
				return WorkspaceContextAcquisition.Unavailable();
			}

			entry.LastUsedUtc = now;
			entry.TimeoutAtUse = timeout;
			return WorkspaceContextAcquisition.Found(entry.Context);
		}
	}

	/// <summary>
	/// Removes the context when the account matches. A missing id and another account's id do nothing.
	/// </summary>
	/// <param name="accountId">The authenticated account.</param>
	/// <param name="contextId">The opaque id.</param>
	/// <returns>True when this account's context was removed. Callers that speak HTTP do not expose this distinction.</returns>
	public bool Release(Guid accountId, string contextId)
	{
		lock (_gate)
		{
			if (!_entries.TryGetValue(contextId, out var entry) || entry.Context.AccountId != accountId)
			{
				return false;
			}

			_entries.Remove(contextId);
			LogAccounting("released");
			return true;
		}
	}

	/// <summary>
	/// Removes contexts whose idle time has elapsed. A second caller while one pass is inside the gate does not start another pass.
	/// </summary>
	public void ReclaimExpired()
	{
		if (Interlocked.CompareExchange(ref _cleanupRunning, 1, 0) != 0)
		{
			return;
		}

		try
		{
			lock (_gate)
			{
				var removed = ExpireDue(_clock.UtcNow, _settings.IdleTimeout);
				if (removed > 0)
				{
					LogAccounting("reclaimed");
				}
			}
		}
		finally
		{
			Interlocked.Exchange(ref _cleanupRunning, 0);
		}
	}

	/// <summary>
	/// Drops other contexts until the estimate fits. Expired entries go first, then the least recently used.
	/// The context being published is not a victim. The caller already holds the store gate.
	/// </summary>
	/// <param name="budget">The current process budget, in bytes.</param>
	/// <param name="protectedId">The context id that this publish must keep.</param>
	private void ReclaimLocked(long budget, string protectedId)
	{
		var total = _entries.Values.Sum(entry => entry.Context.EstimatedBytes);
		if (total <= budget)
		{
			return;
		}

		var victims = _entries.Values
			.Where(entry => !string.Equals(entry.Context.ContextId, protectedId, StringComparison.Ordinal))
			.OrderByDescending(entry => entry.Expired)
			.ThenBy(entry => entry.LastUsedUtc)
			.ToList();
		foreach (var victim in victims)
		{
			if (total <= budget)
			{
				break;
			}

			total -= victim.Context.EstimatedBytes;
			_entries.Remove(victim.Context.ContextId);
		}
	}

	/// <summary>
	/// Removes entries whose sticky deadline has passed. Removal is the only idle cleanup.
	/// </summary>
	/// <param name="now">The clock time used for this pass.</param>
	/// <param name="currentTimeout">The timeout currently configured.</param>
	/// <returns>How many entries were removed.</returns>
	private int ExpireDue(DateTime now, TimeSpan currentTimeout)
	{
		var removed = 0;
		foreach (var entry in _entries.Values.ToArray())
		{
			if (!IsExpired(entry, now, currentTimeout))
			{
				continue;
			}

			entry.Expired = true;
			_entries.Remove(entry.Context.ContextId);
			removed++;
		}

		return removed;
	}

	/// <summary>
	/// An entry stays expired once marked. Otherwise the deadline is the shorter of the timeout
	/// captured at last use and the timeout configured now.
	/// </summary>
	/// <param name="entry">The stored context.</param>
	/// <param name="now">The clock time used for this check.</param>
	/// <param name="currentTimeout">The timeout currently configured.</param>
	/// <returns>True when the entry must not be acquired.</returns>
	private static bool IsExpired(Entry entry, DateTime now, TimeSpan currentTimeout)
	{
		if (entry.Expired)
		{
			return true;
		}

		var effective = entry.TimeoutAtUse < currentTimeout ? entry.TimeoutAtUse : currentTimeout;
		return now - entry.LastUsedUtc >= effective;
	}

	/// <summary>
	/// Records the action, the stored count, and the estimated bytes. The message has no context id and no tree text.
	/// </summary>
	/// <param name="action">published, released, or reclaimed.</param>
	private void LogAccounting(string action)
	{
		_logger.LogInformation(
			"Workspace context {Action}. Count {Count}. EstimatedBytes {EstimatedBytes}.",
			action,
			_entries.Count,
			_entries.Values.Sum(entry => entry.Context.EstimatedBytes));
	}

	/// <summary>
	/// One stored context. <see cref="TimeoutAtUse"/> is the idle timeout at the last successful use.
	/// A later longer setting does not replace it, and <see cref="Expired"/> is not cleared by a settings change.
	/// </summary>
	private sealed class Entry
	{
		public Entry(WorkspaceContext context, DateTime lastUsedUtc, TimeSpan timeoutAtUse)
		{
			Context = context;
			LastUsedUtc = lastUsedUtc;
			TimeoutAtUse = timeoutAtUse;
		}

		public WorkspaceContext Context { get; }

		public DateTime LastUsedUtc { get; set; }

		public TimeSpan TimeoutAtUse { get; set; }

		public bool Expired { get; set; }
	}
}
