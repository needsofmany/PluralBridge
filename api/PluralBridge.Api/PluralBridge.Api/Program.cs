// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PluralBridge.Api;
using PluralBridge.Api.Account;
using Serilog;
using Serilog.Events;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
	loggerConfiguration
		.MinimumLevel.Information()
		.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
		.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
		.Enrich.FromLogContext()
		.WriteTo.Console()
		.WriteTo.Debug()
		.WriteTo.File(
			path: "logs/pb-api-.log",
			rollingInterval: RollingInterval.Day,
			retainedFileCountLimit: 14,
			shared: true);
});

// Add services to the container.
builder.Services.AddControllers();

builder.Services
	.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.Cookie.Name = "PluralBridgeProofAuth";
		options.Cookie.Path = "/";
		options.Cookie.HttpOnly = true;
		options.Cookie.SameSite = SameSiteMode.Lax;
		options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
		options.LoginPath = Globals.browserLoginRoute;
		options.LogoutPath = Globals.browserLogoutRoute;
		options.AccessDeniedPath = Globals.browserLoginRoute;
		options.Events.OnRedirectToLogin = context =>
		{
			if (context.Request.Path.StartsWithSegments("/api"))
			{
				context.Response.StatusCode = StatusCodes.Status401Unauthorized;

				return Task.CompletedTask;
			}

			context.Response.Redirect(context.RedirectUri);

			return Task.CompletedTask;
		};
		options.Events.OnRedirectToAccessDenied = context =>
		{
			if (context.Request.Path.StartsWithSegments("/api"))
			{
				context.Response.StatusCode = StatusCodes.Status403Forbidden;

				return Task.CompletedTask;
			}

			context.Response.Redirect(context.RedirectUri);

			return Task.CompletedTask;
		};
	});

builder.Services.AddAuthorization();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
	options.AddPolicy("Phase2BLocalBrowserProof", policy =>
	{
		policy.AllowAnyOrigin();
		policy.AllowAnyHeader();
		policy.AllowAnyMethod();
	});
});

builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IAccountAuditWriter, SqlAccountAuditWriter>();

var accountCodeDeliveryProvider = builder.Configuration["AccountCodeDelivery:Provider"];

if (StringComparer.OrdinalIgnoreCase.Equals(accountCodeDeliveryProvider, "AzureCommunicationServices"))
{
	builder.Services.AddScoped<IAccountCodeDelivery, AzureAccountCodeDelivery>();
}
else if (builder.Environment.IsDevelopment())
{
	builder.Services.AddScoped<IAccountCodeDelivery, DevelopmentAccountCodeDelivery>();
}
else
{
	builder.Services.AddScoped<IAccountCodeDelivery, DisabledAccountCodeDelivery>();
}

builder.Services.AddScoped<IAccountService, AccountService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// adds middleware for HTTP -> HTTPS
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
	// allow cross domain requests
	app.UseCors("Phase2BLocalBrowserProof");
}

// enable authentication
app.UseAuthentication();

// enable authorization middleware
app.UseAuthorization();

// add static app redirects
app.MapGet("/", () => Results.Redirect(Globals.browserAppRoute));

var allowedBrowserCssFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
	"base.css",
	"layout.css",
	"members.css",
	"developer-tools.css",
	"legacy-app.css"
};

var allowedBrowserJsFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
	"bootstrap.js",
	"api-client.js",
	"members.js",
	"developer-tools.js",
	"legacy-app.js"
};

// require login for the browser button app files
app.MapGet(Globals.browserAppRoute, () =>
{
	var path = Path.Combine(app.Environment.WebRootPath!, "app", "index.html");

	return Results.File(path, "text/html");
}).RequireAuthorization();

app.MapGet("/app/index.html", () =>
{
	var path = Path.Combine(app.Environment.WebRootPath!, "app", "index.html");

	return Results.File(path, "text/html");
}).RequireAuthorization();

app.MapGet("/app/app.css", () =>
{
	var path = Path.Combine(app.Environment.WebRootPath!, "app", "app.css");

	return Results.File(path, "text/css");
}).RequireAuthorization();

app.MapGet("/app/app.js", () =>
{
	var path = Path.Combine(app.Environment.WebRootPath!, "app", "app.js");

	return Results.File(path, "text/javascript");
}).RequireAuthorization();

