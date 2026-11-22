// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Text.Json;
using PluralBridge.Api;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

/// <summary>
/// Names and return-path rules the auth pages rely on.
/// These tests do not execute the browser script and do not open a database connection.
/// </summary>
public sealed class AccountBrowserContractTests
{
	private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

	/// <summary>The pages post these camelCase names. The verification body keeps the existing email field.</summary>
	[Fact]
	public void BrowserRequests_UseTheExistingJsonNames()
	{
		var continuation = JsonSerializer.Serialize(new LoginContinuationRequest("ada"), WebJson);
		var registration = JsonSerializer.Serialize(new RegisterAccountRequest("ada", "ada@example.com", "Ada", "password-value"), WebJson);
		var verification = JsonSerializer.Serialize(new VerifyRegistrationRequest("ada@example.com", "012345"), WebJson);
		var resend = JsonSerializer.Serialize(new ResendRegistrationCodeRequest("ada"), WebJson);

		Assert.Contains("\"usernameOrEmail\":\"ada\"", continuation, StringComparison.Ordinal);
		Assert.Contains("\"username\":\"ada\"", registration, StringComparison.Ordinal);
		Assert.Contains("\"email\":\"ada@example.com\"", registration, StringComparison.Ordinal);
		Assert.Contains("\"displayName\":\"Ada\"", registration, StringComparison.Ordinal);
		Assert.Contains("\"password\":\"password-value\"", registration, StringComparison.Ordinal);
		Assert.Contains("\"email\":\"ada@example.com\"", verification, StringComparison.Ordinal);
		Assert.Contains("\"code\":\"012345\"", verification, StringComparison.Ordinal);
		Assert.Contains("\"usernameOrEmail\":\"ada\"", resend, StringComparison.Ordinal);
	}

	/// <summary>Continuation steps and the reason codes the pages branch on stay stable.</summary>
	[Fact]
	public void BrowserResponses_ExposeStepAndReasonCodes()
	{
		var password = JsonSerializer.Serialize(
			new LoginContinuationResponse(true, "succeeded", "none", "Enter the account password.", RegistrationContinuation.PasswordStep),
			WebJson);
		var verification = JsonSerializer.Serialize(
			new LoginContinuationResponse(true, "succeeded", "none", "Enter the verification code.", RegistrationContinuation.VerificationStep),
			WebJson);
		var registration = JsonSerializer.Serialize(
			new LoginContinuationResponse(true, "succeeded", "none", "Create the account.", RegistrationContinuation.RegistrationStep),
			WebJson);

		Assert.Contains("\"nextStep\":\"password\"", password, StringComparison.Ordinal);
		Assert.Contains("\"nextStep\":\"verification\"", verification, StringComparison.Ordinal);
		Assert.Contains("\"nextStep\":\"registration\"", registration, StringComparison.Ordinal);
		Assert.Equal("registration", RegistrationContinuation.RegistrationStep);
		Assert.Equal("delivery_failed", AccountReasonCodes.DeliveryFailed);
		Assert.Equal(
			"The account was created, but the verification email could not be sent.",
			Globals.accountRegistrationDeliveryFailed);
		Assert.NotEqual(Globals.accountRegistrationCouldNotBeCompleted, Globals.accountRegistrationDeliveryFailed);
		Assert.Equal("expired_code", AccountReasonCodes.ExpiredCode);
		Assert.Equal("invalid_code", AccountReasonCodes.InvalidCode);
		Assert.Equal("rate_limited", AccountReasonCodes.RateLimited);
	}

	/// <summary>
	/// The auth script branches on the three continuation steps and copies the typed identity into the registration form.
	/// This reads the script as text. It does not execute it and does not open a browser.
	/// </summary>
	[Fact]
	public void BrowserScript_RoutesEachContinuationStepAndCarriesTheIdentity()
	{
		var script = File.ReadAllText(AuthPagePath("auth-pages.js"));
		var login = File.ReadAllText(AuthPagePath("login.html"));

		Assert.Contains("var registrationStep = \"registration\";", script, StringComparison.Ordinal);
		Assert.Contains("nextStep === passwordStep", script, StringComparison.Ordinal);
		Assert.Contains("showPasswordStep(identity)", script, StringComparison.Ordinal);
		Assert.Contains("passwordIdentity\").textContent = identity", script, StringComparison.Ordinal);
		Assert.Contains("nextStep === verificationStep", script, StringComparison.Ordinal);
		Assert.Contains("verificationIdentity\").textContent = identity", script, StringComparison.Ordinal);
		Assert.Contains("nextStep === registrationStep", script, StringComparison.Ordinal);
		Assert.Contains("function showRegistrationStep(identity)", script, StringComparison.Ordinal);
		Assert.Contains("username.value = identity;", script, StringComparison.Ordinal);
		Assert.Contains("email.value = identity;", script, StringComparison.Ordinal);
		Assert.DoesNotContain("postJson(registerPath", ExtractFunction(script, "function showRegistrationStep(identity)"), StringComparison.Ordinal);
		Assert.Contains("data-auth-step=\"registration\"", login, StringComparison.Ordinal);
		Assert.Contains("id=\"registerForm\"", login, StringComparison.Ordinal);
		Assert.Contains("id=\"registerUsername\"", login, StringComparison.Ordinal);
		Assert.Contains("id=\"registerEmail\"", login, StringComparison.Ordinal);
	}

