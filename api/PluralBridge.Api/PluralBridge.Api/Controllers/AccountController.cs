using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PluralBridge.Api.Account;

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

		if (result is { Succeeded: true, Value: not null })
		{
			return Ok(result.Value);
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
}