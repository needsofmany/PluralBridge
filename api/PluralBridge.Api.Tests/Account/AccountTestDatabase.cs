using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Tests.Account;

internal static class AccountTestDatabase
{
	internal static string GetConnectionString()
	{
		using var factory = AccountTestHost.CreateFactory();

		var configuration = factory.Services.GetRequiredService<IConfiguration>();

		return configuration.GetConnectionString(AccountTestGlobals.Database.DefaultConnectionName)
		       ?? throw new InvalidOperationException($"{AccountTestGlobals.Database.DefaultConnectionName} is not configured.");
	}

	internal static async Task CleanupRuntimeTestAccountsAsync()
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   DECLARE @RuntimeAccounts TABLE
		                   (
		                   	AccountId uniqueidentifier NOT NULL PRIMARY KEY
		                   );

		                   INSERT INTO @RuntimeAccounts
		                   (
		                   	AccountId
		                   )
		                   SELECT
		                   	AccountId
		                   FROM dbo.pb_accounts
		                   WHERE NormalizedUsername LIKE 'RUNTIME_TEST_%'
		                   	OR NormalizedEmail LIKE '%@EXAMPLE.TEST';

		                   DELETE auditEvents
		                   FROM dbo.pb_account_audit_events auditEvents
		                   WHERE auditEvents.ActorAccountId IN
		                   	(
		                   		SELECT AccountId
		                   		FROM @RuntimeAccounts
		                   	)
		                   	OR auditEvents.TargetAccountId IN
		                   	(
		                   		SELECT AccountId
		                   		FROM @RuntimeAccounts
		                   	);

		                   DELETE codes
		                   FROM dbo.pb_account_codes codes
		                   WHERE codes.AccountId IN
		                   	(
		                   		SELECT AccountId
		                   		FROM @RuntimeAccounts
		                   	);

		                   DELETE outbox
		                   FROM dbo.pb_account_code_delivery_outbox outbox
		                   WHERE outbox.AccountId IN
		                   	(
		                   		SELECT AccountId
		                   		FROM @RuntimeAccounts
		                   	);
		                   	
		                   DELETE credentials
		                   FROM dbo.pb_account_credentials credentials
		                   WHERE credentials.AccountId IN
		                   	(
		                   		SELECT AccountId
		                   		FROM @RuntimeAccounts
		                   	);

