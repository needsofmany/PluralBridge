// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

/// <summary>
/// Anonymous delivery of the registration document.
/// The host uses the disabled mail adapter and a non-routable account connection.
/// These requests do not open that connection and do not send email.
/// </summary>
public sealed class AccountRegisterRouteTests
{
	/// <summary>
	/// Create account navigates to this document. An unauthenticated request must receive it, not a sign-in redirect.
	/// </summary>
	[Fact]
	public async Task GetRegister_WithoutAuthentication_ReturnsTheRegistrationDocument()
	{
		await using var factory = CreateFactory();
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
		{
			AllowAutoRedirect = false
		});

		var response = await client.GetAsync("/register");
		var body = await response.Content.ReadAsStringAsync();
		var location = response.Headers.Location?.ToString() ?? string.Empty;

		Assert.True(
			response.StatusCode == HttpStatusCode.OK,
			$"GET /register returned {(int)response.StatusCode} Location={location}");
		Assert.True(location.Length == 0, $"GET /register redirected to {location}");
		Assert.Contains("data-auth-page=\"register\"", body, StringComparison.Ordinal);
		Assert.Contains("id=\"registerForm\"", body, StringComparison.Ordinal);
		Assert.DoesNotContain("data-auth-page=\"login\"", body, StringComparison.Ordinal);
	}

	/// <summary>
	/// The registration page stays anonymous. The workspace document stays behind sign-in.
	/// </summary>
	[Fact]
	public async Task GetApp_WithoutAuthentication_RedirectsToSignIn()
	{
		await using var factory = CreateFactory();
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
		{
			AllowAutoRedirect = false
		});

		var response = await client.GetAsync("/app/");
		var location = response.Headers.Location?.ToString() ?? string.Empty;

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Contains("/login", location, StringComparison.Ordinal);
	}

	/// <summary>
	/// Testing environment skips development delivery and user secrets.
	/// The connection string is a documentation address so a mistaken open cannot reach the owner's database.
	/// </summary>
	private static WebApplicationFactory<Program> CreateFactory()
	{
		return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
		{
			builder.UseEnvironment("Testing");
			builder.ConfigureAppConfiguration((_, configuration) =>
			{
				configuration.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["AccountCodeDelivery:Provider"] = "Disabled",
					["ConnectionStrings:PluralBridgeChap2SafeSpine"] =
						"Server=203.0.113.10,1433;Database=PluralBridgeIsolatedRoute;User ID=isolated;Password=isolated;Connect Timeout=1;Encrypt=False"
				});
			});
			builder.ConfigureServices(services =>
			{
				foreach (var descriptor in services.Where(service => service.ServiceType == typeof(IAccountCodeDelivery)).ToList())
				{
					services.Remove(descriptor);
				}

				services.AddScoped<IAccountCodeDelivery, DisabledAccountCodeDelivery>();
			});
		});
	}
}
