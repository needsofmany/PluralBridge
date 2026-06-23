using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
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
		options.LoginPath = "/login";
		options.LogoutPath = "/logout";
		options.AccessDeniedPath = "/login";
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
app.MapGet("/", () => Results.Redirect("/app/"));

// require login for the browser button app files
app.MapGet("/app/", () =>
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

// add login page endpoint
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

// add login form post endpoint
app.MapPost("/login", async (HttpContext context, IConfiguration configuration) =>
{
	var form = await context.Request.ReadFormAsync();

	var userName = form["userName"].FirstOrDefault() ?? string.Empty;
	var password = form["password"].FirstOrDefault() ?? string.Empty;

	var configuredUserName = configuration["ProtectedDemo:UserName"] ?? string.Empty;
	var configuredPassword = configuration["ProtectedDemo:Password"] ?? string.Empty;

	if (string.IsNullOrWhiteSpace(configuredUserName) ||
	    string.IsNullOrWhiteSpace(configuredPassword) ||
	    !string.Equals(userName, configuredUserName, StringComparison.Ordinal) ||
	    !string.Equals(password, configuredPassword, StringComparison.Ordinal))
	{
		return Results.Redirect("/login");
	}

	var claims = new List<Claim>
	{
		new(ClaimTypes.Name, configuredUserName)
	};

	var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
	var principal = new ClaimsPrincipal(identity);

	await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

	return Results.Redirect("/app/");
});

// add logout endpoint
app.MapPost("/logout", async (HttpContext context) =>
{
	await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

	return Results.Redirect("/login");
}).RequireAuthorization();

#if DEBUG_MODE
// temporary auth diagnostic endpoint
app.MapGet("/whoami", (HttpContext context) => Results.Json(new
{
	isAuthenticated = context.User.Identity?.IsAuthenticated ?? false,
	name = context.User.Identity?.Name ?? string.Empty
}));
#endif

// add API controller endpoints
app.MapControllers().RequireAuthorization();

app.Run();