		                   DELETE accounts
		                   FROM dbo.pb_accounts accounts
		                   WHERE accounts.AccountId IN
		                   	(
		                   		SELECT AccountId
		                   		FROM @RuntimeAccounts
		                   	);
		                   """;

		await using var command = new SqlCommand(sql, connection);

		await command.ExecuteNonQueryAsync();
	}

	internal static async Task<RuntimeAccountState?> ReadAccountStateAsync(string username)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   SELECT
		                   	AccountId,
		                   	AccountStatusId,
		                   	IsEmailVerified,
		                   	NormalizedUsername,
		                   	NormalizedEmail
		                   FROM dbo.pb_accounts
		                   WHERE NormalizedUsername = @NormalizedUsername;
		                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@NormalizedUsername", username.ToUpperInvariant());

		await using var reader = await command.ExecuteReaderAsync();

		if (!await reader.ReadAsync())
		{
			return null;
		}

		return new RuntimeAccountState(
			reader.GetGuid(0),
			reader.GetInt32(1),
			reader.GetBoolean(2),
			reader.GetString(3),
			reader.GetString(4));
	}

	internal static async Task<RuntimeAccountCodeState?> ReadLatestAccountCodeStateAsync(
		Guid accountId,
		string codePurpose,
		string destinationType,
		string destinationNormalized)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   SELECT TOP (1)
		                   	AccountCodeId,
		                   	AccountId,
		                   	CodePurpose,
		                   	DestinationType,
		                   	DestinationNormalized,
		                   	CodeHash,
		                   	CodeHashAlgorithm,
		                   	CodeHashVersion
		                   FROM dbo.pb_account_codes
		                   WHERE AccountId = @AccountId
		                   	AND CodePurpose = @CodePurpose
		                   	AND DestinationType = @DestinationType
		                   	AND DestinationNormalized = @DestinationNormalized
		                   ORDER BY CreatedAtUtc DESC;
		                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@CodePurpose", codePurpose);
		command.Parameters.AddWithValue("@DestinationType", destinationType);
		command.Parameters.AddWithValue("@DestinationNormalized", destinationNormalized);

		await using var reader = await command.ExecuteReaderAsync();

		if (!await reader.ReadAsync())
		{
			return null;
		}

		return new RuntimeAccountCodeState(
			reader.GetGuid(0),
			reader.GetGuid(1),
			reader.GetString(2),
			reader.GetString(3),
			reader.GetString(4),
			(byte[])reader["CodeHash"],
			reader.GetString(6),
			reader.GetInt32(7));
	}

	internal sealed record RuntimeAccountCodeState(
		Guid AccountCodeId,
		Guid AccountId,
		string CodePurpose,
		string DestinationType,
		string DestinationNormalized,
		byte[] CodeHash,
		string CodeHashAlgorithm,
		int CodeHashVersion);

	internal static async Task<int> CountCredentialRowsAsync(Guid accountId)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   SELECT COUNT_BIG(1)
		                   FROM dbo.pb_account_credentials
		                   WHERE AccountId = @AccountId;
		                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);

		var result = await command.ExecuteScalarAsync();

		return Convert.ToInt32(result);
	}

	internal sealed record RuntimeAccountState(
		Guid AccountId,
		int AccountStatusId,
		bool IsEmailVerified,
		string NormalizedUsername,
		string NormalizedEmail);

	internal static async Task<int> CountRegistrationVerificationCodeRowsAsync(
		Guid accountId,
		string destinationNormalized)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   SELECT COUNT_BIG(1)
		                   FROM dbo.pb_account_codes
		                   WHERE AccountId = @AccountId
		                   	AND CodePurpose = @CodePurpose
		                   	AND DestinationType = @DestinationType
		                   	AND DestinationNormalized = @DestinationNormalized;
		                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@CodePurpose", AccountTestGlobals.CodePurposes.RegistrationVerification);
		command.Parameters.AddWithValue("@DestinationType", AccountTestGlobals.DestinationTypes.Email);
		command.Parameters.AddWithValue("@DestinationNormalized", destinationNormalized);

		var result = await command.ExecuteScalarAsync();

		return Convert.ToInt32(result);
	}

	internal static async Task<int> CountAccountsByNormalizedUsernameAsync(string normalizedUsername)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   SELECT COUNT_BIG(1)
		                   FROM dbo.pb_accounts
		                   WHERE NormalizedUsername = @NormalizedUsername;
		                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@NormalizedUsername", normalizedUsername);

		var result = await command.ExecuteScalarAsync();

		return Convert.ToInt32(result);
	}

	internal static async Task<int> CountAccountsByNormalizedEmailAsync(string normalizedEmail)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   SELECT COUNT_BIG(1)
		                   FROM dbo.pb_accounts
		                   WHERE NormalizedEmail = @NormalizedEmail;
		                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@NormalizedEmail", normalizedEmail);

		var result = await command.ExecuteScalarAsync();

		return Convert.ToInt32(result);
	}

	internal static async Task<int> CountAuditRowsAsync(Guid accountId, string eventName)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   SELECT COUNT_BIG(1)
		                   FROM dbo.pb_account_audit_events
		                   WHERE TargetAccountId = @AccountId
		                   	AND EventName = @EventName;
		                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@EventName", eventName);

		var result = await command.ExecuteScalarAsync();

		return Convert.ToInt32(result);
	}

	internal static async Task<int> CountRegistrationVerificationCodeRowsByDestinationAsync(string destinationNormalized)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   SELECT COUNT_BIG(1)
		                   FROM dbo.pb_account_codes
		                   WHERE CodePurpose = @CodePurpose
		                   	AND DestinationType = @DestinationType
		                   	AND DestinationNormalized = @DestinationNormalized;
		                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@CodePurpose", AccountTestGlobals.CodePurposes.RegistrationVerification);
		command.Parameters.AddWithValue("@DestinationType", AccountTestGlobals.DestinationTypes.Email);
		command.Parameters.AddWithValue("@DestinationNormalized", destinationNormalized);

		var result = await command.ExecuteScalarAsync();

		return Convert.ToInt32(result);
	}

	internal static async Task<RuntimeCodeDeliveryOutboxState?> ReadLatestCodeDeliveryOutboxStateAsync(
		Guid accountId,
		string codePurpose,
		string destinationType,
		string destinationNormalized)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   SELECT TOP (1)
		                   	OutboxId,
		                   	AccountId,
		                   	CodePurpose,
		                   	DestinationType,
		                   	DestinationNormalized,
		                   	PlaintextCode,
		                   	CorrelationId,
		                   	ConsumedForTestAtUtc
		                   FROM dbo.pb_account_code_delivery_outbox
		                   WHERE AccountId = @AccountId
		                   	AND CodePurpose = @CodePurpose
		                   	AND DestinationType = @DestinationType
		                   	AND DestinationNormalized = @DestinationNormalized
		                   ORDER BY CreatedAtUtc DESC;
		                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@CodePurpose", codePurpose);
		command.Parameters.AddWithValue("@DestinationType", destinationType);
		command.Parameters.AddWithValue("@DestinationNormalized", destinationNormalized);

		await using var reader = await command.ExecuteReaderAsync();

		if (!await reader.ReadAsync())
		{
			return null;
		}

		return new RuntimeCodeDeliveryOutboxState(
			reader.GetGuid(0),
			reader.GetGuid(1),
			reader.GetString(2),
			reader.GetString(3),
			reader.GetString(4),
			reader.GetString(5),
			reader.GetString(6),
			reader.IsDBNull(7) ? null : reader.GetDateTime(7));
	}

	internal sealed record RuntimeCodeDeliveryOutboxState(
		Guid OutboxId,
		Guid AccountId,
		string CodePurpose,
		string DestinationType,
		string DestinationNormalized,
		string PlaintextCode,
		string CorrelationId,
		DateTime? ConsumedForTestAtUtc);

	internal static async Task ActivateRuntimeTestAccountAsync(Guid accountId)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   UPDATE dbo.pb_accounts
		                   SET
		                   	AccountStatusId = 1,
		                   	IsEmailVerified = 1,
		                   	UpdatedAtUtc = SYSUTCDATETIME()
		                   WHERE AccountId = @AccountId;
		                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);

		await command.ExecuteNonQueryAsync();
	}

	internal static async Task<RuntimeCodeConsumptionState?> ReadLatestCodeConsumptionStateAsync(
		Guid accountId,
		string codePurpose,
		string destinationType,
		string destinationNormalized)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   SELECT TOP (1)
		                   	AccountCodeId,
		                   	AccountId,
		                   	CodePurpose,
		                   	DestinationType,
		                   	DestinationNormalized,
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

		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@CodePurpose", codePurpose);
		command.Parameters.AddWithValue("@DestinationType", destinationType);
		command.Parameters.AddWithValue("@DestinationNormalized", destinationNormalized);

		await using var reader = await command.ExecuteReaderAsync();

		if (!await reader.ReadAsync())
		{
			return null;
		}

		return new RuntimeCodeConsumptionState(
			reader.GetGuid(0),
			reader.GetGuid(1),
			reader.GetString(2),
			reader.GetString(3),
			reader.GetString(4),
			reader.IsDBNull(5) ? null : reader.GetDateTime(5),
			reader.GetInt32(6),
			reader.GetInt32(7));
	}

	internal sealed record RuntimeCodeConsumptionState(
		Guid AccountCodeId,
		Guid AccountId,
		string CodePurpose,
		string DestinationType,
		string DestinationNormalized,
		DateTime? ConsumedAtUtc,
		int AttemptCount,
		int MaxAttempts);

	internal static async Task ExpireLatestAccountCodeAsync(
		Guid accountId,
		string codePurpose,
		string destinationType,
		string destinationNormalized)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   WITH LatestCode AS
		                   (
		                   	SELECT TOP (1)
		                   		AccountCodeId
		                   	FROM dbo.pb_account_codes
		                   	WHERE AccountId = @AccountId
		                   		AND CodePurpose = @CodePurpose
		                   		AND DestinationType = @DestinationType
		                   		AND DestinationNormalized = @DestinationNormalized
		                   	ORDER BY CreatedAtUtc DESC
		                   )
		                   UPDATE codes
		                   SET ExpiresAtUtc = DATEADD(minute, -1, SYSUTCDATETIME())
		                   FROM dbo.pb_account_codes codes
		                   INNER JOIN LatestCode latestCode
		                   	ON latestCode.AccountCodeId = codes.AccountCodeId;
		                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@CodePurpose", codePurpose);
		command.Parameters.AddWithValue("@DestinationType", destinationType);
		command.Parameters.AddWithValue("@DestinationNormalized", destinationNormalized);

		await command.ExecuteNonQueryAsync();
	}

	internal static async Task MaxOutLatestAccountCodeAttemptsAsync(
		Guid accountId,
		string codePurpose,
		string destinationType,
		string destinationNormalized)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   WITH LatestCode AS
		                   (
		                   	SELECT TOP (1)
		                   		AccountCodeId
		                   	FROM dbo.pb_account_codes
		                   	WHERE AccountId = @AccountId
		                   		AND CodePurpose = @CodePurpose
		                   		AND DestinationType = @DestinationType
		                   		AND DestinationNormalized = @DestinationNormalized
		                   	ORDER BY CreatedAtUtc DESC
		                   )
		                   UPDATE codes
		                   SET AttemptCount = MaxAttempts
		                   FROM dbo.pb_account_codes codes
		                   INNER JOIN LatestCode latestCode
		                   	ON latestCode.AccountCodeId = codes.AccountCodeId;
		                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@CodePurpose", codePurpose);
		command.Parameters.AddWithValue("@DestinationType", destinationType);
		command.Parameters.AddWithValue("@DestinationNormalized", destinationNormalized);

		await command.ExecuteNonQueryAsync();
	}

	internal static async Task<DateTime?> ReadLastLoginAtUtcAsync(Guid accountId)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
		                   SELECT LastLoginAtUtc
		                   FROM dbo.pb_accounts
		                   WHERE AccountId = @AccountId;
		                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);

		var result = await command.ExecuteScalarAsync();

		return result is DBNull or null
			? null
			: (DateTime)result;
	}

	internal static async Task<int> CountAccountCodeRowsByPurposeAndDestinationAsync(
		string codePurpose,
		string destinationType,
		string destinationNormalized)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
	                   SELECT COUNT_BIG(1)
	                   FROM dbo.pb_account_codes
	                   WHERE CodePurpose = @CodePurpose
	                   	AND DestinationType = @DestinationType
	                   	AND DestinationNormalized = @DestinationNormalized;
	                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@CodePurpose", codePurpose);
		command.Parameters.AddWithValue("@DestinationType", destinationType);
		command.Parameters.AddWithValue("@DestinationNormalized", destinationNormalized);

		var result = await command.ExecuteScalarAsync();

		return Convert.ToInt32(result);
	}

	internal static async Task<int> CountCodeDeliveryOutboxRowsByPurposeAndDestinationAsync(
		string codePurpose,
		string destinationType,
		string destinationNormalized)
	{
		var connectionString = GetConnectionString();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		const string sql = """
	                   SELECT COUNT_BIG(1)
	                   FROM dbo.pb_account_code_delivery_outbox
	                   WHERE CodePurpose = @CodePurpose
	                   	AND DestinationType = @DestinationType
	                   	AND DestinationNormalized = @DestinationNormalized;
	                   """;

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@CodePurpose", codePurpose);
		command.Parameters.AddWithValue("@DestinationType", destinationType);
		command.Parameters.AddWithValue("@DestinationNormalized", destinationNormalized);

		var result = await command.ExecuteScalarAsync();

		return Convert.ToInt32(result);
	}
}
