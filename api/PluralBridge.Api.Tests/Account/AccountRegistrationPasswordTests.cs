// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

/// <summary>
/// Registration password rules. These tests do not open a database connection or send mail.
/// </summary>
public sealed class AccountRegistrationPasswordTests
{
	[Theory]
	[InlineData("abcdef1!")]
	[InlineData("abcdef1$")]
	[InlineData("abcdef1@")]
	[InlineData("abcdef1%")]
	[InlineData("abcdef1&")]
	[InlineData("abc de1!")]
	public void RegistrationPassword_AcceptsEightCharactersWhenEveryRequirementIsMet(string password)
	{
		Assert.Equal(8, password.Length);
		Assert.True(AccountRegistrationPassword.IsAccepted(password));
	}

	[Theory]
	[InlineData("abcde1!")]
	[InlineData("abcdefg!")]
	[InlineData("abcdefg1")]
	[InlineData(" abcde1!")]
	[InlineData("abcde1! ")]
	[InlineData("")]
	[InlineData("        ")]
	public void RegistrationPassword_RejectsWhenAnyRequirementFails(string password)
	{
		Assert.False(AccountRegistrationPassword.IsAccepted(password));
	}
}
