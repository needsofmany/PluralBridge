// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Account;

/// <summary>
/// Chooses where a successful password sign-in returns.
/// Only a same-site relative path is accepted. Anything else, including a protocol-relative URL, uses the application route.
/// </summary>
internal static class AuthReturnDestination
{
	internal static bool IsLocal(string? candidate)
	{
		if (string.IsNullOrWhiteSpace(candidate))
		{
			return false;
		}

		var value = candidate.Trim();
		if (!value.StartsWith('/'))
		{
			return false;
		}

		if (value.StartsWith("//", StringComparison.Ordinal) || value.StartsWith("/\\", StringComparison.Ordinal))
		{
			return false;
		}

		if (value.Contains('\\') || value.Contains("://", StringComparison.Ordinal))
		{
			return false;
		}

		var decoded = Uri.UnescapeDataString(value);
		if (decoded.StartsWith("//", StringComparison.Ordinal)
			|| decoded.StartsWith("/\\", StringComparison.Ordinal)
			|| decoded.Contains('\\')
			|| decoded.Contains("://", StringComparison.Ordinal))
		{
			return false;
		}

		return true;
	}

	/// <summary>Returns the candidate when it is local. Otherwise returns the application route.</summary>
	internal static string OrApp(string? candidate)
	{
		return IsLocal(candidate) ? candidate!.Trim() : Globals.browserAppRoute;
	}
}
