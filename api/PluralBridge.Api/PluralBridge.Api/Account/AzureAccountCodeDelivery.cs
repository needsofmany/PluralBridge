// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Azure;
using Azure.Communication.Email;

namespace PluralBridge.Api.Account;

public sealed class AzureAccountCodeDelivery(IConfiguration configuration, ILogger<AzureAccountCodeDelivery> logger) : IAccountCodeDelivery
{
	private const string ConfigurationSectionName = "AccountCodeDelivery:AzureCommunicationServices";

	private readonly string _connectionString = configuration[$"{ConfigurationSectionName}:ConnectionString"]
		?? throw new InvalidOperationException($"{ConfigurationSectionName}:ConnectionString is not configured.");

	private readonly string _fromAddress = configuration[$"{ConfigurationSectionName}:FromAddress"]
		?? throw new InvalidOperationException($"{ConfigurationSectionName}:FromAddress is not configured.");

	public async Task DeliverAsync(
		AccountCodeDeliveryCommand command,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(command);

		if (!StringComparer.OrdinalIgnoreCase.Equals(command.DestinationType, AccountDestinationTypes.Email))
		{
			throw new InvalidOperationException($"Unsupported account code delivery destination type: {command.DestinationType}");
		}

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
			var operation = await emailClient.SendAsync(
				WaitUntil.Completed,
				message,
				cancellationToken);

			logger.LogInformation(
				"Account code email sent. AccountId={AccountId} CodePurpose={CodePurpose} DestinationType={DestinationType} CorrelationId={CorrelationId} OperationId={OperationId}",
				command.AccountId,
				command.CodePurpose,
				command.DestinationType,
				command.CorrelationId,
				operation.Id);
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Account code email delivery failed. AccountId={AccountId} CodePurpose={CodePurpose} DestinationType={DestinationType} CorrelationId={CorrelationId}",
				command.AccountId,
				command.CodePurpose,
				command.DestinationType,
				command.CorrelationId);

			throw;
		}
	}

	private static string CreateSubject(string codePurpose)
	{
		return codePurpose switch
		{
			AccountCodePurposes.RegistrationVerification => "PluralBridge verification code",
			AccountCodePurposes.UsernameRecovery => "PluralBridge username recovery code",
			AccountCodePurposes.PasswordReset => "PluralBridge password reset code",
			AccountCodePurposes.ContactVerification => "PluralBridge contact verification code",
			_ => "PluralBridge account code"
		};
	}

	private static string CreatePlainTextBody(AccountCodeDeliveryCommand command)
	{
		var action = CreateActionDescription(command.CodePurpose);

		return $"""
			Your PluralBridge {action} code is:

			{command.PlaintextCode}

			This code expires in 15 minutes.

			If you did not request this code, you can ignore this message.
			""";
	}

	private static string CreateHtmlBody(AccountCodeDeliveryCommand command)
	{
		var action = CreateActionDescription(command.CodePurpose);

		return $"""
			<p>Your PluralBridge {action} code is:</p>
			<p><strong style="font-size: 1.4em; letter-spacing: 0.08em;">{command.PlaintextCode}</strong></p>
			<p>This code expires in 15 minutes.</p>
			<p>If you did not request this code, you can ignore this message.</p>
			""";
	}

	private static string CreateActionDescription(string codePurpose)
	{
		return codePurpose switch
		{
			AccountCodePurposes.RegistrationVerification => "verification",
			AccountCodePurposes.UsernameRecovery => "username recovery",
			AccountCodePurposes.PasswordReset => "password reset",
			AccountCodePurposes.ContactVerification => "contact verification",
			_ => "account"
		};
	}
}
