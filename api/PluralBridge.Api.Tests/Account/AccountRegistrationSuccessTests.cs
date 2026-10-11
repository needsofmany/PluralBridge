// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net.Http.Json;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountRegistrationSuccessTests
{
	[Fact]
	public async Task Register_ValidRequest_CreatesPendingAccount()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}register_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Register";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var request = new RegisterAccountRequest(
				username,
				email,
				displayName,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var response = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				request);

			var responseText = await response.Content.ReadAsStringAsync();
			var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

			Assert.True(
				response.IsSuccessStatusCode,
				responseText);

			Assert.Equal("application/json", contentType);

			var responseBody = await response.Content.ReadFromJsonAsync<AccountOperationResponse>();

			Assert.NotNull(responseBody);
			Assert.True(responseBody.Succeeded);
			Assert.Equal(AccountOutcomes.Succeeded, responseBody.Outcome);
			Assert.Equal(AccountReasonCodes.None, responseBody.ReasonCode);

			var account = await AccountTestDatabase.ReadAccountStateAsync(username);

			Assert.NotNull(account);
			Assert.Equal(2, account.AccountStatusId);
			Assert.False(account.IsEmailVerified);
			Assert.Equal(username.ToUpperInvariant(), account.NormalizedUsername);
			Assert.Equal(email.ToUpperInvariant(), account.NormalizedEmail);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task Register_ValidRequest_CreatesCredential()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}credential_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Credential";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var request = new RegisterAccountRequest(
				username,
				email,
				displayName,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var response = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				request);

			Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username);

			Assert.NotNull(account);

			var credentialCount = await AccountTestDatabase.CountCredentialRowsAsync(account.AccountId);

			Assert.Equal(1, credentialCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task Register_ValidRequest_CreatesRegistrationVerificationCode()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}verification_code_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Verification Code";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var request = new RegisterAccountRequest(
				username,
				email,
				displayName,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var response = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				request);

			Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username);

			Assert.NotNull(account);

			var codeCount = await AccountTestDatabase.CountRegistrationVerificationCodeRowsAsync(
				account.AccountId,
				email.ToUpperInvariant());

			Assert.Equal(1, codeCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task Register_ValidRequest_WritesAuditRows()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}audit_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Audit";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var request = new RegisterAccountRequest(
				username,
				email,
				displayName,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var response = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				request);

			Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username);

			Assert.NotNull(account);

			var registrationCreatedCount = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.RegistrationCreated);

			var codeIssuedCount = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.CodeIssued);

			Assert.Equal(1, registrationCreatedCount);
			Assert.Equal(1, codeIssuedCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}
}
