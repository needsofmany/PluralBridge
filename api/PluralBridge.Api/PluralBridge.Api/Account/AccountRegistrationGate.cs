// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Account;

/// <summary>
/// The next sign-in step.
/// Registration is an identifier that matched no account. Declined stays the shared result for a restricted or ambiguous account.
/// </summary>
internal enum LoginContinuationKind
{
	Declined,
	Password,
	VerificationCode,
	Registration
}

/// <summary>Bookkeeping result for verification or resend. The HTTP body is mapped from this and does not include the account id.</summary>
internal enum RegistrationAccessDecisionKind
{
	Ineligible,
	InvalidCode,
	Expired,
	Consumed,
	RateLimited,
	EligibilityLost,
	Success,
	Cooldown,
	Issued
}

/// <summary>A gate result. <see cref="AccountId"/> is for audit only.</summary>
internal sealed record RegistrationAccessDecision(
	RegistrationAccessDecisionKind Kind,
	Guid? AccountId,
	Guid? CodeId = null,
	string? DeliveryEmail = null);

/// <summary>
/// Registration lifetime and the comparisons the database clock applies.
/// An expiry equal to the database time is already expired. A code issued exactly one cooldown ago can be replaced.
/// </summary>
internal static class AccountCodeLifetimes
{
	internal static int MinutesFor(string codePurpose)
	{
		return codePurpose == AccountCodePurposes.RegistrationVerification
			? Globals.registrationVerificationCodeExpirationMinutes
			: Globals.accountCodeExpirationMinutes;
	}

	internal static bool IsExpired(DateTime expiresAtUtc, DateTime databaseNowUtc)
	{
		return expiresAtUtc <= databaseNowUtc;
	}

	internal static bool IsWithinCooldown(DateTime issuedAtUtc, DateTime databaseNowUtc)
	{
		return issuedAtUtc > databaseNowUtc.AddSeconds(-Globals.registrationCodeResendCooldownSeconds);
	}
}

/// <summary>Maps stored account fields to a continuation step. Status values other than active or pending-verification decline.</summary>
internal static class RegistrationContinuation
{
	internal const int ActiveStatusId = 1;
	internal const int PendingEmailVerificationStatusId = 2;
	internal const string PasswordStep = "password";
	internal const string VerificationStep = "verification";
	internal const string RegistrationStep = "registration";

	internal static LoginContinuationKind KindFor(int statusId, bool isEmailVerified)
	{
		if (statusId == ActiveStatusId && isEmailVerified)
		{
			return LoginContinuationKind.Password;
		}

		if (statusId == PendingEmailVerificationStatusId && !isEmailVerified)
		{
			return LoginContinuationKind.VerificationCode;
		}

		return LoginContinuationKind.Declined;
	}
}

/// <summary>Expiry sentence for one code purpose. Registration uses its own ten-minute lifetime.</summary>
internal static class AccountCodeEmailText
{
	internal static string ExpiresLine(string codePurpose)
	{
		return "This code expires in " + AccountCodeLifetimes.MinutesFor(codePurpose).ToString() + " minutes.";
	}
}

/// <summary>
/// Account lookup and registration-code bookkeeping.
/// The SQL implementation holds the account row for verification and resend. The isolated gate substitutes that lock.
/// </summary>
internal interface IAccountRegistrationGate
{
	/// <summary>
	/// Reads the continuation step for one matching account.
	/// Zero matches select registration and change nothing. More than one distinct account is a decline.
	/// This method does not create an account, send mail, or create a session.
	/// </summary>
	Task<LoginContinuationKind> ReadContinuationAsync(string normalizedIdentifier, CancellationToken cancellationToken);

	/// <summary>
	/// Consumes one eligible registration code and activates the pending account, or changes nothing.
	/// The hash check and the eligibility check run while the account is locked.
	/// </summary>
	Task<RegistrationAccessDecision> VerifyAsync(
		string normalizedIdentifier,
		string submittedCode,
		IPasswordHasher hasher,
		CancellationToken cancellationToken);

