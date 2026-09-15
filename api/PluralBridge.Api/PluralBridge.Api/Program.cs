// ASP.NET Core authentication APIs.
// These are used below to create and destroy the login cookie.
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

// PluralBridge account services implemented elsewhere in the API project.
using PluralBridge.Api.Account;

// Serilog provides the application's structured logging.
using Serilog;
using Serilog.Events;

// Claims are stored in the authentication cookie to identify the logged-in user.
using System.Security.Claims;


// -----------------------------------------------------------------------------
// APPLICATION BUILDER
// -----------------------------------------------------------------------------
//
// WebApplication.CreateBuilder creates the ASP.NET Core host and gives us access
// to configuration, dependency injection, logging, and environment information.
//
// The command-line arguments are also made available to ASP.NET Core.
//
var builder = WebApplication.CreateBuilder(args);


// -----------------------------------------------------------------------------
// LOGGING
// -----------------------------------------------------------------------------
//
// Replace the default ASP.NET Core logging pipeline with Serilog.
//
// PluralBridge currently writes logs to:
//   - the console
//   - the debugger
//   - rolling log files under logs/
//
// Microsoft framework messages are reduced to Warning so normal application
// logging is not drowned out by framework-level informational messages.
//
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


// -----------------------------------------------------------------------------
// MVC / API CONTROLLERS
// -----------------------------------------------------------------------------
//
// Registers controller support so the REST API controllers can be discovered
// and invoked later when app.MapControllers() is called.
//
builder.Services.AddControllers();


// -----------------------------------------------------------------------------
// COOKIE AUTHENTICATION
// -----------------------------------------------------------------------------
//
// The browser application currently uses ASP.NET Core cookie authentication.
//
// After a successful POST to /login, ASP.NET Core creates a cookie named:
//
//     PluralBridgeProofAuth
//
// That cookie accompanies later browser requests and establishes the user's
// authenticated identity.
//
builder.Services
	.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		// Name of the authentication cookie stored by the browser.
		options.Cookie.Name = "PluralBridgeProofAuth";

		// "/" allows the cookie to be sent for the entire site, including
		// /app, /api, /login, and /logout.
		options.Cookie.Path = "/";

		// Prevent browser JavaScript from directly reading the authentication
		// cookie.
		options.Cookie.HttpOnly = true;

		// Lax provides CSRF-related protection while still supporting ordinary
		// navigation to the application.
		options.Cookie.SameSite = SameSiteMode.Lax;

		// Use HTTPS cookies when the request itself is HTTPS.
		options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

		// Browser requests that require authentication are redirected here.
		options.LoginPath = "/login";
		options.LogoutPath = "/logout";
		options.AccessDeniedPath = "/login";

		// REST API clients should receive HTTP 401 instead of an HTML redirect
		// to /login.
		//
		// This distinction is important:
		//
		// Browser page:
		//     unauthenticated -> redirect to /login
		//
		// REST API:
		//     unauthenticated -> HTTP 401
		//
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
	});


// Authorization works with the authenticated identity created above.
// Individual endpoints can then call RequireAuthorization().
builder.Services.AddAuthorization();


// -----------------------------------------------------------------------------
// SWAGGER / OPENAPI
// -----------------------------------------------------------------------------
//
// These services support the Swagger/OpenAPI development interface.
//
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// -----------------------------------------------------------------------------
// DEVELOPMENT CORS POLICY
// -----------------------------------------------------------------------------
//
// This policy exists for the Phase 2B local browser proof.
//
// It permits cross-origin requests while running locally in Development.
// The policy is only activated later when the environment is Development.
//
builder.Services.AddCors(options =>
{
	options.AddPolicy("Phase2BLocalBrowserProof", policy =>
	{
		policy.AllowAnyOrigin();
		policy.AllowAnyHeader();
		policy.AllowAnyMethod();
	});
});


// -----------------------------------------------------------------------------
// PLURALBRIDGE ACCOUNT SERVICES
// -----------------------------------------------------------------------------
//
// AddScoped means ASP.NET Core creates one instance of each service for the
// lifetime of an individual HTTP request.
//
// Interfaces are used by controllers/services; the concrete implementations
// are supplied here through dependency injection.
//
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IAccountAuditWriter, SqlAccountAuditWriter>();


