// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Account;

/// <summary>
/// Password rules shown on the Create account page.
/// The submitted password is compared as-is. This check does not trim it.
/// Sign-in, password reset, and password change do not use these rules.
/// </summary>
internal static class AccountRegistrationPassword
{
	internal const int MinimumLength = 8;

	/// <summary>
	/// A registration password needs 8 characters, one ASCII digit, one special character, and no leading or trailing whitespace.
	/// A special character is any character that is not an ASCII letter, ASCII digit, or whitespace, matching the page checklist.
	/// </summary>
	internal static bool IsAccepted(string? password)
	{
		if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
		{
			return false;
		}

		if (!string.Equals(password, password.Trim(), StringComparison.Ordinal))
		{
			return false;
		}

		var hasNumber = false;
		var hasSpecial = false;

		foreach (var character in password)
		{
			if (character is >= '0' and <= '9')
			{
				hasNumber = true;
				continue;
			}

			if (IsAsciiLetter(character) || char.IsWhiteSpace(character))
			{
				continue;
			}

			hasSpecial = true;
		}

		return hasNumber && hasSpecial;
	}

	private static bool IsAsciiLetter(char character)
	{
		return character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');
	}
}
