// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using PluralBridge.Api;

namespace PluralBridge.Api.Account;

// Small return object for insert paths that need to audit the exact code row created.
// The plaintext code stays out of this shape.
internal sealed record AccountCodeIssueRecord(
	Guid AccountCodeId,
	DateTime ExpiresAtUtc);

// Database helper for account one-time codes. This class owns code rows only:
// creating code text, storing code hashes, selecting code rows, and mutating attempt/consume state.
internal static class AccountCodeService
{
	internal static string CreateNumericCode()
	{
		var value = RandomNumberGenerator.GetInt32(0, 1_000_000);

		return value.ToString(Globals.accountCodeNumericFormat);
	}

	internal static async Task<AccountCodeRecord?> ReadLatestRegistrationCodeAsync(
		SqlConnection connection,
		string normalizedEmail,
		CancellationToken cancellationToken)
	{
		const string sql = """
			SELECT TOP (1)
				AccountCodeId,
				AccountId,
				CodeHash,
				CodeHashAlgorithm,
				CodeHashVersion,
				ExpiresAtUtc,
				ConsumedAtUtc,
				AttemptCount,
				MaxAttempts
			FROM dbo.pb_account_codes
			WHERE CodePurpose = @CodePurpose
			  AND DestinationType = @DestinationType
			  AND DestinationNormalized = @DestinationNormalized
			ORDER BY CreatedAtUtc DESC;
			""";

		await using var command = new SqlCommand(sql, connection);

		AddCodePurposeParameter(command, AccountCodePurposes.RegistrationVerification);
		AddDestinationTypeParameter(command);
		command.Parameters.AddWithValue(Globals.sqlParameterDestinationNormalized, normalizedEmail);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		return await ReadCodeRecordAsync(reader, cancellationToken);
	}

	internal static async Task<AccountCodeRecord?> ReadLatestContactVerificationCodeAsync(
		SqlConnection connection,
		Guid accountId,
		string normalizedEmail,
		CancellationToken cancellationToken)
	{
		const string sql = """
			SELECT TOP (1)
				AccountCodeId,
				AccountId,
				CodeHash,
				CodeHashAlgorithm,
				CodeHashVersion,
				ExpiresAtUtc,
				ConsumedAtUtc,
				AttemptCount,
				MaxAttempts
			FROM dbo.pb_account_codes
			WHERE AccountId = @AccountId
			  AND CodePurpose = @CodePurpose
			  AND DestinationType = @DestinationType
			  AND DestinationNormalized = @DestinationNormalized
			ORDER BY CreatedAtUtc DESC;
			""";

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue(Globals.sqlParameterAccountId, accountId);
		AddCodePurposeParameter(command, AccountCodePurposes.ContactVerification);
		AddDestinationTypeParameter(command);
		command.Parameters.AddWithValue(Globals.sqlParameterDestinationNormalized, normalizedEmail);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		return await ReadCodeRecordAsync(reader, cancellationToken);
	}