// Account-code delivery differs by environment.
//
// Development:
//     Codes can be delivered through the development implementation.
//
// Other environments:
//     The development delivery mechanism is disabled.
//
if (builder.Environment.IsDevelopment())
{
	builder.Services.AddScoped<IAccountCodeDelivery, DevelopmentAccountCodeDelivery>();
}
else
{
	builder.Services.AddScoped<IAccountCodeDelivery, DisabledAccountCodeDelivery>();
}


// Main account-domain service.
builder.Services.AddScoped<IAccountService, AccountService>();


// -----------------------------------------------------------------------------
// BUILD THE ASP.NET CORE APPLICATION
// -----------------------------------------------------------------------------
//
// Everything above configured services.
//
// builder.Build() creates the actual application and begins configuration of
// the HTTP request pipeline and endpoints.
//
var app = builder.Build();


// -----------------------------------------------------------------------------
// DEVELOPMENT-ONLY SWAGGER
// -----------------------------------------------------------------------------
//
// Swagger is intentionally exposed only while the application is running in
// the Development environment.
//
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}


// -----------------------------------------------------------------------------
// HTTP -> HTTPS
// -----------------------------------------------------------------------------
//
// Redirect ordinary HTTP requests to HTTPS.
//
app.UseHttpsRedirection();


// -----------------------------------------------------------------------------
// DEVELOPMENT CORS
// -----------------------------------------------------------------------------
//
// Activate the permissive Phase2BLocalBrowserProof CORS policy only while
// running in Development.
//
if (app.Environment.IsDevelopment())
{
	app.UseCors("Phase2BLocalBrowserProof");
}


// -----------------------------------------------------------------------------
// AUTHENTICATION / AUTHORIZATION MIDDLEWARE
// -----------------------------------------------------------------------------
//
// Order matters here.
//
// UseAuthentication reads the incoming authentication cookie and establishes
// HttpContext.User.
//
// UseAuthorization then uses that identity when an endpoint requires
// authorization.
//
app.UseAuthentication();
app.UseAuthorization();


// -----------------------------------------------------------------------------
// BROWSER APPLICATION ROOT
// -----------------------------------------------------------------------------
//
// Visiting the web-site root sends the user to the browser application.
//
app.MapGet("/", () => Results.Redirect("/app/"));


// -----------------------------------------------------------------------------
// PROTECTED BROWSER-ASSET ALLOWLISTS
// -----------------------------------------------------------------------------
//
// IMPORTANT:
//
// PluralBridge is NOT currently exposing wwwroot through unrestricted static
// file middleware.
//
// Instead, the browser application's CSS and JavaScript files are served by
// explicit endpoints farther below:
//
//     /app/css/{fileName}
//     /app/js/{fileName}
//
// Those endpoints check these allowlists before returning a file.
//
// Therefore:
//
//     A file can physically exist under wwwroot/app/css or wwwroot/app/js
//     and STILL return HTTP 404 if its filename is absent from these sets.
//
// This is what happened with shell.css and shell.js.
//
// Whenever a new browser CSS or JavaScript module is intentionally added,
// its filename must also be registered here unless this asset-serving design
// is changed in the future.
//
var allowedBrowserCssFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
	"base.css",
	"layout.css",
	"members.css",
	"members-mobile.css",
	"developer-tools.css",
	"legacy-app.css",
	"shell.css"
};

var allowedBrowserJsFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
	"bootstrap.js",
	"api-client.js",
	"members.js",
	"developer-tools.js",
	"legacy-app.js",
	"shell.js"
};


// -----------------------------------------------------------------------------
// PROTECTED BROWSER APPLICATION FILES
// -----------------------------------------------------------------------------
//
// Unlike conventional ASP.NET Core applications that expose wwwroot through
// UseStaticFiles(), this application explicitly maps the browser files.
//
// Every endpoint below calls RequireAuthorization(), meaning the authentication
// cookie must be present before the file is returned.
//
// app.Environment.WebRootPath is normally the project's "wwwroot" directory.
//


