// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Account;

public sealed partial class AccountService
{
	/// <summary>
	/// Chooses the next screen from the stored account.
	/// A verified active account selects password. A pending unverified account selects the code. An identifier that matches nothing selects registration and does not create the account.
	/// Disabled, locked, deleted, and ambiguous matches keep the shared unsuccessful result. This method does not send mail or issue a session. It has no request rate limit yet.
	/// </summary>
	/// <param name="request">The username or email typed on the first screen.</param>
	/// <param name="cancellationToken">Cancels the lookup.</param>
	/// <returns>The password step, the verification step, the registration step, or the shared unsuccessful result.</returns>
	public async Task<AccountServiceResult<LoginContinuationResponse>> ContinueLoginAsync(
		LoginContinuationRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);
		if (!AccountText.HasText(request.UsernameOrEmail))
		{
			return await DeclineContinuationAsync(correlationId, cancellationToken);
		}

		var normalizedIdentifier = AccountText.NormalizeEmail(request.UsernameOrEmail);
		var kind = await _registrationGate.ReadContinuationAsync(normalizedIdentifier, cancellationToken);
		if (kind == LoginContinuationKind.Password)
		{
			return AccountServiceResult<LoginContinuationResponse>.Success(
				new LoginContinuationResponse(
					true,
					AccountOutcomes.Succeeded,
					AccountReasonCodes.None,
					Globals.accountLoginContinuationPassword,
					RegistrationContinuation.PasswordStep),
				Globals.accountLoginContinuationPassword);
		}

		if (kind == LoginContinuationKind.VerificationCode)
		{
			return AccountServiceResult<LoginContinuationResponse>.Success(
				new LoginContinuationResponse(
					true,
					AccountOutcomes.Succeeded,
					AccountReasonCodes.None,
					Globals.accountLoginContinuationVerification,
					RegistrationContinuation.VerificationStep),
				Globals.accountLoginContinuationVerification);
		}

		if (kind == LoginContinuationKind.Registration)
		{
			return AccountServiceResult<LoginContinuationResponse>.Success(
				new LoginContinuationResponse(
					true,
					AccountOutcomes.Succeeded,
					AccountReasonCodes.None,
					Globals.accountLoginContinuationRegistration,
					RegistrationContinuation.RegistrationStep),
				Globals.accountLoginContinuationRegistration);
		}

