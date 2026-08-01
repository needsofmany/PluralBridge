using PluralBridge.Api.Account;
using System.Linq;
using System.Net.Http.Json;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountResetPasswordTests
{
	[Fact]
	public async Task ResetPassword_ValidCode_ChangesPassword()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ResetPasswordValidCodeUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = AccountTestGlobals.TestAccounts.ResetPasswordValidCodeDisplayName;

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

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
						  ?? throw new InvalidOperationException(
							  TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var forgotPasswordRequest = new ForgotPasswordRequest(email);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(
				forgotPasswordResponse.IsSuccessStatusCode,
				await forgotPasswordResponse.Content.ReadAsStringAsync());

			var resetCodeDelivery =
				await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
					account.AccountId,
					AccountCodePurposes.PasswordReset,
					AccountDestinationTypes.Email,
					account.NormalizedEmail)
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetCodeDeliveryWasNotFound);

			var resetPasswordRequest = new ResetPasswordRequest(
				email,
				resetCodeDelivery.PlaintextCode,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var resetPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ResetPassword,
				resetPasswordRequest);

			Assert.True(
				resetPasswordResponse.IsSuccessStatusCode,
				await resetPasswordResponse.Content.ReadAsStringAsync());

			var resetBody =
				await resetPasswordResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetResponseBodyWasNotReturned);

			Assert.True(resetBody.Succeeded);
			Assert.Equal(AccountOutcomes.Succeeded, resetBody.Outcome);
			Assert.Equal(AccountReasonCodes.None, resetBody.ReasonCode);

			var loginRequest = new LoginRequest(
				email,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>()
							?? throw new InvalidOperationException(
								AccountTestGlobals.Diagnostics.LoginResponseBodyWasNotReturned);

			Assert.True(loginBody.Succeeded);
			Assert.NotNull(loginBody.Account);
			Assert.Equal(account.AccountId, loginBody.Account.AccountId);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ResetPassword_ValidCode_ConsumesResetCode()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ResetPasswordConsumesCodeUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = AccountTestGlobals.TestAccounts.ResetPasswordConsumesCodeDisplayName;

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

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
						  ?? throw new InvalidOperationException(
							  TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var forgotPasswordRequest = new ForgotPasswordRequest(email);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(
				forgotPasswordResponse.IsSuccessStatusCode,
				await forgotPasswordResponse.Content.ReadAsStringAsync());

			var resetCodeDelivery =
				await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
					account.AccountId,
					AccountCodePurposes.PasswordReset,
					AccountDestinationTypes.Email,
					account.NormalizedEmail)
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetCodeDeliveryWasNotFound);

			var resetPasswordRequest = new ResetPasswordRequest(
				email,
				resetCodeDelivery.PlaintextCode,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var resetPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ResetPassword,
				resetPasswordRequest);

			Assert.True(
				resetPasswordResponse.IsSuccessStatusCode,
				await resetPasswordResponse.Content.ReadAsStringAsync());

			var codeState = await AccountTestDatabase.ReadLatestCodeConsumptionStateAsync(
				account.AccountId,
				AccountCodePurposes.PasswordReset,
				AccountDestinationTypes.Email,
				account.NormalizedEmail);

			Assert.NotNull(codeState);
			Assert.NotNull(codeState.ConsumedAtUtc);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ResetPassword_InvalidCode_IsRejected()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ResetPasswordInvalidCodeUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = AccountTestGlobals.TestAccounts.ResetPasswordInvalidCodeDisplayName;

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

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
						  ?? throw new InvalidOperationException(
							  TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var forgotPasswordRequest = new ForgotPasswordRequest(email);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(
				forgotPasswordResponse.IsSuccessStatusCode,
				await forgotPasswordResponse.Content.ReadAsStringAsync());

			var resetCodeDelivery =
				await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
					account.AccountId,
					AccountCodePurposes.PasswordReset,
					AccountDestinationTypes.Email,
					account.NormalizedEmail)
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetCodeDeliveryWasNotFound);

			var invalidCodeCharacters = resetCodeDelivery.PlaintextCode.ToCharArray();

			invalidCodeCharacters[0] =
				invalidCodeCharacters[0] == '0'
					? '1'
					: '0';

			var invalidCode = new string(invalidCodeCharacters);

			var resetPasswordRequest = new ResetPasswordRequest(
				email,
				invalidCode,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var resetPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ResetPassword,
				resetPasswordRequest);

			Assert.False(resetPasswordResponse.IsSuccessStatusCode);

			var resetBody =
				await resetPasswordResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetResponseBodyWasNotReturned);

			Assert.False(resetBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, resetBody.Outcome);
			Assert.Equal(AccountReasonCodes.InvalidCode, resetBody.ReasonCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ResetPassword_ExpiredCode_IsRejected()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ResetPasswordExpiredCodeUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = AccountTestGlobals.TestAccounts.ResetPasswordExpiredCodeDisplayName;

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

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
						  ?? throw new InvalidOperationException(
							  TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var forgotPasswordRequest = new ForgotPasswordRequest(email);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(
				forgotPasswordResponse.IsSuccessStatusCode,
				await forgotPasswordResponse.Content.ReadAsStringAsync());

			var resetCodeDelivery =
				await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
					account.AccountId,
					AccountCodePurposes.PasswordReset,
					AccountDestinationTypes.Email,
					account.NormalizedEmail)
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetCodeDeliveryWasNotFound);

			await AccountTestDatabase.ExpireLatestAccountCodeAsync(
				account.AccountId,
				AccountCodePurposes.PasswordReset,
				AccountDestinationTypes.Email,
				account.NormalizedEmail);

			var resetPasswordRequest = new ResetPasswordRequest(
				email,
				resetCodeDelivery.PlaintextCode,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var resetPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ResetPassword,
				resetPasswordRequest);

			Assert.False(resetPasswordResponse.IsSuccessStatusCode);

			var resetBody =
				await resetPasswordResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetResponseBodyWasNotReturned);

			Assert.False(resetBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, resetBody.Outcome);
			Assert.Equal(AccountReasonCodes.ExpiredCode, resetBody.ReasonCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ResetPassword_ConsumedCode_IsRejected()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ResetPasswordConsumedCodeUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = AccountTestGlobals.TestAccounts.ResetPasswordConsumedCodeDisplayName;

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

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
						  ?? throw new InvalidOperationException(
							  TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var forgotPasswordRequest = new ForgotPasswordRequest(email);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(
				forgotPasswordResponse.IsSuccessStatusCode,
				await forgotPasswordResponse.Content.ReadAsStringAsync());

			var resetCodeDelivery =
				await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
					account.AccountId,
					AccountCodePurposes.PasswordReset,
					AccountDestinationTypes.Email,
					account.NormalizedEmail)
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetCodeDeliveryWasNotFound);

			var resetPasswordRequest = new ResetPasswordRequest(
				email,
				resetCodeDelivery.PlaintextCode,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var firstResetResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ResetPassword,
				resetPasswordRequest);

			Assert.True(
				firstResetResponse.IsSuccessStatusCode,
				await firstResetResponse.Content.ReadAsStringAsync());

			var secondResetResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ResetPassword,
				resetPasswordRequest);

			Assert.False(secondResetResponse.IsSuccessStatusCode);

			var secondResetBody =
				await secondResetResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetResponseBodyWasNotReturned);

			Assert.False(secondResetBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, secondResetBody.Outcome);
			Assert.Equal(AccountReasonCodes.ConsumedCode, secondResetBody.ReasonCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ResetPassword_AttemptLimit_IsRejected()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ResetPasswordAttemptLimitUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = AccountTestGlobals.TestAccounts.ResetPasswordAttemptLimitDisplayName;

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

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
						  ?? throw new InvalidOperationException(
							  TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var forgotPasswordRequest = new ForgotPasswordRequest(email);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(
				forgotPasswordResponse.IsSuccessStatusCode,
				await forgotPasswordResponse.Content.ReadAsStringAsync());

			var resetCodeDelivery =
				await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
					account.AccountId,
					AccountCodePurposes.PasswordReset,
					AccountDestinationTypes.Email,
					account.NormalizedEmail)
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetCodeDeliveryWasNotFound);

			await AccountTestDatabase.MaxOutLatestAccountCodeAttemptsAsync(
				account.AccountId,
				AccountCodePurposes.PasswordReset,
				AccountDestinationTypes.Email,
				account.NormalizedEmail);

			var resetPasswordRequest = new ResetPasswordRequest(
				email,
				resetCodeDelivery.PlaintextCode,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var resetPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ResetPassword,
				resetPasswordRequest);

			Assert.False(resetPasswordResponse.IsSuccessStatusCode);

			var resetBody =
				await resetPasswordResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetResponseBodyWasNotReturned);

			Assert.False(resetBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, resetBody.Outcome);
			Assert.Equal(AccountReasonCodes.RateLimited, resetBody.ReasonCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ResetPassword_InvalidRequest_IsRejected()
	{
		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var resetPasswordRequest = new ResetPasswordRequest(
				string.Empty,
				string.Empty,
				string.Empty);

			var resetPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ResetPassword,
				resetPasswordRequest);

			Assert.False(resetPasswordResponse.IsSuccessStatusCode);

			var resetBody =
				await resetPasswordResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetResponseBodyWasNotReturned);

			Assert.False(resetBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, resetBody.Outcome);
			Assert.Equal(AccountReasonCodes.ValidationFailed, resetBody.ReasonCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ResetPassword_Success_OldPasswordIsRejected()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ResetPasswordOldPasswordUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = AccountTestGlobals.TestAccounts.ResetPasswordOldPasswordDisplayName;

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

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
						  ?? throw new InvalidOperationException(
							  TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var forgotPasswordRequest = new ForgotPasswordRequest(email);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(
				forgotPasswordResponse.IsSuccessStatusCode,
				await forgotPasswordResponse.Content.ReadAsStringAsync());

			var resetCodeDelivery =
				await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
					account.AccountId,
					AccountCodePurposes.PasswordReset,
					AccountDestinationTypes.Email,
					account.NormalizedEmail)
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetCodeDeliveryWasNotFound);

			var resetPasswordRequest = new ResetPasswordRequest(
				email,
				resetCodeDelivery.PlaintextCode,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var resetPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ResetPassword,
				resetPasswordRequest);

			Assert.True(
				resetPasswordResponse.IsSuccessStatusCode,
				await resetPasswordResponse.Content.ReadAsStringAsync());

			var oldPasswordLoginRequest = new LoginRequest(
				email,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var oldPasswordLoginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				oldPasswordLoginRequest);

			Assert.False(oldPasswordLoginResponse.IsSuccessStatusCode);

			var loginBody = await oldPasswordLoginResponse.Content.ReadFromJsonAsync<LoginResponse>()
							?? throw new InvalidOperationException(
								AccountTestGlobals.Diagnostics.LoginResponseBodyWasNotReturned);

			Assert.False(loginBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, loginBody.Outcome);
			Assert.Equal(AccountReasonCodes.InvalidCredentials, loginBody.ReasonCode);
			Assert.Null(loginBody.Account);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ResetPassword_Success_WritesAuditRows()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ResetPasswordAuditRowsUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = AccountTestGlobals.TestAccounts.ResetPasswordAuditRowsDisplayName;

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

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
						  ?? throw new InvalidOperationException(
							  TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(account.AccountId);

			var forgotPasswordRequest = new ForgotPasswordRequest(email);

			var forgotPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ForgotPassword,
				forgotPasswordRequest);

			Assert.True(
				forgotPasswordResponse.IsSuccessStatusCode,
				await forgotPasswordResponse.Content.ReadAsStringAsync());

			var resetCodeDelivery =
				await AccountTestDatabase.ReadLatestCodeDeliveryOutboxStateAsync(
					account.AccountId,
					AccountCodePurposes.PasswordReset,
					AccountDestinationTypes.Email,
					account.NormalizedEmail)
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.PasswordResetCodeDeliveryWasNotFound);

			var acceptedBefore = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.PasswordResetCodeAccepted);

			var consumedBefore = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.CodeConsumed);

			var completedBefore = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.PasswordResetCompleted);

			var resetPasswordRequest = new ResetPasswordRequest(
				email,
				resetCodeDelivery.PlaintextCode,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var resetPasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ResetPassword,
				resetPasswordRequest);

			Assert.True(
				resetPasswordResponse.IsSuccessStatusCode,
				await resetPasswordResponse.Content.ReadAsStringAsync());

			var acceptedAfter = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.PasswordResetCodeAccepted);

			var consumedAfter = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.CodeConsumed);

			var completedAfter = await AccountTestDatabase.CountAuditRowsAsync(
				account.AccountId,
				AccountAuditEvents.PasswordResetCompleted);

			Assert.Equal(acceptedBefore + 1, acceptedAfter);
			Assert.Equal(consumedBefore + 1, consumedAfter);
			Assert.Equal(completedBefore + 1, completedAfter);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}
}
