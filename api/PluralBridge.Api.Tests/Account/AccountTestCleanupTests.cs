// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountTestCleanupTests
{
	[Fact]
	public async Task AccountTestCleanup_RemovesRuntimeTestAccount()
	{
		var accountId = Guid.NewGuid();
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}cleanup_{accountId:N}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var normalizedUsername = username.ToUpperInvariant();
		var normalizedEmail = email.ToUpperInvariant();

		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		await InsertRuntimeTestAccountAsync(
			connection,
			accountId,
			username,
			normalizedUsername,
			email,
			normalizedEmail);

		await CleanupRuntimeTestAccountsAsync(connection);

		var remainingCount = await CountRuntimeTestAccountAsync(connection, accountId);

		Assert.Equal(0, remainingCount);
	}

	private static string GetConnectionString()
	{
		using var factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				builder.UseEnvironment("Development");

				builder.ConfigureAppConfiguration((_, configurationBuilder) =>
				{
					configurationBuilder.AddUserSecrets<Program>(optional: true);
				});
			});

		var configuration = factory.Services.GetRequiredService<IConfiguration>();

		return configuration.GetConnectionString(AccountTestGlobals.Database.DefaultConnectionName)
			?? throw new InvalidOperationException($"{AccountTestGlobals.Database.DefaultConnectionName} is not configured.");
	}

	private static async Task InsertRuntimeTestAccountAsync(
		SqlConnection connection,
		Guid accountId,
		string username,
		string normalizedUsername,
		string email,
		string normalizedEmail)
	{
		const string sql = """
			INSERT INTO dbo.pb_accounts
			(
				AccountId,
				Username,
				NormalizedUsername,
				Email,
				NormalizedEmail,
				DisplayName,
				AccountStatusId,
				IsEmailVerified,
				CreatedAtUtc,
				UpdatedAtUtc
			)
			VALUES
			(
				@AccountId,
				@Username,
				@NormalizedUsername,
				@Email,
				@NormalizedEmail,
				@DisplayName,
				2,
				0,
				SYSUTCDATETIME(),
				SYSUTCDATETIME()
			);
			""";

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@Username", username);
		command.Parameters.AddWithValue("@NormalizedUsername", normalizedUsername);
		command.Parameters.AddWithValue("@Email", email);
		command.Parameters.AddWithValue("@NormalizedEmail", normalizedEmail);
		command.Parameters.AddWithValue("@DisplayName", $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Cleanup");

		await command.ExecuteNonQueryAsync();
	}

	private static async Task CleanupRuntimeTestAccountsAsync(SqlConnection connection)
	{
		const string sql = """
			DECLARE @RuntimeAccounts TABLE
			(
				AccountId uniqueidentifier NOT NULL PRIMARY KEY
			);

			INSERT INTO @RuntimeAccounts
			(
				AccountId
			)
			SELECT
				AccountId
			FROM dbo.pb_accounts
			WHERE NormalizedUsername LIKE 'RUNTIME_TEST_%'
				OR NormalizedEmail LIKE '%@EXAMPLE.TEST';

			DELETE auditEvents
			FROM dbo.pb_account_audit_events auditEvents
			WHERE auditEvents.ActorAccountId IN
				(
					SELECT AccountId
					FROM @RuntimeAccounts
				)
				OR auditEvents.TargetAccountId IN
				(
					SELECT AccountId
					FROM @RuntimeAccounts
				);

			DELETE codes
			FROM dbo.pb_account_codes codes
			WHERE codes.AccountId IN
				(
					SELECT AccountId
					FROM @RuntimeAccounts
				);

			DELETE credentials
			FROM dbo.pb_account_credentials credentials
			WHERE credentials.AccountId IN
				(
					SELECT AccountId
					FROM @RuntimeAccounts
				);

			DELETE accounts
			FROM dbo.pb_accounts accounts
			WHERE accounts.AccountId IN
				(
					SELECT AccountId
					FROM @RuntimeAccounts
				);
			""";

		await using var command = new SqlCommand(sql, connection);

		await command.ExecuteNonQueryAsync();
	}

	private static async Task<int> CountRuntimeTestAccountAsync(SqlConnection connection, Guid accountId)
	{
		const string sql = """
			SELECT COUNT_BIG(1)
			FROM dbo.pb_accounts
			WHERE AccountId = @AccountId;
			""";

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);

		var result = await command.ExecuteScalarAsync();

		return Convert.ToInt32(result);
	}
}
