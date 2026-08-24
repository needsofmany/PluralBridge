// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace PluralBridge.Api.Tests.Account;

internal static class AccountTestHost
{
	internal static WebApplicationFactory<Program> CreateFactory()
	{
		return CreateFactory("Development");
	}

	internal static WebApplicationFactory<Program> CreateFactory(string environmentName)
	{
		return new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseEnvironment(environmentName);

				builder.ConfigureAppConfiguration((_, configurationBuilder) =>
				{
					configurationBuilder.AddUserSecrets<Program>(optional: true);
				});
			});
	}
}