	internal static async Task IncrementCodeAttemptAsync(
		SqlConnection connection,
		Guid accountCodeId,
		CancellationToken cancellationToken)
	{
		const string sql = """
			UPDATE dbo.pb_account_codes
			SET
				AttemptCount = AttemptCount + 1,
				LastAttemptAtUtc = SYSUTCDATETIME()
			WHERE AccountCodeId = @AccountCodeId;
			""";

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue(Globals.sqlParameterAccountCodeId, accountCodeId);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	internal static async Task ConsumeCodeAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountCodeId,
		CancellationToken cancellationToken)
	{
		const string sql = """
			UPDATE dbo.pb_account_codes
			SET
				ConsumedAtUtc = SYSUTCDATETIME(),
				LastAttemptAtUtc = SYSUTCDATETIME()
			WHERE AccountCodeId = @AccountCodeId
			  AND ConsumedAtUtc IS NULL;
			""";

		await using var command = new SqlCommand(sql, connection, transaction);

		command.Parameters.AddWithValue(Globals.sqlParameterAccountCodeId, accountCodeId);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	internal static async Task InsertPasswordResetCodeAsync(
		SqlConnection connection,
		Guid accountId,
		string destinationNormalized,
		PasswordHashResult resetHash,
		string correlationId,
		CancellationToken cancellationToken)
	{
		const string sql = """
			INSERT INTO dbo.pb_account_codes
			(
				AccountId,
				CodePurpose,
				DestinationType,
				DestinationNormalized,
				CodeHash,
				CodeHashAlgorithm,
				CodeHashVersion,
				ExpiresAtUtc,
				CorrelationId
			)
			VALUES
			(
				@AccountId,
				@CodePurpose,
				@DestinationType,
				@DestinationNormalized,
				@CodeHash,
				@CodeHashAlgorithm,
				@CodeHashVersion,
				DATEADD(MINUTE, @ExpirationMinutes, SYSUTCDATETIME()),
				@CorrelationId
			);
			""";

		await using var command = new SqlCommand(sql, connection);

		AddCodeInsertParameters(
			command,
			accountId,
			AccountCodePurposes.PasswordReset,
			destinationNormalized,
			resetHash,
			correlationId);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	internal static async Task InsertUsernameRecoveryCodeAsync(
		SqlConnection connection,
		Guid accountId,
		string normalizedEmail,
		PasswordHashResult recoveryHash,
		string correlationId,
		CancellationToken cancellationToken)
	{
		const string sql = """
			INSERT INTO dbo.pb_account_codes
			(
				AccountId,
				CodePurpose,
				DestinationType,
				DestinationNormalized,
				CodeHash,
				CodeHashAlgorithm,
				CodeHashVersion,
				ExpiresAtUtc,
				CorrelationId
			)
			VALUES
			(
				@AccountId,
				@CodePurpose,
				@DestinationType,
				@DestinationNormalized,
				@CodeHash,
				@CodeHashAlgorithm,
				@CodeHashVersion,
				DATEADD(MINUTE, @ExpirationMinutes, SYSUTCDATETIME()),
				@CorrelationId
			);
			""";

		await using var command = new SqlCommand(sql, connection);

		AddCodeInsertParameters(
			command,
			accountId,
			AccountCodePurposes.UsernameRecovery,
			normalizedEmail,
			recoveryHash,
			correlationId);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	internal static async Task InsertEmailChangeVerificationCodeAsync(
		SqlConnection connection,
		Guid accountId,
		string destinationNormalized,
		PasswordHashResult verificationHash,
		string correlationId,
		CancellationToken cancellationToken)
	{
		const string sql = """
			INSERT INTO dbo.pb_account_codes
			(
				AccountId,
				CodePurpose,
				DestinationType,
				DestinationNormalized,
				CodeHash,
				CodeHashAlgorithm,
				CodeHashVersion,
				ExpiresAtUtc,
				CorrelationId
			)
			VALUES
			(
				@AccountId,
				@CodePurpose,
				@DestinationType,
				@DestinationNormalized,
				@CodeHash,
				@CodeHashAlgorithm,
				@CodeHashVersion,
				DATEADD(MINUTE, @ExpirationMinutes, SYSUTCDATETIME()),
				@CorrelationId
			);
			""";

		await using var command = new SqlCommand(sql, connection);

		AddCodeInsertParameters(
			command,
			accountId,
			AccountCodePurposes.ContactVerification,
			destinationNormalized,
			verificationHash,
			correlationId);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	internal static async Task<AccountCodeIssueRecord> InsertVerificationCodeAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		string normalizedEmail,
		PasswordHashResult verificationHash,
		string correlationId,
		CancellationToken cancellationToken)
	{
		const string sql = """
			INSERT INTO dbo.pb_account_codes
			(
				AccountId,
				CodePurpose,
				DestinationType,
				DestinationNormalized,
				CodeHash,
				CodeHashAlgorithm,
				CodeHashVersion,
				ExpiresAtUtc,
				CorrelationId
			)
			OUTPUT
				INSERTED.AccountCodeId,
				INSERTED.ExpiresAtUtc
			VALUES
			(
				@AccountId,
				@CodePurpose,
				@DestinationType,
				@DestinationNormalized,
				@CodeHash,
				@CodeHashAlgorithm,
				@CodeHashVersion,
				DATEADD(MINUTE, @ExpirationMinutes, SYSUTCDATETIME()),
				@CorrelationId
			);
			""";

		await using var command = new SqlCommand(sql, connection, transaction);

		AddCodeInsertParameters(
			command,
			accountId,
			AccountCodePurposes.RegistrationVerification,
			normalizedEmail,
			verificationHash,
			correlationId);

		// The registration path needs the inserted id and expiry for SafeDetailJson.
		// OUTPUT gives us the database-assigned values without a second lookup.
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		if (!await reader.ReadAsync(cancellationToken))
		{
			throw new InvalidOperationException(Globals.accountCodeInsertReturnedNoRows);
		}

		return new AccountCodeIssueRecord(
			reader.GetGuid(0),
			reader.GetDateTime(1));
	}

	internal static async Task<AccountCodeRecord?> ReadLatestPasswordResetCodeAsync(
		SqlConnection connection,
		string destinationNormalized,
		CancellationToken cancellationToken)
	{
		const string sql = """
			SELECT TOP (1)
				AccountCodeId,
				AccountId,
				CodeHash,
				CodeHashAlgorithm,
				CodeHashVersion,
				ExpiresAtUtc,
				ConsumedAtUtc,
				AttemptCount,
				MaxAttempts
			FROM dbo.pb_account_codes
			WHERE CodePurpose = @CodePurpose
			  AND DestinationType = @DestinationType
			  AND DestinationNormalized = @DestinationNormalized
			ORDER BY CreatedAtUtc DESC;
			""";

		await using var command = new SqlCommand(sql, connection);

		AddCodePurposeParameter(command, AccountCodePurposes.PasswordReset);
		AddDestinationTypeParameter(command);
		command.Parameters.AddWithValue(Globals.sqlParameterDestinationNormalized, destinationNormalized);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		return await ReadCodeRecordAsync(reader, cancellationToken);
	}

	private static void AddCodeInsertParameters(
		SqlCommand command,
		Guid accountId,
		string codePurpose,
		string destinationNormalized,
		PasswordHashResult codeHash,
		string correlationId)
	{
		// All code insert paths share the same destination type and expiry policy.
		// The purpose decides which workflow will later be allowed to consume the row.
		command.Parameters.AddWithValue(Globals.sqlParameterAccountId, accountId);
		AddCodePurposeParameter(command, codePurpose);
		AddDestinationTypeParameter(command);
		command.Parameters.AddWithValue(Globals.sqlParameterDestinationNormalized, destinationNormalized);
		command.Parameters.AddWithValue(Globals.sqlParameterCodeHash, codeHash.PasswordHash);
		command.Parameters.AddWithValue(Globals.sqlParameterCodeHashAlgorithm, codeHash.Algorithm);
		command.Parameters.AddWithValue(Globals.sqlParameterCodeHashVersion, codeHash.Version);
		command.Parameters.AddWithValue(Globals.sqlParameterExpirationMinutes, Globals.accountCodeExpirationMinutes);
		command.Parameters.AddWithValue(Globals.sqlParameterCorrelationId, correlationId);
	}

	private static void AddCodePurposeParameter(SqlCommand command, string codePurpose)
	{
		command.Parameters.AddWithValue(Globals.sqlParameterCodePurpose, codePurpose);
	}

	private static void AddDestinationTypeParameter(SqlCommand command)
	{
		command.Parameters.AddWithValue(Globals.sqlParameterDestinationType, AccountDestinationTypes.Email);
	}

	private static async Task<AccountCodeRecord?> ReadCodeRecordAsync(
		SqlDataReader reader,
		CancellationToken cancellationToken)
	{
		// A missing row is a normal verification outcome; the caller decides which generic
		// public response and which audit event fit the workflow.
		if (!await reader.ReadAsync(cancellationToken))
		{
			return null;
		}

		return new AccountCodeRecord(
			reader.GetGuid(0),
			reader.GetGuid(1),
			(byte[])reader[Globals.accountCodeHashFieldName],
			reader.GetString(3),
			reader.GetInt32(4),
			reader.GetDateTime(5),
			reader.IsDBNull(6) ? null : reader.GetDateTime(6),
			reader.GetInt32(7),
			reader.GetInt32(8));
	}
}
