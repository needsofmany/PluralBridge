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
	"legacy-app.css",
	"account-screens.css",
	"members-mobile.css",
	"shell.css"
};

var allowedBrowserJsFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
	"bootstrap.js",
	"api-client.js",
	"members.js",
	"developer-tools.js",
	"legacy-app.js",
	"account-screens.js",
	"shell.js"
};

var allowedBrowserImageFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
	"pluralbridge-about-logo-smaller.png",
	"pluralbridge-about-logo-small.png"
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

app.MapGet("/app/images/{fileName}", (string fileName) =>
{
	if (!allowedBrowserImageFiles.Contains(fileName))
	{
		return Results.NotFound();
	}

	var path = Path.Combine(app.Environment.WebRootPath!, "app", "images", fileName);
	var extension = Path.GetExtension(fileName);
	var contentType = extension.ToLowerInvariant() switch
	{
		".png" => "image/png",
		".jpg" => "image/jpeg",
		".jpeg" => "image/jpeg",
		".gif" => "image/gif",
		".webp" => "image/webp",
		".svg" => "image/svg+xml",
		_ => "application/octet-stream"
	};

	return Results.File(path, contentType);
}).RequireAuthorization();

var authAssetFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
	"auth.css",
	"auth-pages.js"
};

app.MapGet("/auth/{fileName}", (string fileName) =>
{
	if (!authAssetFiles.Contains(fileName))
	{
		return Results.NotFound();
	}

	var path = Path.Combine(
		app.Environment.WebRootPath!,
		"auth",
		fileName);

	return Results.File(
		path,
		fileName.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
			? "text/css"
			: "text/javascript");
});

// add login/recovery pages
app.MapGet(Globals.browserLoginRoute, () =>
{
	var path = Path.Combine(
		app.Environment.WebRootPath!,
		"auth",
		"login.html");

	return Results.File(path, "text/html");
});

app.MapGet($"/{Globals.forgotUsernameEndpointSegment}", () =>
{
	var path = Path.Combine(
		app.Environment.WebRootPath!,
		"auth",
		"forgot-username.html");

	return Results.File(path, "text/html");
});

app.MapGet($"/{Globals.forgotPasswordEndpointSegment}", () =>
{
	var path = Path.Combine(
		app.Environment.WebRootPath!,
		"auth",
		"forgot-password.html");

	return Results.File(path, "text/html");
});

app.MapPost($"/{Globals.forgotUsernameEndpointSegment}", async (
	HttpContext context,
	IAccountService accountService,
	CancellationToken cancellationToken) =>
{
	var form = await context.Request.ReadFormAsync(cancellationToken);
	var email = form["email"].FirstOrDefault() ?? string.Empty;

	var result = await accountService.ForgotUsernameAsync(
		new ForgotUsernameRequest(email),
		cancellationToken);

	var encodedMessage = Uri.EscapeDataString(result.Message ?? string.Empty);
	var status = result.Succeeded ? "success" : "error";

	return Results.Redirect(
		$"/{Globals.forgotUsernameEndpointSegment}?status={status}&message={encodedMessage}");
});

app.MapPost($"/{Globals.forgotPasswordEndpointSegment}", async (
	HttpContext context,
	IAccountService accountService,
	CancellationToken cancellationToken) =>
{
	var form = await context.Request.ReadFormAsync(cancellationToken);
	var usernameOrEmail = form["usernameOrEmail"].FirstOrDefault() ?? string.Empty;

	var result = await accountService.ForgotPasswordAsync(
		new ForgotPasswordRequest(usernameOrEmail),
		cancellationToken);

	var encodedMessage = Uri.EscapeDataString(result.Message ?? string.Empty);
	var status = result.Succeeded ? "success" : "error";

	return Results.Redirect(
		$"/{Globals.forgotPasswordEndpointSegment}?status={status}&message={encodedMessage}");
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
		return Results.Redirect($"{Globals.browserLoginRoute}?error=1");
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
