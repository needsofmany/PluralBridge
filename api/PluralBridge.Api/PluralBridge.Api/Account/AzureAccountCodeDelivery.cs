// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Azure;
using Azure.Communication.Email;

namespace PluralBridge.Api.Account;

// Sends account one-time codes through Azure Communication Services.
// AccountService owns the workflow decision; this class owns only provider delivery.
public sealed class AzureAccountCodeDelivery(IConfiguration configuration, ILogger<AzureAccountCodeDelivery> logger) : IAccountCodeDelivery
{
	// Keep these nullable so missing configuration is reported during delivery,
	// where AccountService can convert the failure into a controlled API result.
	private readonly string? _connectionString = configuration[
		$"{Globals.accountCodeDeliveryAzureConfigurationSection}:{Globals.accountCodeDeliveryConnectionStringKey}"];

	private readonly string? _fromAddress = configuration[
		$"{Globals.accountCodeDeliveryAzureConfigurationSection}:{Globals.accountCodeDeliveryFromAddressKey}"];

	public async Task DeliverAsync(
		AccountCodeDeliveryCommand command,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(command);

		// This provider supports email only. Other destination types need a different delivery provider.
		if (!StringComparer.OrdinalIgnoreCase.Equals(command.DestinationType, AccountDestinationTypes.Email))
		{
			throw new InvalidOperationException($"{Globals.unsupportedAccountCodeDeliveryDestinationType} {command.DestinationType}");
		}

		// Configuration errors should be loud in logs but still flow back through AccountService
		// as delivery_failed, rather than crashing startup or tearing down the request.
		if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(_fromAddress))
		{
			logger.LogError(
				Globals.azureAccountCodeEmailDeliveryNotConfiguredLog,
				string.IsNullOrWhiteSpace(_connectionString),
				string.IsNullOrWhiteSpace(_fromAddress),
				command.AccountId,
				command.CodePurpose,
				command.CorrelationId);

			throw new InvalidOperationException(Globals.azureAccountCodeEmailDeliveryNotConfigured);
		}

		// Build the Azure SDK message from the provider-neutral delivery command.
		var emailClient = new EmailClient(_connectionString);
		var subject = CreateSubject(command.CodePurpose);
		var plainTextBody = CreatePlainTextBody(command);
		var htmlBody = CreateHtmlBody(command);

		var content = new EmailContent(subject)
		{
			PlainText = plainTextBody,
			Html = htmlBody
		};

		var recipients = new EmailRecipients(
		[
			new EmailAddress(command.DestinationNormalized)
		]);

		var message = new EmailMessage(
			senderAddress: _fromAddress,
			recipients: recipients,
			content: content);

		try
		{
			// WaitUntil.Completed proves Azure accepted and completed the send operation before
			// the API returns. This is slower, but it gives a clean proof during account testing.
			var operation = await emailClient.SendAsync(
				WaitUntil.Completed,
				message,
				cancellationToken);

			logger.LogInformation(
				Globals.accountCodeEmailSentLog,
				command.AccountId,
				command.CodePurpose,
				command.DestinationType,
				command.CorrelationId,
				operation.Id);
		}
		catch (Exception ex)
		{
			// Preserve provider exception detail in logs, then rethrow so AccountService can
			// write the account audit failure and return a controlled result.
			logger.LogError(
				ex,
				Globals.accountCodeEmailDeliveryFailedLog,
				command.AccountId,
				command.CodePurpose,
				command.DestinationType,
				command.CorrelationId);

			throw;
		}
	}

	private static string CreateSubject(string codePurpose)
	{
		// Subjects are purpose-specific so recipients can tell which account flow generated the code.
		return codePurpose switch
		{
			AccountCodePurposes.RegistrationVerification => Globals.accountCodeEmailSubjectRegistrationVerification,
			AccountCodePurposes.UsernameRecovery => Globals.accountCodeEmailSubjectUsernameRecovery,
			AccountCodePurposes.PasswordReset => Globals.accountCodeEmailSubjectPasswordReset,
			AccountCodePurposes.ContactVerification => Globals.accountCodeEmailSubjectContactVerification,
			_ => Globals.accountCodeEmailSubjectDefault
		};
	}

	private static string CreatePlainTextBody(AccountCodeDeliveryCommand command)
	{
		var action = CreateActionDescription(command.CodePurpose);

		// Plain text is the accessibility and fallback body. Do not add secret diagnostics here.
		return $"""
			Your PluralBridge {action} code is:

			{command.PlaintextCode}

			{Globals.accountCodeEmailExpiresLine}

			{Globals.accountCodeEmailIgnoreLine}
			""";
	}

	private static string CreateHtmlBody(AccountCodeDeliveryCommand command)
	{
		var action = CreateActionDescription(command.CodePurpose);

		// HTML mirrors the plain text body, with only light formatting around the code.
		return $"""
			<p>Your PluralBridge {action} code is:</p>
			<p><strong style="font-size: 1.4em; letter-spacing: 0.08em;">{command.PlaintextCode}</strong></p>
			<p>{Globals.accountCodeEmailExpiresLine}</p>
			<p>{Globals.accountCodeEmailIgnoreLine}</p>
			""";
	}

	private static string CreateActionDescription(string codePurpose)
	{
		// The action word is embedded in both body formats; keep this aligned with CreateSubject.
		return codePurpose switch
		{
			AccountCodePurposes.RegistrationVerification => Globals.accountCodeEmailActionVerification,
			AccountCodePurposes.UsernameRecovery => Globals.accountCodeEmailActionUsernameRecovery,
			AccountCodePurposes.PasswordReset => Globals.accountCodeEmailActionPasswordReset,
			AccountCodePurposes.ContactVerification => Globals.accountCodeEmailActionContactVerification,
			_ => Globals.accountCodeEmailActionDefault
		};
	}
}
