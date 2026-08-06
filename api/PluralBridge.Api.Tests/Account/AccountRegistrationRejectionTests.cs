using System.Net.Http.Json;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountRegistrationRejectionTests
{
	[Fact]
	public async Task Register_DuplicateUsername_IsRejected()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}duplicate_username_{testId}";
		var firstEmail = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var secondEmail = $"{username}_second@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Duplicate Username";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var firstRequest = new RegisterAccountRequest(
				username,
				firstEmail,
				displayName,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var firstResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				firstRequest);

			Assert.True(firstResponse.IsSuccessStatusCode, await firstResponse.Content.ReadAsStringAsync());

			var duplicateRequest = new RegisterAccountRequest(
				username,
				secondEmail,
				displayName,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var duplicateResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				duplicateRequest);

			Assert.False(duplicateResponse.IsSuccessStatusCode);

			var duplicateBody = await duplicateResponse.Content.ReadFromJsonAsync<AccountOperationResponse>();

			Assert.NotNull(duplicateBody);
			Assert.False(duplicateBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, duplicateBody.Outcome);
			Assert.Equal(AccountReasonCodes.DuplicateAccountIdentifier, duplicateBody.ReasonCode);

			var usernameCount = await AccountTestDatabase.CountAccountsByNormalizedUsernameAsync(username.ToUpperInvariant());
			var secondEmailCount = await AccountTestDatabase.CountAccountsByNormalizedEmailAsync(secondEmail.ToUpperInvariant());

			Assert.Equal(1, usernameCount);
			Assert.Equal(0, secondEmailCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task Register_DuplicateEmail_IsRejected()
	{
		var testId = Guid.NewGuid().ToString("N");
		var firstUsername = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}duplicate_email_first_{testId}";
		var secondUsername = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}duplicate_email_second_{testId}";
		var email = $"{firstUsername}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Duplicate Email";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var firstRequest = new RegisterAccountRequest(
				firstUsername,
				email,
				displayName,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var firstResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				firstRequest);

			Assert.True(firstResponse.IsSuccessStatusCode, await firstResponse.Content.ReadAsStringAsync());

			var duplicateRequest = new RegisterAccountRequest(
				secondUsername,
				email,
				displayName,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var duplicateResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				duplicateRequest);

			Assert.False(duplicateResponse.IsSuccessStatusCode);

			var duplicateBody = await duplicateResponse.Content.ReadFromJsonAsync<AccountOperationResponse>();

			Assert.NotNull(duplicateBody);
			Assert.False(duplicateBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, duplicateBody.Outcome);
			Assert.Equal(AccountReasonCodes.DuplicateAccountIdentifier, duplicateBody.ReasonCode);

			var emailCount = await AccountTestDatabase.CountAccountsByNormalizedEmailAsync(email.ToUpperInvariant());
			var secondUsernameCount = await AccountTestDatabase.CountAccountsByNormalizedUsernameAsync(secondUsername.ToUpperInvariant());

			Assert.Equal(1, emailCount);
			Assert.Equal(0, secondUsernameCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task Register_InvalidRequest_IsRejectedWithoutRows()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}invalid_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Invalid";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var invalidRequest = new RegisterAccountRequest(
				username,
				email,
				displayName,
				string.Empty);

			var response = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				invalidRequest);

			Assert.False(response.IsSuccessStatusCode);

			var responseBody = await response.Content.ReadFromJsonAsync<AccountOperationResponse>();

			Assert.NotNull(responseBody);
			Assert.False(responseBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, responseBody.Outcome);
			Assert.Equal(AccountReasonCodes.ValidationFailed, responseBody.ReasonCode);

			var usernameCount = await AccountTestDatabase.CountAccountsByNormalizedUsernameAsync(username.ToUpperInvariant());
			var emailCount = await AccountTestDatabase.CountAccountsByNormalizedEmailAsync(email.ToUpperInvariant());
			var codeCount = await AccountTestDatabase.CountRegistrationVerificationCodeRowsByDestinationAsync(email.ToUpperInvariant());

			Assert.Equal(0, usernameCount);
			Assert.Equal(0, emailCount);
			Assert.Equal(0, codeCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}
}
