// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Account;

public static class AccountCodePurposes
{
	public const string RegistrationVerification = "registration_verification";
	public const string UsernameRecovery = "username_recovery";
	public const string PasswordReset = "password_reset";
	public const string ContactVerification = "contact_verification";
}

public static class AccountDestinationTypes
{
	public const string Email = "email";
}

public sealed record AccountCodeDeliveryCommand(
	Guid AccountId,
	string CodePurpose,
	string DestinationType,
	string DestinationNormalized,
	string PlaintextCode,
	string CorrelationId);

public interface IAccountCodeDelivery
{
	Task DeliverAsync(
		AccountCodeDeliveryCommand command,
		CancellationToken cancellationToken);
}

public sealed class DevelopmentAccountCodeDelivery(IConfiguration configuration) : IAccountCodeDelivery
{
	private readonly string _connectionString = configuration.GetConnectionString(AccountConfigurationKeys.ConnectionStringName)
		?? throw new InvalidOperationException($"{AccountConfigurationKeys.ConnectionStringName} is not configured.");

	public async Task DeliverAsync(
		AccountCodeDeliveryCommand command,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(command);

		const string sql = """
			INSERT INTO dbo.pb_account_code_delivery_outbox
			(
				OutboxId,
				AccountId,
				CodePurpose,
				DestinationType,
				DestinationNormalized,
				PlaintextCode,
				CorrelationId
			)
			VALUES
			(
				NEWID(),
				@AccountId,
				@CodePurpose,
				@DestinationType,
				@DestinationNormalized,
				@PlaintextCode,
				@CorrelationId
			);
			""";

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		await using var sqlCommand = new SqlCommand(sql, connection);

		sqlCommand.Parameters.AddWithValue("@AccountId", command.AccountId);
		sqlCommand.Parameters.AddWithValue("@CodePurpose", command.CodePurpose);
		sqlCommand.Parameters.AddWithValue("@DestinationType", command.DestinationType);
		sqlCommand.Parameters.AddWithValue("@DestinationNormalized", command.DestinationNormalized);
		sqlCommand.Parameters.AddWithValue("@PlaintextCode", command.PlaintextCode);
		sqlCommand.Parameters.AddWithValue("@CorrelationId", command.CorrelationId);

		await sqlCommand.ExecuteNonQueryAsync(cancellationToken);
	}
}

public sealed class DisabledAccountCodeDelivery : IAccountCodeDelivery
{
	public Task DeliverAsync(
		AccountCodeDeliveryCommand command,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(command);

		return Task.CompletedTask;
	}
}
