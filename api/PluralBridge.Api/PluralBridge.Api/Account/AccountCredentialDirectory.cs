// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Account;

/// <summary>
/// Login and password-reset lookups. An ambiguous identifier returns null.
/// The service checks the password and sends mail. This directory does not issue a session.
/// </summary>
internal interface IAccountCredentialDirectory
{
	/// <summary>Reads the one matching account, including an ineligible account. Ambiguous and unknown identifiers return null.</summary>
	Task<LoginAccountRecord?> ReadLoginAccountAsync(string normalizedIdentifier, CancellationToken cancellationToken);

	/// <summary>Records a successful password sign-in for the account that was already accepted. It does not choose an account.</summary>
	Task RecordSuccessfulLoginAsync(Guid accountId, CancellationToken cancellationToken);

	/// <summary>
	/// Reads one active, verified account for a reset code.
	/// An unknown, ineligible, or ambiguous identifier returns null and the caller sends nothing.
	/// </summary>
	Task<PasswordResetAccountRecord?> ReadPasswordResetAccountAsync(string normalizedIdentifier, CancellationToken cancellationToken);

	/// <summary>Stores one reset-code hash for an account the lookup already accepted. Delivery stays outside this method.</summary>
	Task IssuePasswordResetCodeAsync(
		Guid accountId,
		string normalizedEmail,
		PasswordHashResult resetHash,
		string correlationId,
		CancellationToken cancellationToken);
}

/// <summary>Password-reset lookup row. <see cref="AccountId"/> is the only identity used after the ambiguity check.</summary>
internal sealed record PasswordResetAccountRecord(
	Guid AccountId,
	string NormalizedEmail);

/// <summary>
/// SQL implementation of the credential lookups.
/// Each call opens its own connection. The password check sits between the login read and the last-login update, and those two commands are not one transaction.
/// An ambiguous read returns before either write. This class does not prove the SQL row set by itself.
/// </summary>
internal sealed class SqlAccountCredentialDirectory : IAccountCredentialDirectory
{
	private readonly string _connectionString;

	internal SqlAccountCredentialDirectory(string connectionString)
	{
		_connectionString = connectionString;
	}

	/// <inheritdoc />
	public async Task<LoginAccountRecord?> ReadLoginAccountAsync(string normalizedIdentifier, CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);
		return await AccountRepository.ReadLoginAccountAsync(connection, normalizedIdentifier, cancellationToken);
	}

	/// <inheritdoc />
	public async Task RecordSuccessfulLoginAsync(Guid accountId, CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);
		await AccountRepository.UpdateLastLoginAsync(connection, accountId, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<PasswordResetAccountRecord?> ReadPasswordResetAccountAsync(
		string normalizedIdentifier,
		CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);
		return await AccountRepository.ReadPasswordResetAccountAsync(connection, normalizedIdentifier, cancellationToken);
	}

	/// <inheritdoc />
	public async Task IssuePasswordResetCodeAsync(
		Guid accountId,
		string normalizedEmail,
		PasswordHashResult resetHash,
		string correlationId,
		CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);
		await AccountCodeService.InsertPasswordResetCodeAsync(
			connection,
			accountId,
			normalizedEmail,
			resetHash,
			correlationId,
			cancellationToken);
	}
}
