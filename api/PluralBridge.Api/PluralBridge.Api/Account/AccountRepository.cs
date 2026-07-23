using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Account;

internal static class AccountRepository
{
	internal static async Task<bool> AccountIdentifierExistsAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		string normalizedUsername,
		string normalizedEmail,
		CancellationToken cancellationToken)
	{
		const string sql = """
            SELECT TOP (1) 1
            FROM dbo.pb_accounts
            WHERE NormalizedUsername = @NormalizedUsername
               OR NormalizedEmail = @NormalizedEmail;
            """;

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.AddWithValue("@NormalizedUsername", normalizedUsername);
		command.Parameters.AddWithValue("@NormalizedEmail", normalizedEmail);

		var result = await command.ExecuteScalarAsync(cancellationToken);

		return result is not null;
	}

	internal static async Task<LoginAccountRecord?> ReadLoginAccountAsync(
		SqlConnection connection,
		string normalizedIdentifier,
		CancellationToken cancellationToken)
	{
		const string sql = """
        SELECT TOP (1)
            a.AccountId,
            a.Username,
            a.Email,
            a.DisplayName,
            a.AccountStatusId,
            s.StatusName,
            a.IsEmailVerified,
            a.CreatedAtUtc,
            a.UpdatedAtUtc,
            a.LastLoginAtUtc,
            c.PasswordHash,
            c.PasswordHashAlgorithm,
            c.PasswordHashVersion
        FROM dbo.pb_accounts a
        INNER JOIN dbo.pb_account_statuses s
            ON s.AccountStatusId = a.AccountStatusId
        INNER JOIN dbo.pb_account_credentials c
            ON c.AccountId = a.AccountId
        WHERE a.NormalizedUsername = @NormalizedIdentifier
           OR a.NormalizedEmail = @NormalizedIdentifier;
        """;

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@NormalizedIdentifier", normalizedIdentifier);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		if (!await reader.ReadAsync(cancellationToken))
		{
			return null;
		}

		return new LoginAccountRecord(
			reader.GetGuid(0),
			reader.GetString(1),
			reader.GetString(2),
			reader.GetString(3),
			reader.GetInt32(4),
			reader.GetString(5),
			reader.GetBoolean(6),
			reader.GetDateTime(7),
			reader.IsDBNull(8) ? null : reader.GetDateTime(8),
			reader.IsDBNull(9) ? null : reader.GetDateTime(9),
			(byte[])reader["PasswordHash"],
			reader.GetString(11),
			reader.GetInt32(12));
	}

	internal static async Task UpdateLastLoginAsync(
		SqlConnection connection,
		Guid accountId,
		CancellationToken cancellationToken)
	{
		const string sql = """
        UPDATE dbo.pb_accounts
        SET
            LastLoginAtUtc = SYSUTCDATETIME(),
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE AccountId = @AccountId;
        """;

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@AccountId", accountId);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	internal static async Task<Guid?> ReadAccountIdByNormalizedEmailAsync(
		SqlConnection connection,
		string normalizedEmail,
		CancellationToken cancellationToken)
	{
		const string sql = """
        SELECT TOP (1)
            AccountId
        FROM dbo.pb_accounts
        WHERE NormalizedEmail = @NormalizedEmail
          AND AccountStatusId IN (1, 2);
        """;

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@NormalizedEmail", normalizedEmail);

		var result = await command.ExecuteScalarAsync(cancellationToken);

		return result is Guid accountId
			? accountId
			: null;
	}

	internal static async Task<Guid?> ReadAccountIdByNormalizedIdentifierForPasswordResetAsync(
		SqlConnection connection,
		string normalizedIdentifier,
		CancellationToken cancellationToken)
	{
		const string sql = """
        SELECT TOP (1)
            AccountId
        FROM dbo.pb_accounts
        WHERE AccountStatusId = 1
          AND IsEmailVerified = 1
          AND
          (
              NormalizedUsername = @NormalizedIdentifier
              OR NormalizedEmail = @NormalizedIdentifier
          );
        """;

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@NormalizedIdentifier", normalizedIdentifier);

		var result = await command.ExecuteScalarAsync(cancellationToken);

		return result is Guid accountId
			? accountId
			: null;
	}

	internal static async Task<LoginAccountRecord?> ReadPasswordChangeAccountAsync(
		SqlConnection connection,
		Guid accountId,
		CancellationToken cancellationToken)
	{
		const string sql = """
		                   SELECT TOP (1)
		                       a.AccountId,
		                       a.Username,
		                       a.Email,
		                       a.DisplayName,
		                       a.AccountStatusId,
		                       s.StatusName,
		                       a.IsEmailVerified,
		                       a.CreatedAtUtc,
		                       a.UpdatedAtUtc,
		                       a.LastLoginAtUtc,
		                       c.PasswordHash,
		                       c.PasswordHashAlgorithm,
		                       c.PasswordHashVersion
		                   FROM dbo.pb_accounts a
		                   INNER JOIN dbo.pb_account_statuses s
		                       ON s.AccountStatusId = a.AccountStatusId
		                   INNER JOIN dbo.pb_account_credentials c
		                       ON c.AccountId = a.AccountId
		                   WHERE a.AccountId = @AccountId;
		                   """;

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@AccountId", accountId);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		if (!await reader.ReadAsync(cancellationToken))
		{
			return null;
		}

		return new LoginAccountRecord(
			reader.GetGuid(0),
			reader.GetString(1),
			reader.GetString(2),
			reader.GetString(3),
			reader.GetInt32(4),
			reader.GetString(5),
			reader.GetBoolean(6),
			reader.GetDateTime(7),
			reader.IsDBNull(8) ? null : reader.GetDateTime(8),
			reader.IsDBNull(9) ? null : reader.GetDateTime(9),
			(byte[])reader["PasswordHash"],
			reader.GetString(11),
			reader.GetInt32(12));
	}

	internal static async Task InsertAccountAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		string username,
		string normalizedUsername,
		string email,
		string normalizedEmail,
		string displayName,
		int pendingEmailVerificationStatusId,
		CancellationToken cancellationToken)
	{
		const string sql = """
            INSERT INTO dbo.pb_accounts
            (
                AccountId,
                Email,
                DisplayName,
                AccountStatusId,
                Username,
                NormalizedUsername,
                NormalizedEmail,
                IsEmailVerified
            )
            VALUES
            (
                @AccountId,
                @Email,
                @DisplayName,
                @AccountStatusId,
                @Username,
                @NormalizedUsername,
                @NormalizedEmail,
                0
            );
            """;

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@Email", email);
		command.Parameters.AddWithValue("@DisplayName", displayName);
		command.Parameters.AddWithValue("@AccountStatusId", pendingEmailVerificationStatusId);
		command.Parameters.AddWithValue("@Username", username);
		command.Parameters.AddWithValue("@NormalizedUsername", normalizedUsername);
		command.Parameters.AddWithValue("@NormalizedEmail", normalizedEmail);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	internal static async Task InsertCredentialAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		PasswordHashResult passwordHash,
		CancellationToken cancellationToken)
	{
		const string sql = """
            INSERT INTO dbo.pb_account_credentials
            (
                AccountId,
                PasswordHash,
                PasswordHashAlgorithm,
                PasswordHashVersion
            )
            VALUES
            (
                @AccountId,
                @PasswordHash,
                @PasswordHashAlgorithm,
                @PasswordHashVersion
            );
            """;

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@PasswordHash", passwordHash.PasswordHash);
		command.Parameters.AddWithValue("@PasswordHashAlgorithm", passwordHash.Algorithm);
		command.Parameters.AddWithValue("@PasswordHashVersion", passwordHash.Version);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	internal static async Task UpdatePasswordCredentialAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		PasswordHashResult passwordHash,
		CancellationToken cancellationToken)
	{
		const string sql = """
		                   UPDATE dbo.pb_account_credentials
		                   SET
		                       PasswordHash = @PasswordHash,
		                       PasswordHashAlgorithm = @PasswordHashAlgorithm,
		                       PasswordHashVersion = @PasswordHashVersion,
		                       PasswordChangedAtUtc = SYSUTCDATETIME(),
		                       UpdatedAtUtc = SYSUTCDATETIME()
		                   WHERE AccountId = @AccountId;
		                   """;

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@PasswordHash", passwordHash.PasswordHash);
		command.Parameters.AddWithValue("@PasswordHashAlgorithm", passwordHash.Algorithm);
		command.Parameters.AddWithValue("@PasswordHashVersion", passwordHash.Version);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	internal static async Task ActivateAccountAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		CancellationToken cancellationToken)
	{
		const string sql = """
        UPDATE dbo.pb_accounts
        SET
            AccountStatusId = 1,
            IsEmailVerified = 1,
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE AccountId = @AccountId
          AND AccountStatusId = 2;
        """;

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.AddWithValue("@AccountId", accountId);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}
}
