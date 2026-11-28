// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Messaging
{
	/// <summary>
	/// Test and host clock that moves only when <see cref="Advance"/> is called.
	/// </summary>
	/// <remarks>
	/// Time starts at zero. Focused tests advance it by exact tick counts and do not sleep.
	/// <see cref="Advance"/> uses an interlocked add so a concurrency test can move time
	/// while other operations read <see cref="MonotonicTime"/>. This type does not expire messages.
	/// The core applies the greater-than-or-equal TTL boundary to the samples it reads.
	/// </remarks>
	public sealed class ManualMessagingClock : IMessagingClock
	{
		private long _ticks;

		/// <summary>
		/// Current monotonic time. Starts at zero and never moves backward.
		/// </summary>
		public TimeSpan MonotonicTime => new TimeSpan(Interlocked.Read(ref _ticks));

		/// <summary>
		/// Moves the clock forward.
		/// </summary>
		/// <param name="amount">The non-negative amount of monotonic time to add.</param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is negative.</exception>
		public void Advance(TimeSpan amount)
		{
			if (amount < TimeSpan.Zero)
			{
				throw new ArgumentOutOfRangeException(nameof(amount), "Monotonic time cannot move backward.");
			}

			Interlocked.Add(ref _ticks, amount.Ticks);
		}
	}
}
