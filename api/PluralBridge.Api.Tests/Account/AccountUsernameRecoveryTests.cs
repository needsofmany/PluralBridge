// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net.Http.Json;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountUsernameRecoveryTests
{
	[Fact]
	public async Task ForgotUsername_KnownEmail_ReturnsSuccessAndIssuesCode()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}forgot_username_known_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Forgot Username Known";
		var normalizedEmail = email.ToUpperInvariant();

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var registerRequest = new RegisterAccountRequest(
				username,
				email,
				displayName,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				registerRequest);

			Assert.True(registerResponse.IsSuccessStatusCode, await registerResponse.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username);
			var accountId = account?.AccountId ?? throw new InvalidOperationException("Runtime test account was not created.");

			var forgotUsernameRequest = new ForgotUsernameRequest(email);

			var forgotUsernameResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotUsername,
				forgotUsernameRequest);

			Assert.True(forgotUsernameResponse.IsSuccessStatusCode, await forgotUsernameResponse.Content.ReadAsStringAsync());

			var codeState = await AccountTestDatabase.ReadLatestCodeConsumptionStateAsync(
				accountId,
				AccountCodePurposes.UsernameRecovery,
				AccountDestinationTypes.Email,
				normalizedEmail);

			Assert.NotNull(codeState);
			Assert.Equal(accountId, codeState.AccountId);
			Assert.Equal(AccountCodePurposes.UsernameRecovery, codeState.CodePurpose);
			Assert.Equal(AccountDestinationTypes.Email, codeState.DestinationType);
			Assert.Equal(normalizedEmail, codeState.DestinationNormalized);
			Assert.Null(codeState.ConsumedAtUtc);
			Assert.Equal(0, codeState.AttemptCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ForgotUsername_UnknownEmail_ReturnsSuccessWithoutIssuingCode()
	{
		var testId = Guid.NewGuid().ToString("N");
		var email = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}forgot_username_unknown_{testId}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var normalizedEmail = email.ToUpperInvariant();

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var forgotUsernameRequest = new ForgotUsernameRequest(email);

			var forgotUsernameResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotUsername,
				forgotUsernameRequest);

			Assert.True(forgotUsernameResponse.IsSuccessStatusCode, await forgotUsernameResponse.Content.ReadAsStringAsync());

			var codeCount = await AccountTestDatabase.CountAccountCodeRowsByPurposeAndDestinationAsync(
				AccountCodePurposes.UsernameRecovery,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var outboxCount = await AccountTestDatabase.CountCodeDeliveryOutboxRowsByPurposeAndDestinationAsync(
				AccountCodePurposes.UsernameRecovery,
				AccountDestinationTypes.Email,
				normalizedEmail);

			Assert.Equal(0, codeCount);
			Assert.Equal(0, outboxCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ForgotUsername_InvalidRequest_IsRejectedWithoutIssuingCode()
	{
		var testId = Guid.NewGuid().ToString("N");
		var email = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}forgot_username_invalid_{testId}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var normalizedEmail = email.ToUpperInvariant();

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var forgotUsernameRequest = new ForgotUsernameRequest(string.Empty);

			var forgotUsernameResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotUsername,
				forgotUsernameRequest);

			Assert.False(forgotUsernameResponse.IsSuccessStatusCode);

			var codeCount = await AccountTestDatabase.CountAccountCodeRowsByPurposeAndDestinationAsync(
				AccountCodePurposes.UsernameRecovery,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var outboxCount = await AccountTestDatabase.CountCodeDeliveryOutboxRowsByPurposeAndDestinationAsync(
				AccountCodePurposes.UsernameRecovery,
				AccountDestinationTypes.Email,
				normalizedEmail);

			Assert.Equal(0, codeCount);
			Assert.Equal(0, outboxCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ForgotUsername_KnownAndUnknownEmails_ReturnSamePublicResponse()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}forgot_username_same_response_{testId}";
		var knownEmail = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var unknownEmail = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}forgot_username_same_response_unknown_{testId}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Forgot Username Same Response";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var registerRequest = new RegisterAccountRequest(
				username,
				knownEmail,
				displayName,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				registerRequest);

			Assert.True(registerResponse.IsSuccessStatusCode, await registerResponse.Content.ReadAsStringAsync());

			var knownResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotUsername,
				new ForgotUsernameRequest(knownEmail));

			var unknownResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotUsername,
				new ForgotUsernameRequest(unknownEmail));

			Assert.True(knownResponse.IsSuccessStatusCode, await knownResponse.Content.ReadAsStringAsync());
			Assert.True(unknownResponse.IsSuccessStatusCode, await unknownResponse.Content.ReadAsStringAsync());

			var knownBody = await knownResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
							?? throw new InvalidOperationException("Known-email forgot-username response body was not returned.");

			var unknownBody = await unknownResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
							  ?? throw new InvalidOperationException("Unknown-email forgot-username response body was not returned.");

			Assert.Equal(knownBody.Succeeded, unknownBody.Succeeded);
			Assert.Equal(knownBody.Outcome, unknownBody.Outcome);
			Assert.Equal(knownBody.ReasonCode, unknownBody.ReasonCode);
			Assert.Equal(knownBody.Message, unknownBody.Message);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}
}
