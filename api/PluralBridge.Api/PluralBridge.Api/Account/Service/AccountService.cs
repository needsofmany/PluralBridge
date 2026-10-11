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
	}
}
