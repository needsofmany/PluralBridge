// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Net.Http.Json;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

public sealed class AccountSafetyTests
{
	[Fact]
	public async Task UpdateProfile_UnauthenticatedRequest_ReturnsUnauthorized()
	{
		await using var factory =
			AccountTestHost.CreateFactory();

		using var client = factory.CreateClient();

		var response = await client.PutAsJsonAsync(
			AccountTestGlobals.Routes.Profile,
			new UpdateAccountProfileRequest(
				"Unauthorized Profile Update"));

		Assert.Equal(
			HttpStatusCode.Unauthorized,
			response.StatusCode);
	}

	[Fact]
	public async Task UpdateContact_UnauthenticatedRequest_ReturnsUnauthorized()
	{
		await using var factory =
			AccountTestHost.CreateFactory();

		using var client = factory.CreateClient();

		var response = await client.PutAsJsonAsync(
			AccountTestGlobals.Routes.Contact,
			new UpdateAccountContactRequest(
				"unauthorized@example.test"));

		Assert.Equal(
			HttpStatusCode.Unauthorized,
			response.StatusCode);
	}

	[Fact]
	public async Task VerifyContact_UnauthenticatedRequest_ReturnsUnauthorized()
	{
		await using var factory =
			AccountTestHost.CreateFactory();

		using var client = factory.CreateClient();

		var response = await client.PostAsJsonAsync(
			AccountTestGlobals.Routes.VerifyContact,
			new VerifyAccountContactRequest(
				"unauthorized@example.test",
				"123456"));

		Assert.Equal(
			HttpStatusCode.Unauthorized,
			response.StatusCode);
	}
}