	/// <summary>
	/// Invalidates unconsumed registration codes and inserts a replacement when the cooldown allows.
	/// Delivery stays outside this method. A failed delivery does not restore the invalidated codes.
	/// </summary>
	Task<RegistrationAccessDecision> PrepareResendAsync(
		string normalizedIdentifier,
		PasswordHashResult codeHash,
		string correlationId,
		CancellationToken cancellationToken);
}

/// <summary>
/// SQL Server registration gate.
/// A username or email that matches no account selects registration and changes nothing.
/// A username or email that matches more than one account is declined and changes nothing.
/// Verification and resend take UPDLOCK and HOLDLOCK on the matched account rows first, then on that account's registration codes when exactly one account matched.
/// Acceptance uses <c>SYSUTCDATETIME()</c> and the stored <c>ExpiresAtUtc</c>. This class does not prove those locks by itself.
/// </summary>
internal sealed class SqlAccountRegistrationGate : IAccountRegistrationGate
{
	private readonly string _connectionString;

	internal SqlAccountRegistrationGate(string connectionString)
	{
		_connectionString = connectionString;
	}

	/// <inheritdoc />
	public async Task<LoginContinuationKind> ReadContinuationAsync(string normalizedIdentifier, CancellationToken cancellationToken)
	{
		const string sql = """
			SELECT
				a.AccountId,
				a.AccountStatusId,
				a.IsEmailVerified
			FROM dbo.pb_accounts AS a
			WHERE a.NormalizedUsername = @NormalizedIdentifier
			   OR a.NormalizedEmail = @NormalizedIdentifier;
			""";

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);
		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@NormalizedIdentifier", normalizedIdentifier);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		var matches = new List<(Guid AccountId, int StatusId, bool IsEmailVerified)>();
		while (await reader.ReadAsync(cancellationToken))
		{
			matches.Add((reader.GetGuid(0), reader.GetInt32(1), reader.GetBoolean(2)));
		}

		if (matches.Count == 0)
		{
			return LoginContinuationKind.Registration;
		}

		if (!AccountIdentityMatches.TrySingle(matches, match => match.AccountId, out var account))
		{
			return LoginContinuationKind.Declined;
		}

