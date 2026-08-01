using PluralBridge.Api.Account;
using System.Net.Http.Json;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountForgotPasswordTests
{
	[Fact]
	public async Task ForgotPassword_KnownUsername_ReturnsGenericSuccess()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}forgot_password_known_username_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Forgot Password Known Username";

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
			var accountId = account?.AccountId ?? throw new InvalidOperationException(TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(accountId);

			var forgotPasswordRequest = new ForgotPasswordRequest(username);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(forgotPasswordResponse.IsSuccessStatusCode, await forgotPasswordResponse.Content.ReadAsStringAsync());

			var body = await forgotPasswordResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
					   ?? throw new InvalidOperationException("Forgot-password response body was not returned.");

			Assert.True(body.Succeeded);
			Assert.Equal(AccountOutcomes.Succeeded, body.Outcome);
			Assert.Equal(AccountReasonCodes.None, body.ReasonCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ForgotPassword_KnownEmail_ReturnsGenericSuccess()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}forgot_password_known_email_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Forgot Password Known Email";

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
			var accountId = account?.AccountId ?? throw new InvalidOperationException(TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(accountId);

			var forgotPasswordRequest = new ForgotPasswordRequest(email);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(forgotPasswordResponse.IsSuccessStatusCode, await forgotPasswordResponse.Content.ReadAsStringAsync());

			var body = await forgotPasswordResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
					   ?? throw new InvalidOperationException("Forgot-password response body was not returned.");

			Assert.True(body.Succeeded);
			Assert.Equal(AccountOutcomes.Succeeded, body.Outcome);
			Assert.Equal(AccountReasonCodes.None, body.ReasonCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ForgotPassword_KnownAccount_CreatesResetCode()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}forgot_password_creates_reset_code_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Forgot Password Creates Reset Code";

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

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
						  ?? throw new InvalidOperationException(TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var forgotPasswordRequest = new ForgotPasswordRequest(username);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(forgotPasswordResponse.IsSuccessStatusCode, await forgotPasswordResponse.Content.ReadAsStringAsync());

			var resetCode = await AccountTestDatabase.ReadLatestAccountCodeStateAsync(
				account.AccountId,
				AccountCodePurposes.PasswordReset,
				AccountDestinationTypes.Email,
				account.NormalizedEmail);

			Assert.NotNull(resetCode);
			Assert.Equal(account.AccountId, resetCode.AccountId);
			Assert.Equal(AccountCodePurposes.PasswordReset, resetCode.CodePurpose);
			Assert.Equal(AccountDestinationTypes.Email, resetCode.DestinationType);
			Assert.Equal(account.NormalizedEmail, resetCode.DestinationNormalized);
			Assert.NotEmpty(resetCode.CodeHash);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ForgotPassword_KnownAccount_WritesAuditRows()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}forgot_password_writes_audit_rows_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Forgot Password Writes Audit Rows";

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

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
						  ?? throw new InvalidOperationException(TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var requestedBefore = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.PasswordResetRequested);

			var issuedBefore = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.PasswordResetIssued);

			var codeIssuedBefore = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.CodeIssued);

			var forgotPasswordRequest = new ForgotPasswordRequest(username);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(forgotPasswordResponse.IsSuccessStatusCode, await forgotPasswordResponse.Content.ReadAsStringAsync());

			var requestedAfter = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.PasswordResetRequested);

			var issuedAfter = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.PasswordResetIssued);

			var codeIssuedAfter = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.CodeIssued);

			Assert.Equal(requestedBefore + 1, requestedAfter);
			Assert.Equal(issuedBefore + 1, issuedAfter);
			Assert.Equal(codeIssuedBefore + 1, codeIssuedAfter);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ForgotPassword_UnknownIdentifier_ReturnsSameGenericSuccess()
	{
		var testId = Guid.NewGuid().ToString("N");
		var unknownIdentifier = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}forgot_password_unknown_{testId}";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var forgotPasswordRequest = new ForgotPasswordRequest(unknownIdentifier);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(forgotPasswordResponse.IsSuccessStatusCode, await forgotPasswordResponse.Content.ReadAsStringAsync());

			var body = await forgotPasswordResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
			           ?? throw new InvalidOperationException("Forgot-password response body was not returned.");

			Assert.True(body.Succeeded);
			Assert.Equal(AccountOutcomes.Succeeded, body.Outcome);
			Assert.Equal(AccountReasonCodes.None, body.ReasonCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ForgotPassword_UnknownIdentifier_DoesNotCreateCode()
	{
		var testId = Guid.NewGuid().ToString("N");
		var unknownEmail = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}forgot_password_no_code_{testId}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var normalizedEmail = unknownEmail.ToUpperInvariant();

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var codeRowsBefore = await AccountTestDatabase.CountAccountCodeRowsByPurposeAndDestinationAsync(
				AccountCodePurposes.PasswordReset,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var forgotPasswordRequest = new ForgotPasswordRequest(unknownEmail);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(forgotPasswordResponse.IsSuccessStatusCode, await forgotPasswordResponse.Content.ReadAsStringAsync());

			var codeRowsAfter = await AccountTestDatabase.CountAccountCodeRowsByPurposeAndDestinationAsync(
				AccountCodePurposes.PasswordReset,
				AccountDestinationTypes.Email,
				normalizedEmail);

			Assert.Equal(codeRowsBefore, codeRowsAfter);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ForgotPassword_InvalidRequest_IsRejected()
	{
		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var forgotPasswordRequest = new ForgotPasswordRequest(string.Empty);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.False(forgotPasswordResponse.IsSuccessStatusCode);

			var body = await forgotPasswordResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
			           ?? throw new InvalidOperationException("Forgot-password response body was not returned.");

			Assert.False(body.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, body.Outcome);
			Assert.Equal(AccountReasonCodes.ValidationFailed, body.ReasonCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}
}
