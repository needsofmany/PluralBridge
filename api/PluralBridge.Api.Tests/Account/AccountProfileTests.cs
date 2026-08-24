// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api.Account;
using System.Net.Http.Json;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountProfileTests
{
	[Fact]
	public async Task UpdateProfile_ValidDisplayName_UpdatesProfile()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ProfileUpdateUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName =
			AccountTestGlobals.TestAccounts.ProfileUpdateDisplayName;

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

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				new LoginRequest(
					email,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var profileResponse = await client.PutAsJsonAsync(
				AccountTestGlobals.Routes.Profile,
				new UpdateAccountProfileRequest(
					AccountTestGlobals.TestAccounts.UpdatedDisplayName));

			Assert.True(
				profileResponse.IsSuccessStatusCode,
				await profileResponse.Content.ReadAsStringAsync());

			var profileBody =
				await profileResponse.Content.ReadFromJsonAsync<AccountResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.ProfileResponseBodyWasNotReturned);

			Assert.Equal(
				AccountTestGlobals.TestAccounts.UpdatedDisplayName,
				profileBody.DisplayName);

			var persistedDisplayName =
				await AccountTestDatabase.ReadAccountDisplayNameAsync(account.AccountId);

			Assert.Equal(
				AccountTestGlobals.TestAccounts.UpdatedDisplayName,
				persistedDisplayName);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task UpdateProfile_BlankDisplayName_IsRejected()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ProfileInvalidUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName =
			AccountTestGlobals.TestAccounts.ProfileInvalidDisplayName;

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

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				new LoginRequest(
					email,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var profileResponse = await client.PutAsJsonAsync(
				AccountTestGlobals.Routes.Profile,
				new UpdateAccountProfileRequest("   "));

			Assert.False(profileResponse.IsSuccessStatusCode);

			var responseBody =
				await profileResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.ProfileResponseBodyWasNotReturned);

			Assert.False(responseBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, responseBody.Outcome);
			Assert.Equal(
				AccountReasonCodes.ValidationFailed,
				responseBody.ReasonCode);

			var persistedDisplayName =
				await AccountTestDatabase.ReadAccountDisplayNameAsync(account.AccountId);

			Assert.Equal(displayName, persistedDisplayName);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task UpdateProfile_AccountBecomesUnavailable_IsRejected()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ProfileUnavailableUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName =
			AccountTestGlobals.TestAccounts.ProfileUnavailableDisplayName;

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

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				new LoginRequest(
					email,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			await AccountTestDatabase.DisableRuntimeTestAccountAsync(account.AccountId);

			var profileResponse = await client.PutAsJsonAsync(
				AccountTestGlobals.Routes.Profile,
				new UpdateAccountProfileRequest(
					AccountTestGlobals.TestAccounts.UpdatedDisplayName));

			Assert.False(profileResponse.IsSuccessStatusCode);

			var responseBody =
				await profileResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.ProfileResponseBodyWasNotReturned);

			Assert.False(responseBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, responseBody.Outcome);
			Assert.Equal(
				AccountReasonCodes.AccountUnavailable,
				responseBody.ReasonCode);

			var persistedDisplayName =
				await AccountTestDatabase.ReadAccountDisplayNameAsync(account.AccountId);

			Assert.Equal(displayName, persistedDisplayName);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task UpdateProfile_Success_WritesAuditRow()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ProfileAuditUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName =
			AccountTestGlobals.TestAccounts.ProfileAuditDisplayName;

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

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				new LoginRequest(
					email,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var profileResponse = await client.PutAsJsonAsync(
				AccountTestGlobals.Routes.Profile,
				new UpdateAccountProfileRequest(
					AccountTestGlobals.TestAccounts.UpdatedDisplayName));

			Assert.True(
				profileResponse.IsSuccessStatusCode,
				await profileResponse.Content.ReadAsStringAsync());

			var auditRows = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.ProfileUpdated);

			Assert.Equal(1, auditRows);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}
}
