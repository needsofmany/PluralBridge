// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Account;

/// <summary>
/// Chooses the one account matched by a username or email.
/// Zero matches and two or more distinct account ids are no match. Duplicate rows for one id remain that one account.
/// The caller supplies the rows. This type does not query the database or prefer username over email.
/// </summary>
internal static class AccountIdentityMatches
{
	internal static bool TrySingle<T>(IReadOnlyList<T> matches, Func<T, Guid> accountId, out T single)
	{
		if (matches.Count == 0)
		{
			single = default!;
			return false;
		}

		var id = accountId(matches[0]);
		for (var index = 1; index < matches.Count; index++)
		{
			if (accountId(matches[index]) != id)
			{
				single = default!;
				return false;
			}
		}

		single = matches[0];
		return true;
	}
}
