// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Account;

public sealed partial class AccountService
{
	/// <summary>
	/// Checks one account's password. Unknown identifiers, ambiguous identifiers, and failed passwords share the invalid-credentials result.
	/// One ineligible account keeps the unavailable result. A successful result is the only result that records last login.
	/// The controller and the browser login post issue a session only from that successful result.
	/// </summary>
	public async Task<AccountServiceResult<LoginResponse>> LoginAsync(
		LoginRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);

		// Login failures intentionally collapse to generic invalid/unavailable responses.
		// The audit path is where operations can distinguish validation, status, and hash failures.
		if (!AccountText.HasText(request.UsernameOrEmail) || !AccountText.HasText(request.Password))
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.LoginRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCredentials,
				null,
				null,
				correlationId,
				Globals.accountOperationLogin,
				cancellationToken);

			return AccountServiceResult<LoginResponse>.Rejected(
				AccountReasonCodes.InvalidCredentials,
				Globals.accountLoginCouldNotBeCompleted);
		}

		var normalizedIdentifier = AccountText.NormalizeEmail(request.UsernameOrEmail);

		// Null covers an unknown identifier and an identifier that matches more than one account.
		var account = await _credentials.ReadLoginAccountAsync(normalizedIdentifier, cancellationToken);

		if (account is null)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.LoginRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCredentials,
				null,
				null,
				correlationId,
				Globals.accountOperationLogin,
				cancellationToken);

			return AccountServiceResult<LoginResponse>.Rejected(
				AccountReasonCodes.InvalidCredentials,
				Globals.accountLoginCouldNotBeCompleted);
		}

		if (account.AccountStatusId != 1 || !account.IsEmailVerified)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.LoginRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.AccountUnavailable,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationLogin,
				cancellationToken);

			return AccountServiceResult<LoginResponse>.Rejected(
				AccountReasonCodes.AccountUnavailable,
				Globals.accountLoginCouldNotBeCompleted);
		}

		// Password comparison stays inside the hasher so algorithm/version checks remain centralized.
		var passwordAccepted = _passwordHasher.VerifyPassword(
			request.Password,
			account.PasswordHash,
			account.PasswordHashAlgorithm,
			account.PasswordHashVersion);

		if (!passwordAccepted)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.LoginRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCredentials,
				account.AccountId,
				account.AccountId,
				correlationId,
				Globals.accountOperationLogin,
				cancellationToken);

			return AccountServiceResult<LoginResponse>.Rejected(
				AccountReasonCodes.InvalidCredentials,
				Globals.accountLoginCouldNotBeCompleted);
		}

		await _credentials.RecordSuccessfulLoginAsync(account.AccountId, cancellationToken);

		var accountResponse = new AccountResponse(
			account.AccountId,
			account.Username,
			account.Email,
			account.DisplayName,
			account.IsEmailVerified,
			account.AccountStatusName,
			account.CreatedAtUtc,
			account.UpdatedAtUtc,
			DateTime.UtcNow);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.LoginSucceeded,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			account.AccountId,
			account.AccountId,
			correlationId,
			Globals.accountOperationLogin,
			cancellationToken);

		return AccountServiceResult<LoginResponse>.Success(
			new LoginResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				Globals.accountLoginCompleted,
				accountResponse),
			Globals.accountLoginCompleted);
	}
}
