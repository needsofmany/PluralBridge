using Microsoft.AspNetCore.Mvc;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Controllers;


[ApiController]
[Route("api/account")]
public sealed class AccountController(IAccountService accountService) : ControllerBase
{
	[HttpPost("register")]
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

	[HttpPost("verify-registration")]
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

	[HttpPost("login")]
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

	[HttpPost("forgot-password")]
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
}