// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;

namespace PluralBridge.Api.Account;

public sealed partial class AccountService
{
	private async Task<bool> TryDeliverCodeAsync(
		AccountCodeDeliveryCommand command,
		string failureAuditEventName,
		string operationName,
		Guid accountId,
		string correlationId,
		CancellationToken cancellationToken)
	{
		try
		{
			// Provider-specific delivery exceptions are contained here so workflows can return a
			// controlled service result and still write a failure audit row.
			await _codeDelivery.DeliverAsync(command, cancellationToken);

			return true;
		}
		catch
		{
			await AccountInfrastructure.WriteAuditAsync(
				_auditWriter,
				failureAuditEventName,
				AccountOutcomes.Failed,
				AccountReasonCodes.DeliveryFailed,
				accountId,
				accountId,
				correlationId,
				operationName,
				JsonSerializer.Serialize(new
				{
					codePurpose = command.CodePurpose,
					destinationType = command.DestinationType,
					destinationNormalized = command.DestinationNormalized
				}),
				cancellationToken);

			return false;
		}
	}
}
