using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Account;

internal static class AccountCodeService
{
	internal static string CreateNumericCode()
	{
		var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
		return value.ToString("D6");
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
        WHERE CodePurpose = N'registration_verification'
          AND DestinationType = N'email'
          AND DestinationNormalized = @DestinationNormalized
        ORDER BY CreatedAtUtc DESC;
        """;

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@DestinationNormalized", normalizedEmail);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		if (!await reader.ReadAsync(cancellationToken))
		{
			return null;
		}

		return new AccountCodeRecord(
			reader.GetGuid(0),
			reader.GetGuid(1),
			(byte[])reader["CodeHash"],
			reader.GetString(3),
			reader.GetInt32(4),
			reader.GetDateTime(5),
			reader.IsDBNull(6) ? null : reader.GetDateTime(6),
			reader.GetInt32(7),
			reader.GetInt32(8));
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
		command.Parameters.AddWithValue("@AccountCodeId", accountCodeId);

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
		command.Parameters.AddWithValue("@AccountCodeId", accountCodeId);

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
            N'password_reset',
            N'email',
            @DestinationNormalized,
            @CodeHash,
            @CodeHashAlgorithm,
            @CodeHashVersion,
            DATEADD(MINUTE, 15, SYSUTCDATETIME()),
            @CorrelationId
        );
        """;

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@DestinationNormalized", destinationNormalized);
		command.Parameters.AddWithValue("@CodeHash", resetHash.PasswordHash);
		command.Parameters.AddWithValue("@CodeHashAlgorithm", resetHash.Algorithm);
		command.Parameters.AddWithValue("@CodeHashVersion", resetHash.Version);
		command.Parameters.AddWithValue("@CorrelationId", correlationId);

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
            N'username_recovery',
            N'email',
            @DestinationNormalized,
            @CodeHash,
            @CodeHashAlgorithm,
            @CodeHashVersion,
            DATEADD(MINUTE, 15, SYSUTCDATETIME()),
            @CorrelationId
        );
        """;

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@DestinationNormalized", normalizedEmail);
		command.Parameters.AddWithValue("@CodeHash", recoveryHash.PasswordHash);
		command.Parameters.AddWithValue("@CodeHashAlgorithm", recoveryHash.Algorithm);
		command.Parameters.AddWithValue("@CodeHashVersion", recoveryHash.Version);
		command.Parameters.AddWithValue("@CorrelationId", correlationId);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	internal static async Task InsertVerificationCodeAsync(
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
            VALUES
            (
                @AccountId,
                N'registration_verification',
                N'email',
                @DestinationNormalized,
                @CodeHash,
                @CodeHashAlgorithm,
                @CodeHashVersion,
                DATEADD(MINUTE, 30, SYSUTCDATETIME()),
                @CorrelationId
            );
            """;

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@DestinationNormalized", normalizedEmail);
		command.Parameters.AddWithValue("@CodeHash", verificationHash.PasswordHash);
		command.Parameters.AddWithValue("@CodeHashAlgorithm", verificationHash.Algorithm);
		command.Parameters.AddWithValue("@CodeHashVersion", verificationHash.Version);
		command.Parameters.AddWithValue("@CorrelationId", correlationId);

		await command.ExecuteNonQueryAsync(cancellationToken);
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
		                   WHERE CodePurpose = N'password_reset'
		                     AND DestinationType = N'email'
		                     AND DestinationNormalized = @DestinationNormalized
		                   ORDER BY CreatedAtUtc DESC;
		                   """;

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@DestinationNormalized", destinationNormalized);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		if (!await reader.ReadAsync(cancellationToken))
		{
			return null;
		}

		return new AccountCodeRecord(
			reader.GetGuid(0),
			reader.GetGuid(1),
			(byte[])reader["CodeHash"],
			reader.GetString(3),
			reader.GetInt32(4),
			reader.GetDateTime(5),
			reader.IsDBNull(6) ? null : reader.GetDateTime(6),
			reader.GetInt32(7),
			reader.GetInt32(8));
	}
}