		return await DeclineContinuationAsync(correlationId, cancellationToken);
	}

	/// <summary>
	/// Accepts one registration code. Success activates only a still-pending account and does not sign the caller in.
	/// The gate commits the code consumption and the activation together. An exception rolls that transaction back.
	/// </summary>
	/// <param name="request">The username or email, and the submitted code.</param>
	/// <param name="cancellationToken">Cancels the verification before the gate returns.</param>
	public async Task<AccountServiceResult<AccountOperationResponse>> VerifyRegistrationAsync(
		VerifyRegistrationRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);
		if (!AccountText.HasText(request.Email) || !AccountText.HasText(request.Code))
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.RegistrationVerificationRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				null,
				null,
				correlationId,
				Globals.accountOperationRegistrationVerification,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				Globals.accountCodeCouldNotBeAccepted);
		}

		var normalizedIdentifier = AccountText.NormalizeEmail(request.Email);
		RegistrationAccessDecision decision;
		try
		{
			decision = await _registrationGate.VerifyAsync(
				normalizedIdentifier,
				request.Code.Trim(),
				_passwordHasher,
				cancellationToken);
		}
		catch
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.RegistrationVerificationRejected,
				AccountOutcomes.Failed,
				AccountReasonCodes.StorageFailed,
				null,
				null,
				correlationId,
				Globals.accountOperationRegistrationVerification,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				Globals.accountRequestCouldNotBeCompleted);
		}

		if (decision.Kind == RegistrationAccessDecisionKind.Success)
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.CodeAccepted,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				decision.AccountId,
				decision.AccountId,
				correlationId,
				Globals.accountOperationRegistrationVerification,
				cancellationToken);
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.CodeConsumed,
				AccountOutcomes.Consumed,
				AccountReasonCodes.None,
				decision.AccountId,
				decision.AccountId,
				correlationId,
				Globals.accountOperationRegistrationVerification,
				cancellationToken);
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.RegistrationVerified,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				decision.AccountId,
				decision.AccountId,
				correlationId,
				Globals.accountOperationRegistration,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Success(
				new AccountOperationResponse(
					true,
					AccountOutcomes.Succeeded,
					AccountReasonCodes.None,
					Globals.accountRegistrationVerificationCompleted),
				Globals.accountRegistrationVerificationCompleted);
		}

		var (eventName, outcome, reason, message) = VerificationFailure(decision.Kind);
		await AccountInfrastructure.WriteAuditAsync(
			_auditWriter,
			eventName,
			outcome,
			reason,
			decision.AccountId,
			decision.AccountId,
			correlationId,
			Globals.accountOperationRegistrationVerification,
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Rejected(reason, message);
	}

	/// <summary>
	/// Issues a replacement registration code for a pending account after the cooldown.
	/// Unknown and ineligible identities get the same accepted response and no mail.
	/// Delivery runs after the replacement row is committed and uses the stored email. A delivery failure leaves that new code in place.
	/// </summary>
	/// <param name="request">The username or email of the pending account.</param>
	/// <param name="cancellationToken">Cancels the request before the gate returns.</param>
	public async Task<AccountServiceResult<AccountOperationResponse>> ResendRegistrationCodeAsync(
		ResendRegistrationCodeRequest request,
		CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);
		if (!AccountText.HasText(request.UsernameOrEmail))
		{
			return GenericResendAccepted();
		}

		var normalizedIdentifier = AccountText.NormalizeEmail(request.UsernameOrEmail);
		var plaintextCode = AccountCodeService.CreateNumericCode();
		var codeHash = _passwordHasher.HashPassword(plaintextCode);
		RegistrationAccessDecision decision;
		try
		{
			decision = await _registrationGate.PrepareResendAsync(
				normalizedIdentifier,
				codeHash,
				correlationId,
				cancellationToken);
		}
		catch
		{
			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.StorageFailed,
				Globals.accountRequestCouldNotBeCompleted);
		}

		if (decision.Kind == RegistrationAccessDecisionKind.Cooldown)
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Blocked,
				AccountReasonCodes.RateLimited,
				decision.AccountId,
				decision.AccountId,
				correlationId,
				Globals.accountOperationRegistrationResend,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.RateLimited,
				Globals.accountRegistrationCodeResendCooldown);
		}

		if (decision.Kind != RegistrationAccessDecisionKind.Issued)
		{
			return GenericResendAccepted();
		}

		if (decision.AccountId is null || string.IsNullOrWhiteSpace(decision.DeliveryEmail))
		{
			// The replacement row is already committed. Do not send the typed username in place of a stored email.
			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.DeliveryFailed,
				Globals.accountRegistrationCodeResendDeliveryFailed);
		}

		var delivered = await TryDeliverCodeAsync(
			new AccountCodeDeliveryCommand(
				decision.AccountId.Value,
				AccountCodePurposes.RegistrationVerification,
				AccountDestinationTypes.Email,
				decision.DeliveryEmail,
				plaintextCode,
				correlationId),
			AccountAuditEvents.RegistrationRejected,
			Globals.accountOperationRegistrationResend,
			decision.AccountId.Value,
			correlationId,
			cancellationToken);
		if (!delivered)
		{
			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.DeliveryFailed,
				Globals.accountRegistrationCodeResendDeliveryFailed);
		}

		await AccountInfrastructure.WriteAuditAsync(
			_auditWriter,
			AccountAuditEvents.CodeIssued,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			decision.AccountId,
			decision.AccountId,
			correlationId,
			Globals.accountOperationRegistrationResend,
			cancellationToken);

		return GenericResendAccepted();
	}

	/// <summary>One unsuccessful continuation result. The audit reason does not name the stored status.</summary>
	private async Task<AccountServiceResult<LoginContinuationResponse>> DeclineContinuationAsync(
		string correlationId,
		CancellationToken cancellationToken)
	{
		await AccountInfrastructure.WriteAuditAsync(
			_auditWriter,
			AccountAuditEvents.LoginRejected,
			AccountOutcomes.Rejected,
			AccountReasonCodes.InvalidCredentials,
			null,
			null,
			correlationId,
			Globals.accountOperationLoginContinuation,
			cancellationToken);

		var response = new LoginContinuationResponse(
			false,
			AccountOutcomes.Rejected,
			AccountReasonCodes.InvalidCredentials,
			Globals.accountLoginContinuationDeclined,
			null);
		return AccountServiceResult<LoginContinuationResponse>.Rejected(
			AccountReasonCodes.InvalidCredentials,
			Globals.accountLoginContinuationDeclined) with
		{
			Value = response
		};
	}

	/// <summary>The public resend response for an unknown identity, an ineligible account, and a code that was actually sent.</summary>
	private static AccountServiceResult<AccountOperationResponse> GenericResendAccepted()
	{
		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				Globals.accountRegistrationCodeResendAccepted),
			Globals.accountRegistrationCodeResendAccepted);
	}

	/// <summary>Maps a non-activating verification to an existing audit event and reason. The expired message stays distinct from the other failures.</summary>
	private static (string EventName, string Outcome, string Reason, string Message) VerificationFailure(
		RegistrationAccessDecisionKind kind)
	{
		return kind switch
		{
			RegistrationAccessDecisionKind.Expired => (
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Expired,
				AccountReasonCodes.ExpiredCode,
				Globals.accountCodeExpiredOrCouldNotBeAccepted),
			RegistrationAccessDecisionKind.Consumed => (
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Consumed,
				AccountReasonCodes.ConsumedCode,
				Globals.accountCodeCouldNotBeAccepted),
			RegistrationAccessDecisionKind.RateLimited => (
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Blocked,
				AccountReasonCodes.RateLimited,
				Globals.accountCodeCouldNotBeAccepted),
			_ => (
				AccountAuditEvents.CodeRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.InvalidCode,
				Globals.accountCodeCouldNotBeAccepted)
		};
	}
}
