using System.Net.Http.Json;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountRegistrationVerificationTests
{
	[Fact]
	public async Task VerifyRegistration_ValidCode_ActivatesAccount()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}verify_registration_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Verify Registration";
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

			var pendingAccount = await AccountTestDatabase.ReadAccountStateAsync(username);
			var accountId = pendingAccount?.AccountId ?? throw new InvalidOperationException("Runtime test account was not created.");

			Assert.Equal(2, pendingAccount.AccountStatusId);
			Assert.False(pendingAccount.IsEmailVerified);

			var outbox = await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var verificationCode = outbox?.PlaintextCode ?? throw new InvalidOperationException("Runtime test verification code was not delivered.");

			var verifyRequest = new VerifyRegistrationRequest(
				email,
				verificationCode);

			var verifyResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.VerifyRegistration,
				verifyRequest);

			Assert.True(verifyResponse.IsSuccessStatusCode, await verifyResponse.Content.ReadAsStringAsync());

			var verifiedAccount = await AccountTestDatabase.ReadAccountStateAsync(username);

			Assert.NotNull(verifiedAccount);
			Assert.Equal(accountId, verifiedAccount.AccountId);
			Assert.Equal(1, verifiedAccount.AccountStatusId);
			Assert.True(verifiedAccount.IsEmailVerified);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task VerifyRegistration_ValidCode_ConsumesCode()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}verify_consumes_code_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Verify Consumes Code";
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

			var outbox = await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var verificationCode = outbox?.PlaintextCode ?? throw new InvalidOperationException("Runtime test verification code was not delivered.");

			var verifyRequest = new VerifyRegistrationRequest(
				email,
				verificationCode);

			var verifyResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.VerifyRegistration,
				verifyRequest);

			Assert.True(verifyResponse.IsSuccessStatusCode, await verifyResponse.Content.ReadAsStringAsync());

			var codeState = await AccountTestDatabase.ReadLatestCodeConsumptionStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var consumedCode = codeState ?? throw new InvalidOperationException("Runtime test verification code row was not found.");

			Assert.NotNull(consumedCode.ConsumedAtUtc);
			Assert.Equal(0, consumedCode.AttemptCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task VerifyRegistration_InvalidCode_IsRejectedWithoutActivatingAccount()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}verify_invalid_code_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Verify Invalid Code";
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

			var outbox = await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var deliveredCode = outbox?.PlaintextCode ?? throw new InvalidOperationException("Runtime test verification code was not delivered.");
			var invalidCode = deliveredCode == "000000" ? "111111" : "000000";

			var verifyRequest = new VerifyRegistrationRequest(
				email,
				invalidCode);

			var verifyResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.VerifyRegistration,
				verifyRequest);

			Assert.False(verifyResponse.IsSuccessStatusCode);

			var stillPendingAccount = await AccountTestDatabase.ReadAccountStateAsync(username);

			Assert.NotNull(stillPendingAccount);
			Assert.Equal(accountId, stillPendingAccount.AccountId);
			Assert.Equal(2, stillPendingAccount.AccountStatusId);
			Assert.False(stillPendingAccount.IsEmailVerified);

			var codeState = await AccountTestDatabase.ReadLatestCodeConsumptionStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var rejectedCode = codeState ?? throw new InvalidOperationException("Runtime test verification code row was not found.");

			Assert.Null(rejectedCode.ConsumedAtUtc);
			Assert.Equal(1, rejectedCode.AttemptCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task VerifyRegistration_ConsumedCode_IsRejected()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}verify_consumed_code_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Verify Consumed Code";
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

			var outbox = await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var verificationCode = outbox?.PlaintextCode ?? throw new InvalidOperationException("Runtime test verification code was not delivered.");

			var firstVerifyRequest = new VerifyRegistrationRequest(
				email,
				verificationCode);

			var firstVerifyResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.VerifyRegistration,
				firstVerifyRequest);

			Assert.True(firstVerifyResponse.IsSuccessStatusCode, await firstVerifyResponse.Content.ReadAsStringAsync());

			var secondVerifyRequest = new VerifyRegistrationRequest(
				email,
				verificationCode);

			var secondVerifyResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.VerifyRegistration,
				secondVerifyRequest);

			Assert.False(secondVerifyResponse.IsSuccessStatusCode);

			var verifiedAccount = await AccountTestDatabase.ReadAccountStateAsync(username);

			Assert.NotNull(verifiedAccount);
			Assert.Equal(accountId, verifiedAccount.AccountId);
			Assert.Equal(1, verifiedAccount.AccountStatusId);
			Assert.True(verifiedAccount.IsEmailVerified);

			var codeState = await AccountTestDatabase.ReadLatestCodeConsumptionStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var consumedCode = codeState ?? throw new InvalidOperationException("Runtime test verification code row was not found.");

			Assert.NotNull(consumedCode.ConsumedAtUtc);
			Assert.Equal(0, consumedCode.AttemptCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task VerifyRegistration_ExpiredCode_IsRejectedWithoutActivatingAccount()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}verify_expired_code_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Verify Expired Code";
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

			var outbox = await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var verificationCode = outbox?.PlaintextCode ?? throw new InvalidOperationException("Runtime test verification code was not delivered.");

			await AccountTestDatabase.ExpireLatestAccountCodeAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var verifyRequest = new VerifyRegistrationRequest(
				email,
				verificationCode);

			var verifyResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.VerifyRegistration,
				verifyRequest);

			Assert.False(verifyResponse.IsSuccessStatusCode);

			var stillPendingAccount = await AccountTestDatabase.ReadAccountStateAsync(username);

			Assert.NotNull(stillPendingAccount);
			Assert.Equal(accountId, stillPendingAccount.AccountId);
			Assert.Equal(2, stillPendingAccount.AccountStatusId);
			Assert.False(stillPendingAccount.IsEmailVerified);

			var codeState = await AccountTestDatabase.ReadLatestCodeConsumptionStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var expiredCode = codeState ?? throw new InvalidOperationException("Runtime test verification code row was not found.");

			Assert.Null(expiredCode.ConsumedAtUtc);
			Assert.Equal(0, expiredCode.AttemptCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task VerifyRegistration_MaxAttemptsCode_IsRejectedWithoutActivatingAccount()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}verify_max_attempts_code_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Verify Max Attempts Code";
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

			var outbox = await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var verificationCode = outbox?.PlaintextCode ?? throw new InvalidOperationException("Runtime test verification code was not delivered.");

			await AccountTestDatabase.MaxOutLatestAccountCodeAttemptsAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var verifyRequest = new VerifyRegistrationRequest(
				email,
				verificationCode);

			var verifyResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.VerifyRegistration,
				verifyRequest);

			Assert.False(verifyResponse.IsSuccessStatusCode);

			var stillPendingAccount = await AccountTestDatabase.ReadAccountStateAsync(username);
			var pendingAccount = stillPendingAccount ?? throw new InvalidOperationException("Runtime test account was not found.");

			Assert.Equal(accountId, pendingAccount.AccountId);
			Assert.Equal(2, pendingAccount.AccountStatusId);
			Assert.False(pendingAccount.IsEmailVerified);

			var codeState = await AccountTestDatabase.ReadLatestCodeConsumptionStateAsync(
				accountId,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				normalizedEmail);

			var maxedCode = codeState ?? throw new InvalidOperationException("Runtime test verification code row was not found.");

			Assert.Null(maxedCode.ConsumedAtUtc);
			Assert.Equal(maxedCode.MaxAttempts, maxedCode.AttemptCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task VerifyRegistration_InvalidRequest_IsRejectedWithoutCreatingAccount()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}verify_invalid_request_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var normalizedEmail = email.ToUpperInvariant();

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var verifyRequest = new VerifyRegistrationRequest(
				email,
				string.Empty);

			var verifyResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.VerifyRegistration,
				verifyRequest);

			Assert.False(verifyResponse.IsSuccessStatusCode);

			var accountCount = await AccountTestDatabase.CountAccountsByNormalizedEmailAsync(normalizedEmail);

			Assert.Equal(0, accountCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}
}
