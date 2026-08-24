// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api.Account;
using System.Net.Http.Json;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountContactTests
{
	[Fact]
	public async Task UpdateContact_NewEmail_IssuesVerificationWithoutChangingEmail()
	{
		var testId = Guid.NewGuid().ToString("N");

		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ContactUpdateUsernameSegment}" +
			$"{testId}";

		var email =
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var newEmail =
			$"{username}.new@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var normalizedEmail = email.ToUpperInvariant();
		var normalizedNewEmail = newEmail.ToUpperInvariant();

		const string displayName =
			AccountTestGlobals.TestAccounts.ContactUpdateDisplayName;

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					username,
					email,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(
				account.AccountId);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				new LoginRequest(
					email,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var contactResponse = await client.PutAsJsonAsync(
				AccountTestGlobals.Routes.Contact,
				new UpdateAccountContactRequest(newEmail));

			Assert.True(
				contactResponse.IsSuccessStatusCode,
				await contactResponse.Content.ReadAsStringAsync());

			var responseBody =
				await contactResponse.Content
					.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics
						.ContactResponseBodyWasNotReturned);

			Assert.True(responseBody.Succeeded);
			Assert.Equal(
				AccountOutcomes.Succeeded,
				responseBody.Outcome);
			Assert.Equal(
				AccountReasonCodes.None,
				responseBody.ReasonCode);

			var accountAfterRequest =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			Assert.Equal(
				normalizedEmail,
				accountAfterRequest.NormalizedEmail);

			Assert.Equal(
				0,
				await AccountTestDatabase.CountAccountsByNormalizedEmailAsync(
					normalizedNewEmail));

			var verificationCode =
				await AccountTestDatabase.ReadLatestAccountCodeStateAsync(
					account.AccountId,
					AccountCodePurposes.ContactVerification,
					AccountDestinationTypes.Email,
					normalizedNewEmail);

			Assert.NotNull(verificationCode);

			var delivery =
				await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
					account.AccountId,
					AccountCodePurposes.ContactVerification,
					AccountDestinationTypes.Email,
					normalizedNewEmail);

			Assert.NotNull(delivery);
			Assert.False(string.IsNullOrWhiteSpace(delivery.PlaintextCode));
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task UpdateContact_BlankEmail_IsRejected()
	{
		var testId = Guid.NewGuid().ToString("N");

		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ContactUpdateUsernameSegment}" +
			$"blank_{testId}";

		var email =
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";

		const string displayName =
			AccountTestGlobals.TestAccounts.ContactUpdateDisplayName;

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					username,
					email,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(
				account.AccountId);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				new LoginRequest(
					email,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var contactResponse = await client.PutAsJsonAsync(
				AccountTestGlobals.Routes.Contact,
				new UpdateAccountContactRequest("   "));

			Assert.False(contactResponse.IsSuccessStatusCode);

			var responseBody =
				await contactResponse.Content
					.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics
						.ContactResponseBodyWasNotReturned);

			Assert.False(responseBody.Succeeded);
			Assert.Equal(
				AccountOutcomes.Rejected,
				responseBody.Outcome);
			Assert.Equal(
				AccountReasonCodes.ValidationFailed,
				responseBody.ReasonCode);

			var accountAfterRequest =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			Assert.Equal(
				account.NormalizedEmail,
				accountAfterRequest.NormalizedEmail);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task UpdateContact_AccountBecomesUnavailable_IsRejected()
	{
		var testId = Guid.NewGuid().ToString("N");

		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ContactUpdateUsernameSegment}" +
			$"unavailable_{testId}";

		var email =
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var newEmail =
			$"{username}.new@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var normalizedNewEmail = newEmail.ToUpperInvariant();

		const string displayName =
			AccountTestGlobals.TestAccounts.ContactUpdateDisplayName;

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					username,
					email,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(
				account.AccountId);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				new LoginRequest(
					email,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			await AccountTestDatabase.DisableRuntimeTestAccountAsync(
				account.AccountId);

			var contactResponse = await client.PutAsJsonAsync(
				AccountTestGlobals.Routes.Contact,
				new UpdateAccountContactRequest(newEmail));

			Assert.False(contactResponse.IsSuccessStatusCode);

			var responseBody =
				await contactResponse.Content
					.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics
						.ContactResponseBodyWasNotReturned);

			Assert.False(responseBody.Succeeded);
			Assert.Equal(
				AccountOutcomes.Rejected,
				responseBody.Outcome);
			Assert.Equal(
				AccountReasonCodes.AccountUnavailable,
				responseBody.ReasonCode);

			var codeRows =
				await AccountTestDatabase.CountAccountCodeRowsByPurposeAndDestinationAsync(
					AccountCodePurposes.ContactVerification,
					AccountDestinationTypes.Email,
					normalizedNewEmail);

			Assert.Equal(0, codeRows);

			var outboxRows =
				await AccountTestDatabase.CountCodeDeliveryOutboxRowsByPurposeAndDestinationAsync(
					AccountCodePurposes.ContactVerification,
					AccountDestinationTypes.Email,
					normalizedNewEmail);

			Assert.Equal(0, outboxRows);

			var auditRows =
				await AccountTestDatabase.CountAuditRowsAsync(
					account.AccountId,
					AccountAuditEvents.ContactRejected);

			Assert.Equal(1, auditRows);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task UpdateContact_EmailOwnedByAnotherAccount_IsRejected()
	{
		var testId = Guid.NewGuid().ToString("N");

		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ContactUpdateUsernameSegment}" +
			$"duplicate_source_{testId}";

		var email =
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var otherUsername =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ContactUpdateUsernameSegment}" +
			$"duplicate_target_{testId}";

		var otherEmail =
			$"{otherUsername}@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var normalizedOtherEmail = otherEmail.ToUpperInvariant();

		const string displayName =
			AccountTestGlobals.TestAccounts.ContactUpdateDisplayName;

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					username,
					email,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var otherRegisterResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					otherUsername,
					otherEmail,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				otherRegisterResponse.IsSuccessStatusCode,
				await otherRegisterResponse.Content.ReadAsStringAsync());

			var account =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			var otherAccount =
				await AccountTestDatabase.ReadAccountStateAsync(otherUsername)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(
				account.AccountId);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(
				otherAccount.AccountId);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				new LoginRequest(
					email,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var contactResponse = await client.PutAsJsonAsync(
				AccountTestGlobals.Routes.Contact,
				new UpdateAccountContactRequest(otherEmail));

			Assert.False(contactResponse.IsSuccessStatusCode);

			var responseBody =
				await contactResponse.Content
					.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics
						.ContactResponseBodyWasNotReturned);

			Assert.False(responseBody.Succeeded);
			Assert.Equal(
				AccountOutcomes.Rejected,
				responseBody.Outcome);
			Assert.Equal(
				AccountReasonCodes.DuplicateAccountIdentifier,
				responseBody.ReasonCode);

			var accountAfterRequest =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			Assert.Equal(
				account.NormalizedEmail,
				accountAfterRequest.NormalizedEmail);

			var codeRows =
				await AccountTestDatabase.CountAccountCodeRowsByPurposeAndDestinationAsync(
					AccountCodePurposes.ContactVerification,
					AccountDestinationTypes.Email,
					normalizedOtherEmail);

			Assert.Equal(0, codeRows);

			var auditRows =
				await AccountTestDatabase.CountAuditRowsAsync(
					account.AccountId,
					AccountAuditEvents.ContactRejected);

			Assert.Equal(1, auditRows);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task UpdateContact_CurrentEmail_IsRejected()
	{
		var testId = Guid.NewGuid().ToString("N");

		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ContactUpdateUsernameSegment}" +
			$"same_email_{testId}";

		var email =
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var normalizedEmail = email.ToUpperInvariant();

		const string displayName =
			AccountTestGlobals.TestAccounts.ContactUpdateDisplayName;

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					username,
					email,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(
				account.AccountId);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				new LoginRequest(
					email,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var contactResponse = await client.PutAsJsonAsync(
				AccountTestGlobals.Routes.Contact,
				new UpdateAccountContactRequest(email));

			Assert.False(contactResponse.IsSuccessStatusCode);

			var responseBody =
				await contactResponse.Content
					.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics
						.ContactResponseBodyWasNotReturned);

			Assert.False(responseBody.Succeeded);
			Assert.Equal(
				AccountOutcomes.Rejected,
				responseBody.Outcome);
			Assert.Equal(
				AccountReasonCodes.InvalidRequest,
				responseBody.ReasonCode);

			var accountAfterRequest =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			Assert.Equal(
				account.NormalizedEmail,
				accountAfterRequest.NormalizedEmail);

			var codeRows =
				await AccountTestDatabase.CountAccountCodeRowsByPurposeAndDestinationAsync(
					AccountCodePurposes.ContactVerification,
					AccountDestinationTypes.Email,
					normalizedEmail);

			Assert.Equal(0, codeRows);

			var auditRows =
				await AccountTestDatabase.CountAuditRowsAsync(
					account.AccountId,
					AccountAuditEvents.ContactRejected);

			Assert.Equal(1, auditRows);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task VerifyContact_ValidCode_UpdatesEmailAndConsumesCode()
	{
		var testId = Guid.NewGuid().ToString("N");

		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ContactUpdateUsernameSegment}" +
			$"verify_{testId}";

		var email =
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var newEmail =
			$"{username}.new@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var normalizedEmail = email.ToUpperInvariant();
		var normalizedNewEmail = newEmail.ToUpperInvariant();

		const string displayName =
			AccountTestGlobals.TestAccounts.ContactUpdateDisplayName;

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory =
				AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					username,
					email,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(
				account.AccountId);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				new LoginRequest(
					email,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var contactResponse = await client.PutAsJsonAsync(
				AccountTestGlobals.Routes.Contact,
				new UpdateAccountContactRequest(newEmail));

			Assert.True(
				contactResponse.IsSuccessStatusCode,
				await contactResponse.Content.ReadAsStringAsync());

			var delivery =
				await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
					account.AccountId,
					AccountCodePurposes.ContactVerification,
					AccountDestinationTypes.Email,
					normalizedNewEmail)
				?? throw new InvalidOperationException(
					"Contact verification code was not delivered.");

			var verificationResponse =
				await client.PostAsJsonAsync(
					AccountTestGlobals.Routes.VerifyContact,
					new VerifyAccountContactRequest(
						newEmail,
						delivery.PlaintextCode));

			Assert.True(
				verificationResponse.IsSuccessStatusCode,
				await verificationResponse.Content.ReadAsStringAsync());

			var body =
				await verificationResponse.Content
					.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics
						.ContactResponseBodyWasNotReturned);

			Assert.True(body.Succeeded);
			Assert.Equal(
				AccountOutcomes.Succeeded,
				body.Outcome);
			Assert.Equal(
				AccountReasonCodes.None,
				body.ReasonCode);

			var updatedAccount =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			Assert.Equal(
				normalizedNewEmail,
				updatedAccount.NormalizedEmail);

			Assert.True(updatedAccount.IsEmailVerified);

			Assert.Equal(
				0,
				await AccountTestDatabase.CountAccountsByNormalizedEmailAsync(
					normalizedEmail));

			Assert.Equal(
				1,
				await AccountTestDatabase.CountAccountsByNormalizedEmailAsync(
					normalizedNewEmail));

			var codeState =
				await AccountTestDatabase.ReadLatestCodeConsumptionStateAsync(
					account.AccountId,
					AccountCodePurposes.ContactVerification,
					AccountDestinationTypes.Email,
					normalizedNewEmail);

			Assert.NotNull(codeState);
			Assert.NotNull(codeState.ConsumedAtUtc);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task VerifyContact_InvalidCode_IsRejectedWithoutChangingEmail()
	{
		var testId = Guid.NewGuid().ToString("N");

		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ContactUpdateUsernameSegment}" +
			$"invalid_code_{testId}";

		var email =
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var newEmail =
			$"{username}.new@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var normalizedEmail = email.ToUpperInvariant();
		var normalizedNewEmail = newEmail.ToUpperInvariant();

		const string displayName =
			AccountTestGlobals.TestAccounts.ContactUpdateDisplayName;

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory =
				AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					username,
					email,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(
				account.AccountId);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				new LoginRequest(
					email,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var contactResponse = await client.PutAsJsonAsync(
				AccountTestGlobals.Routes.Contact,
				new UpdateAccountContactRequest(newEmail));

			Assert.True(
				contactResponse.IsSuccessStatusCode,
				await contactResponse.Content.ReadAsStringAsync());

			var verificationResponse =
				await client.PostAsJsonAsync(
					AccountTestGlobals.Routes.VerifyContact,
					new VerifyAccountContactRequest(
						newEmail,
						"000000"));

			Assert.False(
				verificationResponse.IsSuccessStatusCode);

			var body =
				await verificationResponse.Content
					.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics
						.ContactResponseBodyWasNotReturned);

			Assert.False(body.Succeeded);
			Assert.Equal(
				AccountOutcomes.Rejected,
				body.Outcome);
			Assert.Equal(
				AccountReasonCodes.InvalidCode,
				body.ReasonCode);

			var accountAfterVerification =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			Assert.Equal(
				normalizedEmail,
				accountAfterVerification.NormalizedEmail);

			Assert.Equal(
				1,
				await AccountTestDatabase.CountAccountsByNormalizedEmailAsync(
					normalizedEmail));

			Assert.Equal(
				0,
				await AccountTestDatabase.CountAccountsByNormalizedEmailAsync(
					normalizedNewEmail));
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task VerifyContact_ExpiredCode_IsRejectedWithoutChangingEmail()
	{
		var testId = Guid.NewGuid().ToString("N");

		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ContactUpdateUsernameSegment}" +
			$"expired_code_{testId}";

		var email =
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var newEmail =
			$"{username}.new@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var normalizedEmail = email.ToUpperInvariant();
		var normalizedNewEmail = newEmail.ToUpperInvariant();

		const string displayName =
			AccountTestGlobals.TestAccounts.ContactUpdateDisplayName;

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory =
				AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					username,
					email,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(
				account.AccountId);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				new LoginRequest(
					email,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var contactResponse = await client.PutAsJsonAsync(
				AccountTestGlobals.Routes.Contact,
				new UpdateAccountContactRequest(newEmail));

			Assert.True(
				contactResponse.IsSuccessStatusCode,
				await contactResponse.Content.ReadAsStringAsync());

			var delivery =
				await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
					account.AccountId,
					AccountCodePurposes.ContactVerification,
					AccountDestinationTypes.Email,
					normalizedNewEmail)
				?? throw new InvalidOperationException(
					"Contact verification code was not delivered.");

			await AccountTestDatabase.ExpireLatestAccountCodeAsync(
				account.AccountId,
				AccountCodePurposes.ContactVerification,
				AccountDestinationTypes.Email,
				normalizedNewEmail);

			var verificationResponse =
				await client.PostAsJsonAsync(
					AccountTestGlobals.Routes.VerifyContact,
					new VerifyAccountContactRequest(
						newEmail,
						delivery.PlaintextCode));

			Assert.False(
				verificationResponse.IsSuccessStatusCode);

			var body =
				await verificationResponse.Content
					.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics
						.ContactResponseBodyWasNotReturned);

			Assert.False(body.Succeeded);
			Assert.Equal(
				AccountOutcomes.Rejected,
				body.Outcome);
			Assert.Equal(
				AccountReasonCodes.ExpiredCode,
				body.ReasonCode);

			var accountAfterVerification =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			Assert.Equal(
				normalizedEmail,
				accountAfterVerification.NormalizedEmail);

			Assert.Equal(
				0,
				await AccountTestDatabase.CountAccountsByNormalizedEmailAsync(
					normalizedNewEmail));

			var codeState =
				await AccountTestDatabase.ReadLatestCodeConsumptionStateAsync(
					account.AccountId,
					AccountCodePurposes.ContactVerification,
					AccountDestinationTypes.Email,
					normalizedNewEmail);

			Assert.NotNull(codeState);
			Assert.Null(codeState.ConsumedAtUtc);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task VerifyContact_Success_WritesContactUpdatedAuditRow()
	{
		var testId = Guid.NewGuid().ToString("N");

		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ContactUpdateUsernameSegment}" +
			$"audit_{testId}";

		var email =
			$"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var newEmail =
			$"{username}.new@{AccountTestGlobals.TestAccounts.EmailDomain}";

		var normalizedNewEmail = newEmail.ToUpperInvariant();

		const string displayName =
			AccountTestGlobals.TestAccounts.ContactUpdateDisplayName;

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory =
				AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					username,
					email,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(
				account.AccountId);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				new LoginRequest(
					email,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var contactResponse = await client.PutAsJsonAsync(
				AccountTestGlobals.Routes.Contact,
				new UpdateAccountContactRequest(newEmail));

			Assert.True(
				contactResponse.IsSuccessStatusCode,
				await contactResponse.Content.ReadAsStringAsync());

			var delivery =
				await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
					account.AccountId,
					AccountCodePurposes.ContactVerification,
					AccountDestinationTypes.Email,
					normalizedNewEmail)
				?? throw new InvalidOperationException(
					"Contact verification code was not delivered.");

			var verificationResponse =
				await client.PostAsJsonAsync(
					AccountTestGlobals.Routes.VerifyContact,
					new VerifyAccountContactRequest(
						newEmail,
						delivery.PlaintextCode));

			Assert.True(
				verificationResponse.IsSuccessStatusCode,
				await verificationResponse.Content.ReadAsStringAsync());

			var auditRows =
				await AccountTestDatabase.CountAuditRowsAsync(
					account.AccountId,
					AccountAuditEvents.ContactUpdated);

			Assert.Equal(1, auditRows);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}
}
