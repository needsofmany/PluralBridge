// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net.Http.Json;
using System.Text;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountCodeDeliveryTests
{
	[Fact]
	public async Task CodeDelivery_RegistrationCode_IsWrittenToDevelopmentOutbox()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}code_delivery_registration_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Code Delivery Registration";

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

			var accountId = account.AccountId;

			var outbox = await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				email.ToUpperInvariant());

			Assert.NotNull(outbox);
			Assert.Equal(accountId, outbox.AccountId);

			Assert.Equal(AccountCodePurposes.RegistrationVerification, outbox.CodePurpose);
			Assert.Equal(AccountDestinationTypes.Email, outbox.DestinationType);
			Assert.Equal(email.ToUpperInvariant(), outbox.DestinationNormalized);
			Assert.Matches("^[0-9]{6}$", outbox.PlaintextCode);
			Assert.False(string.IsNullOrWhiteSpace(outbox.CorrelationId));
			Assert.Null(outbox.ConsumedForTestAtUtc);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task CodeDelivery_PasswordResetCode_IsWrittenToDevelopmentOutbox()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}code_delivery_password_reset_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Code Delivery Password Reset";

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

			Assert.NotNull(account);

			var accountId = account.AccountId;

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(accountId);

			var forgotPasswordRequest = new ForgotPasswordRequest(email);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(forgotPasswordResponse.IsSuccessStatusCode, await forgotPasswordResponse.Content.ReadAsStringAsync());

			var outbox = await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
				accountId,
				AccountCodePurposes.PasswordReset,
				AccountDestinationTypes.Email,
				email.ToUpperInvariant());

			Assert.NotNull(outbox);
			Assert.Equal(accountId, outbox.AccountId);
			Assert.Equal(AccountCodePurposes.PasswordReset, outbox.CodePurpose);
			Assert.Equal(AccountDestinationTypes.Email, outbox.DestinationType);
			Assert.Equal(email.ToUpperInvariant(), outbox.DestinationNormalized);
			Assert.Matches("^[0-9]{6}$", outbox.PlaintextCode);
			Assert.False(string.IsNullOrWhiteSpace(outbox.CorrelationId));
			Assert.Null(outbox.ConsumedForTestAtUtc);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task CodeDelivery_UsernameRecoveryCode_IsWrittenToDevelopmentOutbox()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}code_delivery_username_recovery_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Code Delivery Username Recovery";

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

			Assert.NotNull(account);

			var accountId = account.AccountId;

			var forgotUsernameRequest = new ForgotUsernameRequest(email);

			var forgotUsernameResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotUsername,
				forgotUsernameRequest);

			Assert.True(forgotUsernameResponse.IsSuccessStatusCode, await forgotUsernameResponse.Content.ReadAsStringAsync());

			var outbox = await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
				accountId,
				AccountCodePurposes.UsernameRecovery,
				AccountDestinationTypes.Email,
				email.ToUpperInvariant());

			Assert.NotNull(outbox);
			Assert.Equal(accountId, outbox.AccountId);
			Assert.Equal(AccountCodePurposes.UsernameRecovery, outbox.CodePurpose);
			Assert.Equal(AccountDestinationTypes.Email, outbox.DestinationType);
			Assert.Equal(email.ToUpperInvariant(), outbox.DestinationNormalized);
			Assert.Matches("^[0-9]{6}$", outbox.PlaintextCode);
			Assert.False(string.IsNullOrWhiteSpace(outbox.CorrelationId));
			Assert.Null(outbox.ConsumedForTestAtUtc);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task CodeDelivery_DoesNotExposeHashAsPlaintext()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}code_delivery_hash_plaintext_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Code Delivery Hash Plaintext";
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

			var accountCode = await AccountTestDatabase.ReadLatestAccountCodeStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var codeHash = accountCode?.CodeHash ?? throw new InvalidOperationException("Runtime test code hash was not created.");

			var outbox = await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var plaintextCode = outbox?.PlaintextCode ?? throw new InvalidOperationException("Runtime test delivery outbox row was not created.");

			Assert.Matches("^[0-9]{6}$", plaintextCode);
			Assert.NotEqual(plaintextCode, Convert.ToBase64String(codeHash));
			Assert.NotEqual(Encoding.UTF8.GetBytes(plaintextCode), codeHash);
			Assert.True(codeHash.Length > plaintextCode.Length);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task CodeDelivery_IsDevelopmentOrTestOnly()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}code_delivery_disabled_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Code Delivery Disabled";
		var normalizedEmail = email.ToUpperInvariant();

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory("Production");

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

			var accountCode = await AccountTestDatabase.ReadLatestAccountCodeStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			Assert.NotNull(accountCode);

			var outbox = await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			Assert.Null(outbox);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}
}