		return RegistrationContinuation.KindFor(account.StatusId, account.IsEmailVerified);
	}

	/// <inheritdoc />
	public async Task<RegistrationAccessDecision> VerifyAsync(
		string normalizedIdentifier,
		string submittedCode,
		IPasswordHasher hasher,
		CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
		try
		{
			var account = await ReadLockedAccountAsync(connection, transaction, normalizedIdentifier, cancellationToken);
			if (account is null
				|| RegistrationContinuation.KindFor(account.StatusId, account.IsEmailVerified) != LoginContinuationKind.VerificationCode)
			{
				await transaction.CommitAsync(cancellationToken);
				return new RegistrationAccessDecision(RegistrationAccessDecisionKind.Ineligible, account?.AccountId);
			}

			var codes = await ReadLockedCodesAsync(connection, transaction, account.AccountId, cancellationToken);
			var databaseNow = await ReadDatabaseNowAsync(connection, transaction, cancellationToken);
			var decision = DecideVerification(account, codes, submittedCode, hasher, databaseNow);
			if (decision.Kind == RegistrationAccessDecisionKind.InvalidCode && decision.CodeId is Guid invalidCodeId)
			{
				var attempts = await IncrementAttemptAsync(connection, transaction, invalidCodeId, cancellationToken);
				decision = attempts == 1
					? decision
					: new RegistrationAccessDecision(RegistrationAccessDecisionKind.RateLimited, account.AccountId);
			}

			if (decision.Kind != RegistrationAccessDecisionKind.Success)
			{
				await transaction.CommitAsync(cancellationToken);
				return decision;
			}

			var consumed = await ConsumeCodeAsync(connection, transaction, decision.CodeId!.Value, cancellationToken);
			if (consumed != 1)
			{
				await transaction.RollbackAsync(cancellationToken);
				return new RegistrationAccessDecision(RegistrationAccessDecisionKind.Consumed, account.AccountId);
			}

			var activated = await ActivatePendingAccountAsync(connection, transaction, account.AccountId, cancellationToken);
			if (activated != 1)
			{
				await transaction.RollbackAsync(cancellationToken);
				return new RegistrationAccessDecision(RegistrationAccessDecisionKind.EligibilityLost, account.AccountId);
			}

			await transaction.CommitAsync(cancellationToken);
			return decision;
		}
		catch
		{
			await transaction.RollbackAsync(CancellationToken.None);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<RegistrationAccessDecision> PrepareResendAsync(
		string normalizedIdentifier,
		PasswordHashResult codeHash,
		string correlationId,
		CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
		try
		{
			var account = await ReadLockedAccountAsync(connection, transaction, normalizedIdentifier, cancellationToken);
			if (account is null
				|| RegistrationContinuation.KindFor(account.StatusId, account.IsEmailVerified) != LoginContinuationKind.VerificationCode)
			{
				await transaction.CommitAsync(cancellationToken);
				return new RegistrationAccessDecision(RegistrationAccessDecisionKind.Ineligible, account?.AccountId);
			}

			var codes = await ReadLockedCodesAsync(connection, transaction, account.AccountId, cancellationToken);
			var databaseNow = await ReadDatabaseNowAsync(connection, transaction, cancellationToken);
			var latestIssue = codes.Count == 0 ? (DateTime?)null : codes.Max(code => code.IssuedAtUtc);
			if (latestIssue is not null && AccountCodeLifetimes.IsWithinCooldown(latestIssue.Value, databaseNow))
			{
				await transaction.CommitAsync(cancellationToken);
				return new RegistrationAccessDecision(RegistrationAccessDecisionKind.Cooldown, account.AccountId);
			}

			await InvalidateUnconsumedCodesAsync(connection, transaction, account.AccountId, cancellationToken);
			await AccountCodeService.InsertVerificationCodeAsync(
				connection,
				transaction,
				account.AccountId,
				account.NormalizedEmail,
				codeHash,
				correlationId,
				cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return new RegistrationAccessDecision(
				RegistrationAccessDecisionKind.Issued,
				account.AccountId,
				null,
				account.NormalizedEmail);
		}
		catch
		{
			await transaction.RollbackAsync(CancellationToken.None);
			throw;
		}
	}

	/// <summary>
	/// Classifies the locked rows. A consumed match that is not the live code is superseded and does not change the live attempt count.
	/// Equality with the database clock is expired. This method does not write.
	/// </summary>
	private static RegistrationAccessDecision DecideVerification(
		LockedAccount account,
		IReadOnlyList<LockedCode> codes,
		string submittedCode,
		IPasswordHasher hasher,
		DateTime databaseNowUtc)
	{
		var live = codes
			.Where(code => code.ConsumedAtUtc is null)
			.OrderByDescending(code => code.IssuedAtUtc)
			.FirstOrDefault();
		var matchesConsumed = codes.Any(code =>
			code.ConsumedAtUtc is not null
			&& hasher.VerifyPassword(submittedCode, code.Hash, code.Algorithm, code.Version));
		if (matchesConsumed && (live is null || !hasher.VerifyPassword(submittedCode, live.Hash, live.Algorithm, live.Version)))
		{
			return new RegistrationAccessDecision(RegistrationAccessDecisionKind.Consumed, account.AccountId);
		}

		if (live is null)
		{
			return new RegistrationAccessDecision(RegistrationAccessDecisionKind.InvalidCode, account.AccountId);
		}

		if (AccountCodeLifetimes.IsExpired(live.ExpiresAtUtc, databaseNowUtc))
		{
			return new RegistrationAccessDecision(RegistrationAccessDecisionKind.Expired, account.AccountId);
		}

		if (live.AttemptCount >= live.MaxAttempts)
		{
			return new RegistrationAccessDecision(RegistrationAccessDecisionKind.RateLimited, account.AccountId);
		}

		if (!hasher.VerifyPassword(submittedCode, live.Hash, live.Algorithm, live.Version))
		{
			return new RegistrationAccessDecision(RegistrationAccessDecisionKind.InvalidCode, account.AccountId, live.AccountCodeId);
		}

		if (RegistrationContinuation.KindFor(account.StatusId, account.IsEmailVerified) != LoginContinuationKind.VerificationCode)
		{
			return new RegistrationAccessDecision(RegistrationAccessDecisionKind.EligibilityLost, account.AccountId);
		}

		return new RegistrationAccessDecision(RegistrationAccessDecisionKind.Success, account.AccountId, live.AccountCodeId);
	}

	/// <summary>
	/// Locks every account row matched by the identifier, then returns that account only when exactly one distinct account matched.
	/// Zero matches and two or more matches both return null. The caller commits without consuming a code or changing account state.
	/// One account whose username and email are the same value is that one account.
	/// </summary>
	private static async Task<LockedAccount?> ReadLockedAccountAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		string normalizedIdentifier,
		CancellationToken cancellationToken)
	{
		const string sql = """
			SELECT
				a.AccountId,
				a.AccountStatusId,
				a.IsEmailVerified,
				a.NormalizedEmail
			FROM dbo.pb_accounts AS a WITH (UPDLOCK, HOLDLOCK)
			WHERE a.NormalizedUsername = @NormalizedIdentifier
			   OR a.NormalizedEmail = @NormalizedIdentifier;
			""";

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.AddWithValue("@NormalizedIdentifier", normalizedIdentifier);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		var matches = new List<LockedAccount>();
		while (await reader.ReadAsync(cancellationToken))
		{
			matches.Add(new LockedAccount(reader.GetGuid(0), reader.GetInt32(1), reader.GetBoolean(2), reader.GetString(3)));
		}

		return AccountIdentityMatches.TrySingle(matches, match => match.AccountId, out var account)
			? account
			: null;
	}

	/// <summary>Locks this account's registration-code rows for the rest of the transaction.</summary>
	private static async Task<List<LockedCode>> ReadLockedCodesAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		CancellationToken cancellationToken)
	{
		const string sql = """
			SELECT
				AccountCodeId,
				CodeHash,
				CodeHashAlgorithm,
				CodeHashVersion,
				ExpiresAtUtc,
				ConsumedAtUtc,
				AttemptCount,
				MaxAttempts,
				IssuedAtUtc
			FROM dbo.pb_account_codes WITH (UPDLOCK, HOLDLOCK)
			WHERE AccountId = @AccountId
			  AND CodePurpose = @CodePurpose
			  AND DestinationType = @DestinationType;
			""";

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.AddWithValue(Globals.sqlParameterAccountId, accountId);
		command.Parameters.AddWithValue(Globals.sqlParameterCodePurpose, AccountCodePurposes.RegistrationVerification);
		command.Parameters.AddWithValue(Globals.sqlParameterDestinationType, AccountDestinationTypes.Email);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		var codes = new List<LockedCode>();
		while (await reader.ReadAsync(cancellationToken))
		{
			codes.Add(new LockedCode(
				reader.GetGuid(0),
				(byte[])reader.GetValue(1),
				reader.GetString(2),
				reader.GetInt32(3),
				reader.GetDateTime(4),
				reader.IsDBNull(5) ? null : reader.GetDateTime(5),
				reader.GetInt32(6),
				reader.GetInt32(7),
				reader.GetDateTime(8)));
		}

		return codes;
	}

	/// <summary>Reads the database clock inside the transaction. The application clock is not an acceptance clock.</summary>
	private static async Task<DateTime> ReadDatabaseNowAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		CancellationToken cancellationToken)
	{
		await using var command = new SqlCommand("SELECT SYSUTCDATETIME();", connection, transaction);
		var value = await command.ExecuteScalarAsync(cancellationToken);
		if (value is not DateTime databaseNow)
		{
			throw new InvalidOperationException("The database clock did not return a date.");
		}

		return DateTime.SpecifyKind(databaseNow, DateTimeKind.Utc);
	}

	/// <summary>Increments one unconsumed code only while its attempt count is still below the limit. Zero rows means the row was exhausted or consumed concurrently.</summary>
	private static async Task<int> IncrementAttemptAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountCodeId,
		CancellationToken cancellationToken)
	{
		const string sql = """
			UPDATE dbo.pb_account_codes
			SET
				AttemptCount = AttemptCount + 1,
				LastAttemptAtUtc = SYSUTCDATETIME()
			WHERE AccountCodeId = @AccountCodeId
			  AND ConsumedAtUtc IS NULL
			  AND AttemptCount < MaxAttempts;
			""";

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.AddWithValue(Globals.sqlParameterAccountCodeId, accountCodeId);
		return await command.ExecuteNonQueryAsync(cancellationToken);
	}

	/// <summary>Consumes one code that is still unused, unexpired against <c>SYSUTCDATETIME()</c>, and under the attempt limit. Success requires one row.</summary>
	private static async Task<int> ConsumeCodeAsync(
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
			  AND ConsumedAtUtc IS NULL
			  AND ExpiresAtUtc > SYSUTCDATETIME()
			  AND AttemptCount < MaxAttempts;
			""";

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.AddWithValue(Globals.sqlParameterAccountCodeId, accountCodeId);
		return await command.ExecuteNonQueryAsync(cancellationToken);
	}

	/// <summary>Activates one pending, unverified account. A disabled, locked, or deleted row changes zero rows and the consumption rolls back with it.</summary>
	private static async Task<int> ActivatePendingAccountAsync(
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
			  AND AccountStatusId = 2
			  AND IsEmailVerified = 0;
			""";

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.AddWithValue(Globals.sqlParameterAccountId, accountId);
		return await command.ExecuteNonQueryAsync(cancellationToken);
	}

	/// <summary>Marks every unconsumed registration code consumed in the same transaction that inserts the replacement. A later delivery failure does not clear these timestamps.</summary>
	private static async Task InvalidateUnconsumedCodesAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		CancellationToken cancellationToken)
	{
		const string sql = """
			UPDATE dbo.pb_account_codes
			SET
				ConsumedAtUtc = SYSUTCDATETIME(),
				LastAttemptAtUtc = SYSUTCDATETIME()
			WHERE AccountId = @AccountId
			  AND CodePurpose = @CodePurpose
			  AND DestinationType = @DestinationType
			  AND ConsumedAtUtc IS NULL;
			""";

		await using var command = new SqlCommand(sql, connection, transaction);
		command.Parameters.AddWithValue(Globals.sqlParameterAccountId, accountId);
		command.Parameters.AddWithValue(Globals.sqlParameterCodePurpose, AccountCodePurposes.RegistrationVerification);
		command.Parameters.AddWithValue(Globals.sqlParameterDestinationType, AccountDestinationTypes.Email);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private sealed record LockedAccount(Guid AccountId, int StatusId, bool IsEmailVerified, string NormalizedEmail);

	private sealed record LockedCode(
		Guid AccountCodeId,
		byte[] Hash,
		string Algorithm,
		int Version,
		DateTime ExpiresAtUtc,
		DateTime? ConsumedAtUtc,
		int AttemptCount,
		int MaxAttempts,
		DateTime IssuedAtUtc);
}
