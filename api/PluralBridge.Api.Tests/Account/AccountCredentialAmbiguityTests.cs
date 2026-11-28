// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

/// <summary>
/// Isolated sign-in and password-reset ambiguity checks.
/// The directory stands in for the SQL lookups. These tests do not open a database connection or send mail,
/// and they do not prove that SQL Server returns every matching row.
/// </summary>
public sealed class AccountCredentialAmbiguityTests
{
	/// <summary>Duplicate rows for one account id are one match. Two account ids are no match.</summary>
	[Fact]
	public void IdentityMatch_CountsDistinctAccounts()
	{
		var accountId = Guid.NewGuid();
		var sameAccount = new[] { accountId, accountId };
		var twoAccounts = new[] { accountId, Guid.NewGuid() };

		Assert.True(AccountIdentityMatches.TrySingle(sameAccount, id => id, out var single));
		Assert.Equal(accountId, single);
		Assert.False(AccountIdentityMatches.TrySingle(twoAccounts, id => id, out _));
		Assert.False(AccountIdentityMatches.TrySingle(Array.Empty<Guid>(), id => id, out _));
	}

	/// <summary>One unambiguous account can sign in. A wrong password does not record the login.</summary>
	[Fact]
	public async Task Login_UnambiguousAccount_AcceptsThePasswordAndRecordsOneLogin()
	{
		var fixture = Fixture();
		var account = fixture.Directory.Seed("ada", "ada@example.com", "correct-password", active: true);
		var pending = fixture.Directory.Seed("pending", "pending@example.com", "correct-password", active: false);
		var signedIn = await fixture.Service.LoginAsync(new LoginRequest("ada", "correct-password"), CancellationToken.None);
		var rejected = await fixture.Service.LoginAsync(new LoginRequest("ada@example.com", "wrong-password"), CancellationToken.None);
		var ineligible = await fixture.Service.LoginAsync(new LoginRequest("pending", "correct-password"), CancellationToken.None);

		Assert.True(signedIn.Succeeded);
		Assert.Equal(account.AccountId, signedIn.Value!.Account!.AccountId);
		Assert.False(rejected.Succeeded);
		Assert.Null(rejected.Value);
		Assert.Equal(AccountReasonCodes.AccountUnavailable, ineligible.ReasonCode);
		Assert.Null(ineligible.Value);
		Assert.Equal(1, account.LoginRecords);
		Assert.Equal(0, pending.LoginRecords);
		Assert.Equal(0, fixture.Delivery.Calls);
	}

	/// <summary>Two accounts matched by one identifier share the unknown-account failure. Neither login time changes.</summary>
	[Fact]
	public async Task Login_TwoAccounts_DoesNotAuthenticateEither()
	{
		var fixture = Fixture();
		var pending = fixture.Directory.Seed("shared", "pending@example.com", "correct-password", active: true);
		var other = fixture.Directory.Seed("other", "shared", "other-password", active: true);

		var unknown = await fixture.Service.LoginAsync(new LoginRequest("missing@example.com", "correct-password"), CancellationToken.None);
		var ambiguous = await fixture.Service.LoginAsync(new LoginRequest("shared", "correct-password"), CancellationToken.None);

		Assert.False(ambiguous.Succeeded);
		Assert.Null(ambiguous.Value);
		Assert.Equal(unknown.ReasonCode, ambiguous.ReasonCode);
		Assert.Equal(unknown.Message, ambiguous.Message);
		Assert.Equal(0, pending.LoginRecords);
		Assert.Equal(0, other.LoginRecords);
		Assert.Equal(0, fixture.Delivery.Calls);
	}

	/// <summary>One account whose username and email are the same value can sign in.</summary>
	[Fact]
	public async Task Login_OneAccountMatchingBothFields_SignsIn()
	{
		var fixture = Fixture();
		var account = fixture.Directory.Seed("ada@example.com", "ada@example.com", "correct-password", active: true);

		var result = await fixture.Service.LoginAsync(new LoginRequest("ada@example.com", "correct-password"), CancellationToken.None);

		Assert.True(result.Succeeded);
		Assert.Equal(account.AccountId, result.Value!.Account!.AccountId);
		Assert.Equal(1, account.LoginRecords);
	}

