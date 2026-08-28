// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Account;

public sealed partial class AccountService
{
	public async Task<AccountServiceResult<AccountOperationResponse>> ForgotUsernameAsync(
			ForgotUsernameRequest request,
			CancellationToken cancellationToken)
	{
		var correlationId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);

		// Username recovery is account-enumeration resistant. Unknown email addresses receive the
		// same public success message as known addresses.
		if (!AccountText.HasText(request.Email))
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.UsernameRecoveryRejected,
				AccountOutcomes.Rejected,
				AccountReasonCodes.ValidationFailed,
				null,
				null,
				correlationId,
				Globals.accountOperationUsernameRecovery,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Rejected(
				AccountReasonCodes.ValidationFailed,
				Globals.accountUsernameRecoveryInstructionsSent);
		}

		var normalizedEmail = AccountText.NormalizeEmail(request.Email);

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		var accountId = await AccountRepository.ReadAccountIdByNormalizedEmailAsync(
			connection,
			normalizedEmail,
			cancellationToken);

		if (accountId is null)
		{
			await AccountInfrastructure.WriteAuditAsync(_auditWriter,
				AccountAuditEvents.UsernameRecoveryRequested,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				null,
				null,
				correlationId,
				Globals.accountOperationUsernameRecovery,
				cancellationToken);

			return AccountServiceResult<AccountOperationResponse>.Success(
				new AccountOperationResponse(
					true,
					AccountOutcomes.Succeeded,
					AccountReasonCodes.None,
					Globals.accountUsernameRecoveryInstructionsSent),
				Globals.accountUsernameRecoveryInstructionsSent);
		}

		var recoveryCode = AccountCodeService.CreateNumericCode();
		var recoveryHash = _passwordHasher.HashPassword(recoveryCode);

		// Store the recovery code hash first, then deliver the plaintext code through the provider.
		await AccountCodeService.InsertUsernameRecoveryCodeAsync(
			connection,
			accountId.Value,
			normalizedEmail,
			recoveryHash,
			correlationId,
			cancellationToken);

		var usernameRecoveryCodeDelivered = await TryDeliverCodeAsync(
			new AccountCodeDeliveryCommand(
				accountId.Value,
				AccountCodePurposes.UsernameRecovery,
				AccountDestinationTypes.Email,
				normalizedEmail,
				recoveryCode,
				correlationId),
			AccountAuditEvents.UsernameRecoveryRejected,
			Globals.accountOperationUsernameRecovery,
			accountId.Value,
			correlationId,
			cancellationToken);

		if (!usernameRecoveryCodeDelivered)
		{
			return AccountServiceResult<AccountOperationResponse>.Failed(
				AccountReasonCodes.DeliveryFailed,
				Globals.accountUsernameRecoveryInstructionsSent);
		}

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.UsernameRecoveryRequested,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			accountId.Value,
			accountId.Value,
			correlationId,
			Globals.accountOperationUsernameRecovery,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.UsernameRecoveryIssued,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			accountId.Value,
			accountId.Value,
			correlationId,
			Globals.accountOperationUsernameRecovery,
			cancellationToken);

		await AccountInfrastructure.WriteAuditAsync(_auditWriter,
			AccountAuditEvents.CodeIssued,
			AccountOutcomes.Succeeded,
			AccountReasonCodes.None,
			accountId.Value,
			accountId.Value,
			correlationId,
			Globals.accountOperationUsernameRecovery,
			cancellationToken);

		return AccountServiceResult<AccountOperationResponse>.Success(
			new AccountOperationResponse(
				true,
				AccountOutcomes.Succeeded,
				AccountReasonCodes.None,
				Globals.accountUsernameRecoveryInstructionsSent),
				Globals.accountUsernameRecoveryInstructionsSent);
	}
}
