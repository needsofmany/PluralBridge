using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PluralBridge.Api.Account;
using System.Security.Claims;

namespace PluralBridge.Api.Controllers;

[ApiController]
[Route(Globals.accountRouteRoot)]
public sealed class AccountController(IAccountService accountService) : ControllerBase
{
	[AllowAnonymous]
	[HttpPost(Globals.registerEndpointSegment)]
	public async Task<ActionResult<AccountOperationResponse>> Register(
		[FromBody] RegisterAccountRequest request,
		CancellationToken cancellationToken)
	{
		var result = await accountService.RegisterAsync(request, cancellationToken);

		if (result is { Succeeded: true, Value: not null })
		{
			return Ok(result.Value);
		}

		var response = new AccountOperationResponse(
			false,
			result.Outcome,
			result.ReasonCode,
			result.Message);

		return BadRequest(response);
	}

	[AllowAnonymous]
	[HttpPost(Globals.verifyRegistrationEndpointSegment)]
	public async Task<ActionResult<AccountOperationResponse>> VerifyRegistration(
		[FromBody] VerifyRegistrationRequest request,
		CancellationToken cancellationToken)
	{
		var result = await accountService.VerifyRegistrationAsync(request, cancellationToken);

		if (result is { Succeeded: true, Value: not null })
		{
			return Ok(result.Value);
		}

		var response = new AccountOperationResponse(
			false,
			result.Outcome,
			result.ReasonCode,
			result.Message);

		return BadRequest(response);
	}

	[AllowAnonymous]
	[HttpPost(Globals.loginEndpointSegment)]
	public async Task<ActionResult<LoginResponse>> Login(
		[FromBody] LoginRequest request,
		CancellationToken cancellationToken)
	{
		var result = await accountService.LoginAsync(request, cancellationToken);

		if (result is
			{
				Succeeded: true,
				Value: { Account: not null } loginResponse
			})
		{
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

			await HttpContext.SignInAsync(
				CookieAuthenticationDefaults.AuthenticationScheme,
				principal);

			return Ok(loginResponse);
		}

		var response = new LoginResponse(
			false,
			result.Outcome,
			result.ReasonCode,
			result.Message,
			null);

		return BadRequest(response);
	}

	[AllowAnonymous]
	[HttpPost(Globals.forgotUsernameEndpointSegment)]
	public async Task<ActionResult<AccountOperationResponse>> ForgotUsername(
		[FromBody] ForgotUsernameRequest request,
		CancellationToken cancellationToken)
	{
		var result = await accountService.ForgotUsernameAsync(request, cancellationToken);

		if (result is { Succeeded: true, Value: not null })
		{
			return Ok(result.Value);
		}

		var response = new AccountOperationResponse(
			false,
			result.Outcome,
			result.ReasonCode,
			result.Message);

		return BadRequest(response);
	}

	[AllowAnonymous]
	[HttpPost(Globals.forgotPasswordEndpointSegment)]
	public async Task<ActionResult<AccountOperationResponse>> ForgotPassword(
		[FromBody] ForgotPasswordRequest request,
		CancellationToken cancellationToken)
	{
		var result = await accountService.ForgotPasswordAsync(request, cancellationToken);

		if (result is { Succeeded: true, Value: not null })
		{
			return Ok(result.Value);
		}

		var response = new AccountOperationResponse(
			false,
			result.Outcome,
			result.ReasonCode,
			result.Message);

		return BadRequest(response);
	}

	[AllowAnonymous]
	[HttpPost(Globals.resetPasswordEndpointSegment)]
	public async Task<ActionResult<AccountOperationResponse>> ResetPassword(
		[FromBody] ResetPasswordRequest request,
		CancellationToken cancellationToken)
	{
		var result = await accountService.ResetPasswordAsync(request, cancellationToken);

		if (result is { Succeeded: true, Value: not null })
		{
			return Ok(result.Value);
		}

		var response = new AccountOperationResponse(
			false,
			result.Outcome,
			result.ReasonCode,
			result.Message);

		return BadRequest(response);
	}

	[Authorize]
	[HttpPost(Globals.changePasswordEndpointSegment)]
	public async Task<ActionResult<AccountOperationResponse>> ChangePassword(
		[FromBody] ChangePasswordRequest request,
		CancellationToken cancellationToken)
	{
		var accountIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

		if (!Guid.TryParse(accountIdValue, out var actorAccountId))
		{
			var unauthorizedResponse = new AccountOperationResponse(
				false,
				AccountOutcomes.Denied,
				AccountReasonCodes.SessionRequired,
				Globals.authenticationRequired);

			return Unauthorized(unauthorizedResponse);
		}

		var result = await accountService.ChangePasswordAsync(
			actorAccountId,
			request,
			cancellationToken);

		if (result is { Succeeded: true, Value: not null })
		{
			return Ok(result.Value);
		}

		var response = new AccountOperationResponse(
			false,
			result.Outcome,
			result.ReasonCode,
			result.Message);

		return BadRequest(response);
	}
}
