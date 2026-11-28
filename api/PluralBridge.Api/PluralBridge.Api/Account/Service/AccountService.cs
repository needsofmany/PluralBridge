// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Account;

// Coordinates the account workflows and keeps the controllers thin.
// Persistence, hashing, auditing, and code delivery stay behind narrow helper interfaces.
public sealed partial class AccountService : IAccountService
{
	private const int PendingEmailVerificationStatusId = 2;

	private readonly string _connectionString;
	private readonly IPasswordHasher _passwordHasher;
	private readonly IAccountAuditWriter _auditWriter;
	private readonly IAccountCodeDelivery _codeDelivery;
	private readonly IAccountRegistrationGate _registrationGate;
	private readonly IAccountCredentialDirectory _credentials;

	// ReSharper disable once ConvertToPrimaryConstructor
	public AccountService(
		IConfiguration configuration,
		IPasswordHasher passwordHasher,
		IAccountAuditWriter auditWriter,
		IAccountCodeDelivery codeDelivery)
	{
		_connectionString = configuration.GetConnectionString(AccountConfigurationKeys.ConnectionStringName)
		                    ?? throw new InvalidOperationException(Globals.missingConnStringDetail);

		_passwordHasher = passwordHasher;
		_auditWriter = auditWriter;
		_codeDelivery = codeDelivery;
		_registrationGate = new SqlAccountRegistrationGate(_connectionString);
		_credentials = new SqlAccountCredentialDirectory(_connectionString);
	}

	/// <summary>
	/// Creates the service over substitutes that do not open a database connection.
	/// The host uses the public constructor. Isolated tests use this one.
	/// </summary>
	internal AccountService(
		IAccountRegistrationGate registrationGate,
		IPasswordHasher passwordHasher,
		IAccountAuditWriter auditWriter,
		IAccountCodeDelivery codeDelivery,
		IAccountCredentialDirectory? credentials = null)
	{
		_connectionString = string.Empty;
		_registrationGate = registrationGate;
		_passwordHasher = passwordHasher;
		_auditWriter = auditWriter;
		_codeDelivery = codeDelivery;
		_credentials = credentials ?? new SqlAccountCredentialDirectory(string.Empty);
	}
}
