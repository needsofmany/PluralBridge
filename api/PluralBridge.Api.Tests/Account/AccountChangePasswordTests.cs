using Microsoft.AspNetCore.Mvc.Testing;
using PluralBridge.Api.Account;
using System.Net;
using System.Net.Http.Json;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountChangePasswordTests
{
	[Fact]
	public async Task ChangePassword_ValidCurrentPassword_ChangesPassword()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ChangePasswordValidUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = AccountTestGlobals.TestAccounts.ChangePasswordValidDisplayName;

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

			var loginRequest = new LoginRequest(
				email,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var changePasswordRequest = new ChangePasswordRequest(
				AccountTestGlobals.TestAccounts.DefaultPassword,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var changePasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ChangePassword,
				changePasswordRequest);

			Assert.True(
				changePasswordResponse.IsSuccessStatusCode,
				await changePasswordResponse.Content.ReadAsStringAsync());

			var changePasswordBody =
				await changePasswordResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.ChangePasswordResponseBodyWasNotReturned);

			Assert.True(changePasswordBody.Succeeded);
			Assert.Equal(AccountOutcomes.Succeeded, changePasswordBody.Outcome);
			Assert.Equal(AccountReasonCodes.None, changePasswordBody.ReasonCode);

			var changedPasswordLoginRequest = new LoginRequest(
				email,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var changedPasswordLoginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				changedPasswordLoginRequest);

			Assert.True(
				changedPasswordLoginResponse.IsSuccessStatusCode,
				await changedPasswordLoginResponse.Content.ReadAsStringAsync());
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ChangePassword_WrongCurrentPassword_IsRejected()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ChangePasswordWrongCurrentUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName =
			AccountTestGlobals.TestAccounts.ChangePasswordWrongCurrentDisplayName;

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

			var loginRequest = new LoginRequest(
				email,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var changePasswordRequest = new ChangePasswordRequest(
				AccountTestGlobals.TestAccounts.WrongPassword,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var changePasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ChangePassword,
				changePasswordRequest);

			Assert.False(changePasswordResponse.IsSuccessStatusCode);

			var changePasswordBody =
				await changePasswordResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.ChangePasswordResponseBodyWasNotReturned);

			Assert.False(changePasswordBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, changePasswordBody.Outcome); 
			Assert.Equal(
				AccountReasonCodes.InvalidCredentials,
				changePasswordBody.ReasonCode);

			var requestedAuditRows =
				await AccountTestDatabase.CountAuditRowsAsync(
					account.AccountId,
					AccountAuditEvents.PasswordChangeRequested);

			var rejectedAuditRows =
				await AccountTestDatabase.CountAuditRowsAsync(
					account.AccountId,
					AccountAuditEvents.PasswordChangeRejected);

			Assert.Equal(1, requestedAuditRows);
			Assert.Equal(1, rejectedAuditRows);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ChangePassword_Success_OldPasswordIsRejected()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ChangePasswordOldPasswordUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName =
			AccountTestGlobals.TestAccounts.ChangePasswordOldPasswordDisplayName;

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

			var loginRequest = new LoginRequest(
				email,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var changePasswordRequest = new ChangePasswordRequest(
				AccountTestGlobals.TestAccounts.DefaultPassword,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var changePasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ChangePassword,
				changePasswordRequest);

			Assert.True(
				changePasswordResponse.IsSuccessStatusCode,
				await changePasswordResponse.Content.ReadAsStringAsync());

			var oldPasswordLoginRequest = new LoginRequest(
				email,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var oldPasswordLoginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				oldPasswordLoginRequest);

			Assert.False(oldPasswordLoginResponse.IsSuccessStatusCode);

			var oldPasswordLoginBody =
				await oldPasswordLoginResponse.Content.ReadFromJsonAsync<LoginResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.LoginResponseBodyWasNotReturned);

			Assert.False(oldPasswordLoginBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, oldPasswordLoginBody.Outcome);
			Assert.Equal(
				AccountReasonCodes.InvalidCredentials,
				oldPasswordLoginBody.ReasonCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ChangePassword_NewPasswordTooShort_IsRejected()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ChangePasswordShortPasswordUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName =
			AccountTestGlobals.TestAccounts.ChangePasswordShortPasswordDisplayName;

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

			var loginRequest = new LoginRequest(
				email,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var changePasswordRequest = new ChangePasswordRequest(
				AccountTestGlobals.TestAccounts.DefaultPassword,
				AccountTestGlobals.TestAccounts.TooShortPassword);

			var changePasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ChangePassword,
				changePasswordRequest);

			Assert.False(changePasswordResponse.IsSuccessStatusCode);

			var changePasswordBody =
				await changePasswordResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.ChangePasswordResponseBodyWasNotReturned);

			Assert.False(changePasswordBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, changePasswordBody.Outcome);
			Assert.Equal(
				AccountReasonCodes.ValidationFailed,
				changePasswordBody.ReasonCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ChangePassword_UnauthenticatedRequest_ReturnsUnauthorized()
	{
		await using var factory = AccountTestHost.CreateFactory();

		using var client = factory.CreateClient(
			new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false
			});

		var request = new ChangePasswordRequest(
			AccountTestGlobals.TestAccounts.DefaultPassword,
			AccountTestGlobals.TestAccounts.ChangedPassword);

		var response = await client.PostAsJsonAsync(
			AccountTestGlobals.Routes.ChangePassword,
			request);

		Assert.Equal(
			HttpStatusCode.Unauthorized,
			response.StatusCode);
	}

	[Fact]
	public async Task ChangePassword_Success_WritesAuditRows()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ChangePasswordAuditRowsUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName =
			AccountTestGlobals.TestAccounts.ChangePasswordAuditRowsDisplayName;

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

			var loginRequest = new LoginRequest(
				email,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			var changePasswordRequest = new ChangePasswordRequest(
				AccountTestGlobals.TestAccounts.DefaultPassword,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var changePasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ChangePassword,
				changePasswordRequest);

			Assert.True(
				changePasswordResponse.IsSuccessStatusCode,
				await changePasswordResponse.Content.ReadAsStringAsync());

			var requestedAuditRows =
				await AccountTestDatabase.CountAuditRowsAsync(
					account.AccountId,
					AccountAuditEvents.PasswordChangeRequested);

			var completedAuditRows =
				await AccountTestDatabase.CountAuditRowsAsync(
					account.AccountId,
					AccountAuditEvents.PasswordChangeCompleted);

			Assert.Equal(1, requestedAuditRows);
			Assert.Equal(1, completedAuditRows);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task ChangePassword_AccountBecomesUnavailable_IsRejected()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username =
			$"{AccountTestGlobals.TestAccounts.UsernamePrefix}" +
			$"{AccountTestGlobals.TestAccounts.ChangePasswordUnavailableUsernameSegment}" +
			$"{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName =
			AccountTestGlobals.TestAccounts.ChangePasswordUnavailableDisplayName;

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

			var loginRequest = new LoginRequest(
				email,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.True(
				loginResponse.IsSuccessStatusCode,
				await loginResponse.Content.ReadAsStringAsync());

			await AccountTestDatabase.DisableRuntimeTestAccountAsync(account.AccountId);

			var changePasswordRequest = new ChangePasswordRequest(
				AccountTestGlobals.TestAccounts.DefaultPassword,
				AccountTestGlobals.TestAccounts.ChangedPassword);

			var changePasswordResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.ChangePassword,
				changePasswordRequest);

			Assert.False(changePasswordResponse.IsSuccessStatusCode);

			var changePasswordBody =
				await changePasswordResponse.Content.ReadFromJsonAsync<AccountOperationResponse>()
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.ChangePasswordResponseBodyWasNotReturned);

			Assert.False(changePasswordBody.Succeeded);
			Assert.Equal(AccountOutcomes.Rejected, changePasswordBody.Outcome);
			Assert.Equal(
				AccountReasonCodes.AccountUnavailable,
				changePasswordBody.ReasonCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}
}
