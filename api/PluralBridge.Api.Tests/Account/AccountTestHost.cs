// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

internal static class AccountTestHost
{
	internal static WebApplicationFactory<Program> CreateFactory()
	{
		return CreateFactory(AccountTestGlobals.RunModes.ModeDevelopment);
	}

	internal static WebApplicationFactory<Program> CreateFactory(string environmentName)
	{
		return CreateFactory(environmentName, null);
	}

	internal static WebApplicationFactory<Program> CreateFactory(Action<IServiceCollection> configureServices)
	{
		return CreateFactory(AccountTestGlobals.RunModes.ModeDevelopment, configureServices);
	}

	private static WebApplicationFactory<Program> CreateFactory(
		string environmentName,
		Action<IServiceCollection>? configureServices)
	{
		return new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseEnvironment(environmentName);

				builder.ConfigureAppConfiguration((_, configurationBuilder) =>
				{
					configurationBuilder.AddUserSecrets<Program>(optional: true);
				});

				builder.ConfigureServices(services =>
				{
					if (string.Equals(environmentName, AccountTestGlobals.RunModes.ModeDevelopment, StringComparison.OrdinalIgnoreCase))
					{
						services.RemoveAll<IAccountCodeDelivery>();
						services.AddScoped<IAccountCodeDelivery, DevelopmentAccountCodeDelivery>();
					}

					configureServices?.Invoke(services);
				});
			});
	}
}