// /app/ serves the browser application's index.html.
app.MapGet("/app/", () =>
{
	var path = Path.Combine(
		app.Environment.WebRootPath!,
		"app",
		"index.html");

	return Results.File(path, "text/html");
}).RequireAuthorization();


// Allow /app/index.html explicitly as well as /app/.
app.MapGet("/app/index.html", () =>
{
	var path = Path.Combine(
		app.Environment.WebRootPath!,
		"app",
		"index.html");

	return Results.File(path, "text/html");
}).RequireAuthorization();


// app.css is the top-level stylesheet loaded by index.html.
//
// app.css can then use @import to request files through /app/css/{fileName}.
app.MapGet("/app/app.css", () =>
{
	var path = Path.Combine(
		app.Environment.WebRootPath!,
		"app",
		"app.css");

	return Results.File(path, "text/css");
}).RequireAuthorization();


// app.js is the top-level JavaScript loader.
//
// app.js subsequently loads JavaScript modules through /app/js/{fileName}.
app.MapGet("/app/app.js", () =>
{
	var path = Path.Combine(
		app.Environment.WebRootPath!,
		"app",
		"app.js");

	return Results.File(path, "text/javascript");
}).RequireAuthorization();


// -----------------------------------------------------------------------------
// PROTECTED CSS MODULES
// -----------------------------------------------------------------------------
//
// Example:
//
//     Browser requests:
//         /app/css/shell.css
//
//     fileName becomes:
//         shell.css
//
// The filename must first exist in allowedBrowserCssFiles.
//
// If it does not, ASP.NET Core deliberately returns HTTP 404.
//
// If it is allowed, the physical file is read from:
//
//     wwwroot/app/css/{fileName}
//
app.MapGet("/app/css/{fileName}", (string fileName) =>
{
	if (!allowedBrowserCssFiles.Contains(fileName))
	{
		return Results.NotFound();
	}

	var path = Path.Combine(
		app.Environment.WebRootPath!,
		"app",
		"css",
		fileName);

	return Results.File(path, "text/css");
}).RequireAuthorization();


// -----------------------------------------------------------------------------
// PROTECTED JAVASCRIPT MODULES
// -----------------------------------------------------------------------------
//
// This performs the same allowlist check for JavaScript.
//
// Example:
//
//     Browser requests:
//         /app/js/shell.js
//
// The file must be present in allowedBrowserJsFiles before ASP.NET Core will
// return the corresponding file from:
//
//     wwwroot/app/js/{fileName}
//
app.MapGet("/app/js/{fileName}", (string fileName) =>
{
	if (!allowedBrowserJsFiles.Contains(fileName))
	{
		return Results.NotFound();
	}

	var path = Path.Combine(
		app.Environment.WebRootPath!,
		"app",
		"js",
		fileName);

	return Results.File(path, "text/javascript");
}).RequireAuthorization();


// -----------------------------------------------------------------------------
// LOGIN PAGE
// -----------------------------------------------------------------------------
//
// The current login page is generated directly here rather than being stored
// as a separate HTML/Razor file.
//
// GET /login returns the HTML form.
//
// The form submits its username/password to:
//
//     POST /login
//
app.MapGet("/login", () =>
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
		"<form method=\"post\" action=\"/login\">" +
		"<label for=\"userName\">Username</label><br>" +
		"<input id=\"userName\" name=\"userName\" autocomplete=\"username\" required><br><br>" +
		"<label for=\"password\">Password</label><br>" +
		"<input id=\"password\" name=\"password\" type=\"password\" autocomplete=\"current-password\" required><br><br>" +
		"<button type=\"submit\">Sign in</button>" +
		"</form>" +
		"</main>" +
		"</body>" +
		"</html>";

	return Results.Content(loginPage, "text/html");
});