	/// <summary>
	/// The sending screen and the disabled provider controls are present on the auth pages.
	/// This reads the files as text. It does not execute the script and does not open a browser.
	/// </summary>
	[Fact]
	public void BrowserPages_ShowSendingProgressAndUnavailableProviders()
	{
		var script = File.ReadAllText(AuthPagePath("auth-pages.js"));
		var style = File.ReadAllText(AuthPagePath("auth.css"));
		var pages = new[]
		{
			AuthPagePath("login.html"),
			AuthPagePath("register.html"),
			AuthPagePath("forgot-username.html"),
			AuthPagePath("forgot-password.html")
		};

		Assert.Contains("function showSendingStep()", script, StringComparison.Ordinal);
		Assert.Contains("Sending your verification email…", script, StringComparison.Ordinal);
		Assert.Contains("Whether the account was created is unknown.", script, StringComparison.Ordinal);
		Assert.Contains("A replacement code may already have been sent.", script, StringComparison.Ordinal);
		Assert.Contains("The account was created, but the verification email could not be sent.", script, StringComparison.Ordinal);
		Assert.DoesNotContain("postJson(registerPath", ExtractFunction(script, "function showSendingStep()"), StringComparison.Ordinal);
		Assert.Equal(3, CountOf(script, "showSendingStep();"));
		Assert.Contains("pluralbridge.theme", style, StringComparison.Ordinal);
		Assert.Contains("data-theme=\"light\"", style, StringComparison.Ordinal);
		Assert.Contains("prefers-reduced-motion", style, StringComparison.Ordinal);

		foreach (var pagePath in pages)
		{
			var page = File.ReadAllText(pagePath);
			Assert.Contains("pluralbridge.theme", page, StringComparison.Ordinal);
			Assert.Contains("prefers-color-scheme", page, StringComparison.Ordinal);
			Assert.Contains("Google sign-in is not available", page, StringComparison.Ordinal);
			Assert.Contains("Apple sign-in is not available", page, StringComparison.Ordinal);
			Assert.Contains("GitHub sign-in is not available", page, StringComparison.Ordinal);
			Assert.Contains("SSO sign-in is not available", page, StringComparison.Ordinal);
			Assert.DoesNotContain("accounts.google.com", page, StringComparison.Ordinal);
			Assert.DoesNotContain("href=\"https://", page, StringComparison.Ordinal);
		}

		var login = File.ReadAllText(AuthPagePath("login.html"));
		var register = File.ReadAllText(AuthPagePath("register.html"));
		Assert.Contains("Username or Email", login, StringComparison.Ordinal);
		Assert.Contains("Remember my username or email on this device.", login, StringComparison.Ordinal);
		Assert.Contains("pluralbridge.rememberedIdentity", script, StringComparison.Ordinal);
		Assert.DoesNotContain("href=\"/terms\"", login, StringComparison.Ordinal);
		Assert.Contains("href=\"/register\"", login, StringComparison.Ordinal);
		Assert.Contains("href=\"/forgot-username\"", login, StringComparison.Ordinal);
		Assert.Contains("href=\"/forgot-password\"", login, StringComparison.Ordinal);
		Assert.Contains("data-auth-step=\"sending\"", login, StringComparison.Ordinal);
		Assert.Contains("data-auth-step=\"sending\"", register, StringComparison.Ordinal);
		Assert.Contains("id=\"registerForm\"", register, StringComparison.Ordinal);
	}

	/// <summary>Counts exact occurrences. The sending-screen check uses this to confirm each request path.</summary>
	private static int CountOf(string text, string value)
	{
		var count = 0;
		var index = 0;
		while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
		{
			count++;
			index += value.Length;
		}

		return count;
	}

	/// <summary>
	/// Finds an auth page from the test source tree. The output folder may sit outside the repository when the default bin is locked.
	/// </summary>
	private static string AuthPagePath(string fileName, [CallerFilePath] string sourceFile = "")
	{
		var fromSource = Path.GetFullPath(Path.Combine(
			Path.GetDirectoryName(sourceFile) ?? string.Empty,
			"..",
			"..",
			"PluralBridge.Api",
			"PluralBridge.Api",
			"wwwroot",
			"auth",
			fileName));
		if (File.Exists(fromSource))
		{
			return fromSource;
		}

		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			var candidate = Path.Combine(
				directory.FullName,
				"api",
				"PluralBridge.Api",
				"PluralBridge.Api",
				"wwwroot",
				"auth",
				fileName);
			if (File.Exists(candidate))
			{
				return candidate;
			}

			directory = directory.Parent;
		}

		throw new FileNotFoundException(fileName);
	}

	private static string ExtractFunction(string script, string signature)
	{
		var start = script.IndexOf(signature, StringComparison.Ordinal);
		Assert.True(start >= 0, signature);
		var end = script.IndexOf("\n\tfunction ", start + signature.Length, StringComparison.Ordinal);
		Assert.True(end > start, signature);
		return script[start..end];
	}

	/// <summary>A successful password sign-in may return only to a relative same-site path.</summary>
	[Fact]
	public void ReturnDestination_KeepsLocalPathsAndDropsExternalOnes()
	{
		Assert.Equal("/app/", AuthReturnDestination.OrApp(null));
		Assert.Equal("/app/members", AuthReturnDestination.OrApp("/app/members"));
		Assert.Equal("/app/", AuthReturnDestination.OrApp("https://example.test/app"));
		Assert.Equal("/app/", AuthReturnDestination.OrApp("//example.test"));
		Assert.Equal("/app/", AuthReturnDestination.OrApp("/%2F%2Fexample.test"));
		Assert.False(AuthReturnDestination.IsLocal("/\\example.test"));
	}
}
