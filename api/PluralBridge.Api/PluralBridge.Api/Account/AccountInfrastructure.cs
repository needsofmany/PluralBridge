using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Account;

internal static class AccountInfrastructure
{
	internal static Task WriteAuditAsync(
		IAccountAuditWriter auditWriter,
		string eventName,
		string outcome,
		string reasonCode,
		Guid? actorAccountId,
		Guid? targetAccountId,
		string correlationId,
		string safeSubject,
		CancellationToken cancellationToken)
	{
		return auditWriter.WriteAsync(
			new AccountAuditCommand(
				eventName,
				outcome,
				reasonCode,
				actorAccountId,
				targetAccountId,
				null,
				null,
				correlationId,
				"api",
				safeSubject,
				null),
			cancellationToken);
	}
}

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
	private const int SaltSizeBytes = 16;
	private const int KeySizeBytes = 32;
	private const int IterationCount = 210_000;

	public PasswordHashResult HashPassword(string password)
	{
		ArgumentNullException.ThrowIfNull(password);

		var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);

		var key = Rfc2898DeriveBytes.Pbkdf2(
			password: Encoding.UTF8.GetBytes(password),
			salt: salt,
			iterations: IterationCount,
			hashAlgorithm: HashAlgorithmName.SHA256,
			outputLength: KeySizeBytes);

		var payload = new byte[SaltSizeBytes + KeySizeBytes];

		Buffer.BlockCopy(salt, 0, payload, 0, SaltSizeBytes);
		Buffer.BlockCopy(key, 0, payload, SaltSizeBytes, KeySizeBytes);

		return new PasswordHashResult(
			payload,
			"PBKDF2-SHA256",
			1);
	}

	public bool VerifyPassword(string password, byte[] passwordHash, string algorithm, int version)
	{
		ArgumentNullException.ThrowIfNull(password);
		ArgumentNullException.ThrowIfNull(passwordHash);
		ArgumentNullException.ThrowIfNull(algorithm);

		if (!string.Equals(algorithm, "PBKDF2-SHA256", StringComparison.Ordinal))
		{
			return false;
		}

		if (version != 1)
		{
			return false;
		}

		if (passwordHash.Length != SaltSizeBytes + KeySizeBytes)
		{
			return false;
		}

		var salt = passwordHash.AsSpan(0, SaltSizeBytes).ToArray();
		var expectedKey = passwordHash.AsSpan(SaltSizeBytes, KeySizeBytes).ToArray();

		var actualKey = Rfc2898DeriveBytes.Pbkdf2(
			password: Encoding.UTF8.GetBytes(password),
			salt: salt,
			iterations: IterationCount,
			hashAlgorithm: HashAlgorithmName.SHA256,
			outputLength: KeySizeBytes);

		return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
	}
}

public static class AccountText
{
	public static string NormalizeUsername(string value)
	{
		return value.Trim().ToUpperInvariant();
	}

	public static string NormalizeEmail(string value)
	{
		return value.Trim().ToUpperInvariant();
	}

	public static bool HasText(string? value)
	{
		return !string.IsNullOrWhiteSpace(value);
	}
}

public sealed class SqlAccountAuditWriter : IAccountAuditWriter
{
	private readonly string _connectionString;

	public SqlAccountAuditWriter(IConfiguration configuration)
	{
		_connectionString = configuration.GetConnectionString("DefaultConnection")
			?? throw new InvalidOperationException("DefaultConnection is not configured.");
	}

	public async Task WriteAsync(AccountAuditCommand command, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(command);

		const string sql = """
            INSERT INTO dbo.pb_account_audit_events
            (
                EventName,
                Outcome,
                ReasonCode,
                ActorAccountId,
                TargetAccountId,
                SystemId,
                MembershipId,
                CorrelationId,
                Source,
                SafeSubject,
                SafeDetailJson,
                SchemaVersion
            )
            VALUES
            (
                @EventName,
                @Outcome,
                @ReasonCode,
                @ActorAccountId,
                @TargetAccountId,
                @SystemId,
                @MembershipId,
                @CorrelationId,
                @Source,
                @SafeSubject,
                @SafeDetailJson,
                1
            );
            """;

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		await using var commandSql = new SqlCommand(sql, connection);

		commandSql.Parameters.AddWithValue("@EventName", command.EventName);
		commandSql.Parameters.AddWithValue("@Outcome", command.Outcome);
		commandSql.Parameters.AddWithValue("@ReasonCode", command.ReasonCode);
		commandSql.Parameters.AddWithValue("@ActorAccountId", (object?)command.ActorAccountId ?? DBNull.Value);
		commandSql.Parameters.AddWithValue("@TargetAccountId", (object?)command.TargetAccountId ?? DBNull.Value);
		commandSql.Parameters.AddWithValue("@SystemId", (object?)command.SystemId ?? DBNull.Value);
		commandSql.Parameters.AddWithValue("@MembershipId", (object?)command.MembershipId ?? DBNull.Value);
		commandSql.Parameters.AddWithValue("@CorrelationId", command.CorrelationId);
		commandSql.Parameters.AddWithValue("@Source", command.Source);
		commandSql.Parameters.AddWithValue("@SafeSubject", (object?)command.SafeSubject ?? DBNull.Value);
		commandSql.Parameters.AddWithValue("@SafeDetailJson", (object?)command.SafeDetailJson ?? DBNull.Value);

		await commandSql.ExecuteNonQueryAsync(cancellationToken);
	}
}