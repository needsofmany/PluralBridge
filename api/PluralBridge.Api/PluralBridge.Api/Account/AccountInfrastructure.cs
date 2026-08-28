// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Account;

// Shared account-layer primitives that do not belong to a single workflow:
// audit command construction, password hashing, text normalization, and audit persistence.
internal static class AccountInfrastructure
{
	// Most audit calls do not yet have structured detail. This overload preserves the original
	// call shape and forwards to the detail-aware overload with a null SafeDetailJson value.
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
		return WriteAuditAsync(
			auditWriter,
			eventName,
			outcome,
			reasonCode,
			actorAccountId,
			targetAccountId,
			correlationId,
			safeSubject,
			null,
			cancellationToken);
	}

	// Centralizes the stable fields every account audit row needs. Callers provide only the
	// workflow-specific decision data; this helper pins source/system/membership defaults.
	internal static Task WriteAuditAsync(
		IAccountAuditWriter auditWriter,
		string eventName,
		string outcome,
		string reasonCode,
		Guid? actorAccountId,
		Guid? targetAccountId,
		string correlationId,
		string safeSubject,
		string? safeDetailJson,
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
				safeDetailJson),
			cancellationToken);
	}
}

// PBKDF2 password and code hashing implementation.
// Account passwords and one-time account codes use the same hasher contract.
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
	// Payload format is salt || derived-key. Algorithm and version are stored separately
	// so future hash upgrades can coexist with existing rows.
	private const int SaltSizeBytes = 16;
	private const int KeySizeBytes = 32;
	private const int IterationCount = 210_000;

	public PasswordHashResult HashPassword(string password)
	{
		ArgumentNullException.ThrowIfNull(password);

		// Generate a fresh salt for every password/code before deriving the stored key.
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

		// Refuse hashes from unknown algorithm/version combinations before doing any comparison.
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

		// Re-derive using the stored salt, then compare in fixed time.
		var actualKey = Rfc2898DeriveBytes.Pbkdf2(
			password: Encoding.UTF8.GetBytes(password),
			salt: salt,
			iterations: IterationCount,
			hashAlgorithm: HashAlgorithmName.SHA256,
			outputLength: KeySizeBytes);

		return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
	}
}

// Normalization is deliberately small and predictable. The account workflows depend on these
// helpers for duplicate checks, login lookup, code lookup, and email verification.
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

// Writes account audit events to dbo.pb_account_audit_events.
// The writer receives a complete AccountAuditCommand; it does no business-rule interpretation.
public sealed class SqlAccountAuditWriter : IAccountAuditWriter
{
	private readonly string _connectionString;

	// ReSharper disable once ConvertToPrimaryConstructor
	public SqlAccountAuditWriter(IConfiguration configuration)
	{
		_connectionString = configuration.GetConnectionString(AccountConfigurationKeys.ConnectionStringName)
		                    ?? throw new InvalidOperationException($"{AccountConfigurationKeys.ConnectionStringName} is not configured.");
	}

	public async Task WriteAsync(AccountAuditCommand command, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(command);

		// SafeDetailJson is intentionally part of the persisted audit row. It may contain
		// operational lookup detail, but must not contain plaintext passwords or account codes.
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

		// Nullable audit dimensions are written as SQL NULLs instead of empty sentinel values.
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