app.MapGet("/app/css/{fileName}", (string fileName) =>
{
	if (!allowedBrowserCssFiles.Contains(fileName))
	{
		return Results.NotFound();
	}

	var path = Path.Combine(app.Environment.WebRootPath!, "app", "css", fileName);

	return Results.File(path, "text/css");
}).RequireAuthorization();

app.MapGet("/app/js/{fileName}", (string fileName) =>
{
	if (!allowedBrowserJsFiles.Contains(fileName))
	{
		return Results.NotFound();
	}

	var path = Path.Combine(app.Environment.WebRootPath!, "app", "js", fileName);

	return Results.File(path, "text/javascript");
}).RequireAuthorization();

// add login page endpoint
app.MapGet(Globals.browserLoginRoute, () =>
{
	const string loginPage =
		"<!doctype html>" +
		"<html lang=\"en\">" +
		"<head>" +
		"<meta charset=\"utf-8\">" +
		"<title>PluralBridge Demo Login</title>" +
		"<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
		"</head>" +
		"<body>" +
		"<main>" +
		"<h1>PluralBridge Demo Login</h1>" +
		"<p>Private Phase 2B engineering proof.</p>" +
		$"<form method=\"post\" action=\"{Globals.browserLoginRoute}\">" +
		$"<label for=\"{Globals.browserLoginUserNameField}\">Username</label><br>" +
		$"<input id=\"{Globals.browserLoginUserNameField}\" name=\"{Globals.browserLoginUserNameField}\" autocomplete=\"username\" required><br><br>" +
		$"<label for=\"{Globals.browserLoginPasswordField}\">Password</label><br>" +
		$"<input id=\"{Globals.browserLoginPasswordField}\" name=\"{Globals.browserLoginPasswordField}\" type=\"password\" autocomplete=\"current-password\" required><br><br>" +
		"<button type=\"submit\">Sign in</button>" +
		"</form>" +
		"</main>" +
		"</body>" +
		"</html>";

	return Results.Content(loginPage, "text/html");
});

// add login form post endpoint
app.MapPost(Globals.browserLoginRoute, async (
	HttpContext context,
	IAccountService accountService,
	CancellationToken cancellationToken) =>
{
	var form = await context.Request.ReadFormAsync(cancellationToken);

	var usernameOrEmail = form[Globals.browserLoginUserNameField].FirstOrDefault() ?? string.Empty;
	var password = form[Globals.browserLoginPasswordField].FirstOrDefault() ?? string.Empty;

	var result = await accountService.LoginAsync(
		new LoginRequest(
			usernameOrEmail,
			password),
		cancellationToken);

	if (result is not
		{
			Succeeded: true,
			Value: { Account: not null } loginResponse
		})
	{
		return Results.Redirect(Globals.browserLoginRoute);
	}

	var claims = new List<Claim>
	{
		new(
			ClaimTypes.NameIdentifier,
			loginResponse.Account.AccountId.ToString()),
		new(
			ClaimTypes.Name,
			loginResponse.Account.Username)
	};

	var identity = new ClaimsIdentity(
		claims,
		CookieAuthenticationDefaults.AuthenticationScheme);

	var principal = new ClaimsPrincipal(identity);

	await context.SignInAsync(
		CookieAuthenticationDefaults.AuthenticationScheme,
		principal);

	return Results.Redirect(Globals.browserAppRoute);
});

// add logout endpoint
app.MapPost(Globals.browserLogoutRoute, async (HttpContext context) =>
{
	await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

	return Results.Redirect(Globals.browserLoginRoute);
}).RequireAuthorization();

#if DEBUG_MODE
// temporary auth diagnostic endpoint
app.MapGet("/whoami", (HttpContext context) => Results.Json(new
{
	isAuthenticated = context.User.Identity?.IsAuthenticated ?? false,
	name = context.User.Identity?.Name ?? string.Empty
}));
#endif

#if DEBUG_MODE
app.MapGet("/debug/browser-paths", () =>
{
	return Results.Json(new
	{
		contentRootPath = app.Environment.ContentRootPath,
		webRootPath = app.Environment.WebRootPath,
		appIndexPath = Path.Combine(app.Environment.WebRootPath!, "app", "index.html"),
		appApiClientPath = Path.Combine(app.Environment.WebRootPath!, "app", "js", "api-client.js"),
		appApiClientExists = File.Exists(Path.Combine(app.Environment.WebRootPath!, "app", "js", "api-client.js"))
	});
}).RequireAuthorization();
#endif

// add API controller endpoints
app.MapControllers().RequireAuthorization();

app.Run();