	/// <summary>One eligible account receives a reset code at its stored email. An ineligible account receives nothing.</summary>
	[Fact]
	public async Task Reset_UnambiguousAccount_IssuesOneCodeAndSkipsAnIneligibleAccount()
	{
		var fixture = Fixture();
		var account = fixture.Directory.Seed("ada", "ada@example.com", "correct-password", active: true);
		fixture.Directory.Seed("pending", "pending@example.com", "correct-password", active: false);

		var sent = await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest("ada"), CancellationToken.None);
		var skipped = await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest("pending@example.com"), CancellationToken.None);

		Assert.True(sent.Succeeded);
		Assert.Equal(Globals.accountPasswordResetInstructionsSent, sent.Message);
		Assert.Equal(account.NormalizedEmail, fixture.Delivery.LastDestination);
		Assert.Equal(1, account.ResetCodesIssued);
		Assert.True(skipped.Succeeded);
		Assert.Equal(sent.Message, skipped.Message);
		Assert.Equal(1, fixture.Delivery.Calls);
	}

	/// <summary>Two accounts matched by one identifier get the unknown-account reset response. No code is stored and no mail is sent.</summary>
	[Fact]
	public async Task Reset_TwoAccounts_DoesNotChooseARecipient()
	{
		var fixture = Fixture();
		var first = fixture.Directory.Seed("shared", "first@example.com", "correct-password", active: true);
		var second = fixture.Directory.Seed("other", "shared", "correct-password", active: true);

		var unknown = await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest("missing@example.com"), CancellationToken.None);
		var ambiguous = await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest("shared"), CancellationToken.None);

		Assert.True(ambiguous.Succeeded);
		Assert.Equal(unknown.Message, ambiguous.Message);
		Assert.Equal(unknown.ReasonCode, ambiguous.ReasonCode);
		Assert.Equal(0, first.ResetCodesIssued);
		Assert.Equal(0, second.ResetCodesIssued);
		Assert.Equal(0, fixture.Delivery.Calls);
	}

	/// <summary>One account whose username and email are the same value can receive a reset code.</summary>
	[Fact]
	public async Task Reset_OneAccountMatchingBothFields_IssuesOneCode()
	{
		var fixture = Fixture();
		var account = fixture.Directory.Seed("ada@example.com", "ada@example.com", "correct-password", active: true);

		var result = await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest("ada@example.com"), CancellationToken.None);

		Assert.True(result.Succeeded);
		Assert.Equal(account.NormalizedEmail, fixture.Delivery.LastDestination);
		Assert.Equal(1, account.ResetCodesIssued);
		Assert.Equal(1, fixture.Delivery.Calls);
	}

	private static CredentialFixture Fixture()
	{
		var directory = new MemoryCredentialDirectory();
		var delivery = new RecordingDelivery();
		var service = new AccountService(
			new InMemoryAccountRegistrationGate(),
			new Pbkdf2PasswordHasher(),
			new RecordingAudit(),
			delivery,
			directory);
		return new CredentialFixture(directory, delivery, service);
	}

	private sealed record CredentialFixture(
		MemoryCredentialDirectory Directory,
		RecordingDelivery Delivery,
		AccountService Service);

	private sealed class RecordingDelivery : IAccountCodeDelivery
	{
		public int Calls { get; private set; }

		public string? LastDestination { get; private set; }

		public Task DeliverAsync(AccountCodeDeliveryCommand command, CancellationToken cancellationToken)
		{
			Calls++;
			LastDestination = command.DestinationNormalized;
			return Task.CompletedTask;
		}
	}

	private sealed class RecordingAudit : IAccountAuditWriter
	{
		public Task WriteAsync(AccountAuditCommand command, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}
}

/// <summary>In-memory credential directory. Match selection uses <see cref="AccountIdentityMatches"/>.</summary>
internal sealed class MemoryCredentialDirectory : IAccountCredentialDirectory
{
	private readonly List<CredentialAccount> _accounts = new();
	private readonly IPasswordHasher _hasher = new Pbkdf2PasswordHasher();

