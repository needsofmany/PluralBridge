// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountTestDatabaseTests
{
	[Fact]
	public async Task AccountTestDatabase_CanConnect()
	{
		await using var factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseEnvironment("Development");

				builder.ConfigureAppConfiguration((_, configurationBuilder) =>
				{
					configurationBuilder.AddUserSecrets<Program>(optional: true);
				});
			});

		var configuration = factory.Services.GetRequiredService<IConfiguration>();
		var connectionString = configuration.GetConnectionString(AccountTestGlobals.Database.DefaultConnectionName);

		Assert.False(string.IsNullOrWhiteSpace(connectionString));

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		Assert.Equal(System.Data.ConnectionState.Open, connection.State);
	}
}