// -----------------------------------------------------------------------------
// LOGIN FORM PROCESSING
// -----------------------------------------------------------------------------
//
// POST /login:
//
// 1. Reads the submitted form.
// 2. Reads the configured demo username/password.
// 3. Compares submitted credentials with configured credentials.
// 4. Creates an authenticated ClaimsPrincipal on success.
// 5. Writes the authentication cookie.
// 6. Redirects the browser to /app/.
//
app.MapPost("/login", async (
	HttpContext context,
	IAccountService accountService,
	CancellationToken cancellationToken) =>
{
	var form = await context.Request.ReadFormAsync();

	var userName =
		form["userName"].FirstOrDefault() ??
		string.Empty;

	var password =
		form["password"].FirstOrDefault() ??
		string.Empty;

	var loginResult = await accountService.LoginAsync(
		new LoginRequest(userName, password),
		cancellationToken);

	if (loginResult is not
		{
			Succeeded: true,
			Value: { Account: not null } loginResponse
		})
	{
		return Results.Redirect("/login");
	}


	// Claims describe the authenticated identity.
	var claims = new List<Claim>
	{
		new(
			ClaimTypes.NameIdentifier,
			loginResponse.Account.AccountId.ToString()),
		new(
			ClaimTypes.Name,
			loginResponse.Account.Username)
	};


	// Build an identity using the same cookie authentication scheme that was
	// configured above.
	var identity = new ClaimsIdentity(
		claims,
		CookieAuthenticationDefaults.AuthenticationScheme);

	var principal = new ClaimsPrincipal(identity);


	// SignInAsync causes ASP.NET Core's cookie authentication handler to issue
	// the PluralBridgeProofAuth cookie.
	await context.SignInAsync(
		CookieAuthenticationDefaults.AuthenticationScheme,
		principal);


	// The browser can now request the protected /app/ resources.
	return Results.Redirect("/app/");
});


// -----------------------------------------------------------------------------
// LOGOUT
// -----------------------------------------------------------------------------
//
// POST /logout removes the authentication cookie and returns the user to the
// login page.
//
// RequireAuthorization prevents anonymous callers from invoking the protected
// logout action.
//
app.MapPost("/logout", async (HttpContext context) =>
{
	await context.SignOutAsync(
		CookieAuthenticationDefaults.AuthenticationScheme);

	return Results.Redirect("/login");
}).RequireAuthorization();


// -----------------------------------------------------------------------------
// OPTIONAL DEBUG AUTHENTICATION ENDPOINT
// -----------------------------------------------------------------------------
//
// This endpoint exists only when DEBUG_MODE is defined.
//
// It makes it possible to inspect the authenticated identity without exposing
// it in normal builds.
//
#if DEBUG_MODE
app.MapGet(
	"/whoami",
	(HttpContext context) => Results.Json(new
	{
		isAuthenticated =
			context.User.Identity?.IsAuthenticated ??
			false,

		name =
			context.User.Identity?.Name ??
			string.Empty
	}));
#endif


// -----------------------------------------------------------------------------
// OPTIONAL DEBUG BROWSER-PATH ENDPOINT
// -----------------------------------------------------------------------------
//
// Also compiled only when DEBUG_MODE is defined.
//
// This is useful when diagnosing browser-asset problems because it reports the
// content root and web root that the running ASP.NET Core process is actually
// using.
//
// The endpoint also checks whether api-client.js exists at the expected
// physical location.
//
#if DEBUG_MODE
app.MapGet("/debug/browser-paths", () =>
{
	return Results.Json(new
	{
		contentRootPath =
			app.Environment.ContentRootPath,

		webRootPath =
			app.Environment.WebRootPath,

		appIndexPath =
			Path.Combine(
				app.Environment.WebRootPath!,
				"app",
				"index.html"),

		appApiClientPath =
			Path.Combine(
				app.Environment.WebRootPath!,
				"app",
				"js",
				"api-client.js"),

		appApiClientExists =
			File.Exists(
				Path.Combine(
					app.Environment.WebRootPath!,
					"app",
					"js",
					"api-client.js"))
	});
}).RequireAuthorization();
#endif


// -----------------------------------------------------------------------------
// REST API CONTROLLERS
// -----------------------------------------------------------------------------
//
// Map all controller routes registered through AddControllers().
//
// RequireAuthorization() applies the authentication requirement to the
// controller endpoints as a group.
//
app.MapControllers().RequireAuthorization();


// -----------------------------------------------------------------------------
// START THE APPLICATION
// -----------------------------------------------------------------------------
//
// Run starts Kestrel and begins processing HTTP requests.
//
app.Run();
