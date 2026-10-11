// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountLoginTests
{
	[Fact]
	public async Task Login_ValidCredentials_ReturnsSuccess()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}login_valid_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Login Valid";

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

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(accountId);

			var loginRequest = new LoginRequest(
				username,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.True(loginResponse.IsSuccessStatusCode, await loginResponse.Content.ReadAsStringAsync());
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task Login_InvalidPassword_IsRejected()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}login_invalid_password_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		const string displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Login Invalid Password";

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

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(accountId);

			var loginRequest = new LoginRequest(
				username,
				"WrongRuntimeTestPassword001!");

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.False(loginResponse.IsSuccessStatusCode);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task Login_PendingEmailVerification_IsRejected()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}login_pending_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Login Pending";

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

			var loginRequest = new LoginRequest(
				username,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.False(loginResponse.IsSuccessStatusCode);

			var stillPendingAccount = await AccountTestDatabase.ReadAccountStateAsync(username);
			var pendingAccount = stillPendingAccount ?? throw new InvalidOperationException("Runtime test account was not found.");

			Assert.Equal(accountId, pendingAccount.AccountId);
			Assert.Equal(2, pendingAccount.AccountStatusId);
			Assert.False(pendingAccount.IsEmailVerified);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task Login_ValidEmailCredentials_ReturnsSuccess()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}login_email_valid_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Login Email Valid";

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

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(accountId);

			var loginRequest = new LoginRequest(
				email,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.True(loginResponse.IsSuccessStatusCode, await loginResponse.Content.ReadAsStringAsync());
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task Login_UnknownAccount_IsRejected()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}login_unknown_{testId}";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var loginRequest = new LoginRequest(
				username,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.False(loginResponse.IsSuccessStatusCode);

			var accountCount = await AccountTestDatabase.CountAccountsByNormalizedUsernameAsync(username.ToUpperInvariant());

			Assert.Equal(0, accountCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task Login_InvalidRequest_IsRejected()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}login_invalid_request_{testId}";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient();

			var loginRequest = new LoginRequest(
				username,
				string.Empty);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.False(loginResponse.IsSuccessStatusCode);

			var accountCount = await AccountTestDatabase.CountAccountsByNormalizedUsernameAsync(username.ToUpperInvariant());

			Assert.Equal(0, accountCount);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task Login_ValidCredentials_UpdatesLastLogin()
	{
		var testId = Guid.NewGuid().ToString("N");
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}login_updates_last_login_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Login Updates Last Login";

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

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(accountId);

			var beforeLogin = await AccountTestDatabase.ReadLastLoginAtUtcAsync(accountId);

			Assert.Null(beforeLogin);

			var loginRequest = new LoginRequest(
				username,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var loginResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Login,
				loginRequest);

			Assert.True(loginResponse.IsSuccessStatusCode, await loginResponse.Content.ReadAsStringAsync());

			var afterLogin = await AccountTestDatabase.ReadLastLoginAtUtcAsync(accountId);

			Assert.NotNull(afterLogin);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task HtmlLogin_ValidCredentials_ResolvesAccountThroughApiMe()
	{
		var testId = Guid.NewGuid().ToString(AccountTestGlobals.Formats.CompactGuid);
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}{AccountTestGlobals.TestAccounts.HtmlLoginUsernameSegment}{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var systemName = $"{AccountTestGlobals.TestAccounts.HtmlLoginSystemNamePrefix}{testId}";
		var memberDisplayName = $"{AccountTestGlobals.TestAccounts.HtmlLoginMemberDisplayNamePrefix}{testId}";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();

			using var client = factory.CreateClient(
				new WebApplicationFactoryClientOptions
				{
					AllowAutoRedirect = false
				});

			var registerRequest = new RegisterAccountRequest(
				username,
				email,
				AccountTestGlobals.TestAccounts.HtmlLoginDisplayName,
				AccountTestGlobals.TestAccounts.DefaultPassword);

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				registerRequest);

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var accountState =
				await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(
					TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			await AccountTestDatabase.ActivateRuntimeTestAccountAsync(
				accountState.AccountId);

			await AccountTestDatabase.CreateRuntimeAccessFixtureAsync(
				accountState.AccountId,
				systemName,
				memberDisplayName);

			using var loginForm = new FormUrlEncodedContent(
				new Dictionary<string, string>
				{
					[AccountTestGlobals.FormFields.UserName] = username,
					[AccountTestGlobals.FormFields.Password] = AccountTestGlobals.TestAccounts.DefaultPassword
				});

			var loginResponse = await client.PostAsync(
				AccountTestGlobals.Routes.HtmlLogin,
				loginForm);

			Assert.Equal(
				HttpStatusCode.Redirect,
				loginResponse.StatusCode);

			Assert.Equal(
				AccountTestGlobals.Routes.App,
				loginResponse.Headers.Location?.OriginalString);

			var meResponse = await client.GetAsync(
				AccountTestGlobals.Routes.Me);

			var meResponseText = await meResponse.Content.ReadAsStringAsync();

			Assert.True(
				meResponse.IsSuccessStatusCode,
				meResponseText);

			var meResponseBody = JsonSerializer.Deserialize<MeResponse>(
				meResponseText,
				new JsonSerializerOptions(JsonSerializerDefaults.Web))
				?? throw new InvalidOperationException(
					AccountTestGlobals.Diagnostics.ResponseBodyWasNotReturned);

			Assert.Equal(
				accountState.AccountId,
				meResponseBody.CurrentAccount.AccountId);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	private sealed record MeResponse(
		MeAccount CurrentAccount);

	private sealed record MeAccount(
		Guid AccountId);
}