	internal CredentialAccount Seed(string username, string email, string password, bool active)
	{
		var hash = _hasher.HashPassword(password);
		var account = new CredentialAccount
		{
			AccountId = Guid.NewGuid(),
			Username = username,
			NormalizedUsername = username.Trim().ToUpperInvariant(),
			Email = email,
			NormalizedEmail = email.Trim().ToUpperInvariant(),
			StatusId = active ? RegistrationContinuation.ActiveStatusId : RegistrationContinuation.PendingEmailVerificationStatusId,
			IsEmailVerified = active,
			PasswordHash = hash.PasswordHash,
			Algorithm = hash.Algorithm,
			Version = hash.Version
		};
		_accounts.Add(account);
		return account;
	}

	public Task<LoginAccountRecord?> ReadLoginAccountAsync(string normalizedIdentifier, CancellationToken cancellationToken)
	{
		var matches = Matches(normalizedIdentifier);
		if (!AccountIdentityMatches.TrySingle(matches, account => account.AccountId, out var account))
		{
			return Task.FromResult<LoginAccountRecord?>(null);
		}

		return Task.FromResult<LoginAccountRecord?>(ToLoginRecord(account));
	}

	public Task RecordSuccessfulLoginAsync(Guid accountId, CancellationToken cancellationToken)
	{
		var account = _accounts.Single(candidate => candidate.AccountId == accountId);
		account.LoginRecords++;
		return Task.CompletedTask;
	}

	public Task<PasswordResetAccountRecord?> ReadPasswordResetAccountAsync(
		string normalizedIdentifier,
		CancellationToken cancellationToken)
	{
		var matches = Matches(normalizedIdentifier);
		if (!AccountIdentityMatches.TrySingle(matches, account => account.AccountId, out var account)
			|| account.StatusId != RegistrationContinuation.ActiveStatusId
			|| !account.IsEmailVerified)
		{
			return Task.FromResult<PasswordResetAccountRecord?>(null);
		}

		return Task.FromResult<PasswordResetAccountRecord?>(new PasswordResetAccountRecord(account.AccountId, account.NormalizedEmail));
	}

	public Task IssuePasswordResetCodeAsync(
		Guid accountId,
		string normalizedEmail,
		PasswordHashResult resetHash,
		string correlationId,
		CancellationToken cancellationToken)
	{
		var account = _accounts.Single(candidate => candidate.AccountId == accountId);
		account.ResetCodesIssued++;
		account.ResetDestination = normalizedEmail;
		return Task.CompletedTask;
	}

	private List<CredentialAccount> Matches(string normalizedIdentifier)
	{
		return _accounts.Where(account =>
			account.NormalizedUsername == normalizedIdentifier
			|| account.NormalizedEmail == normalizedIdentifier).ToList();
	}

	private static LoginAccountRecord ToLoginRecord(CredentialAccount account)
	{
		return new LoginAccountRecord(
			account.AccountId,
			account.Username,
			account.Email,
			account.Username,
			account.StatusId,
			account.IsEmailVerified ? "Active" : "PendingEmailVerification",
			account.IsEmailVerified,
			DateTime.UtcNow,
			null,
			null,
			account.PasswordHash,
			account.Algorithm,
			account.Version);
	}
}

internal sealed class CredentialAccount
{
	internal Guid AccountId { get; set; }

	internal string Username { get; set; } = string.Empty;

	internal string NormalizedUsername { get; set; } = string.Empty;

	internal string Email { get; set; } = string.Empty;

	internal string NormalizedEmail { get; set; } = string.Empty;

	internal int StatusId { get; set; }

	internal bool IsEmailVerified { get; set; }

	internal byte[] PasswordHash { get; set; } = [];

	internal string Algorithm { get; set; } = string.Empty;

	internal int Version { get; set; }

	internal int LoginRecords { get; set; }

	internal int ResetCodesIssued { get; set; }

	internal string? ResetDestination { get; set; }
}
