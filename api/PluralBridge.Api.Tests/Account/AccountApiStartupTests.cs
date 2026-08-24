// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.AspNetCore.Mvc.Testing;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountApiStartupTests
{
	[Fact]
	public async Task AccountApiHost_Starts()
	{
		await using var factory = new WebApplicationFactory<Program>();

		using var client = factory.CreateClient();

		var response = await client.GetAsync(AccountTestGlobals.Routes.Swagger);

		Assert.True((int)response.StatusCode < 500);
	}
}
