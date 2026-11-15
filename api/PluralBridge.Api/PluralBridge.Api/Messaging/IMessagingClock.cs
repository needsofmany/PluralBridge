// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Messaging
{
	/// <summary>
	/// Monotonic clock used to measure message lifetime in the in-memory core.
	/// </summary>
	/// <remarks>
	/// This slice does not read wall-clock time. The core samples <see cref="MonotonicTime"/>
	/// inside its accounting gate when it accepts a message, and that sample is the send instant.
	/// A message is expired when the later sample minus that send instant is greater than or equal to its TTL.
	/// The clock value must not move backward.
	/// </remarks>
	public interface IMessagingClock
	{
		/// <summary>
		/// Elapsed monotonic time from an arbitrary origin.
		/// </summary>
		/// <remarks>
		/// The origin is chosen by the implementation. Callers compare differences, not absolute dates.
		/// </remarks>
		TimeSpan MonotonicTime { get; }
	}
}
