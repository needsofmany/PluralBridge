// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api;
using PluralBridge.Api.Account;

namespace PluralBridge.Api.Tests.Account;

/// <summary>
/// Isolated registration continuation, verification, and resend checks.
/// The gate lock stands in for the SQL account lock. These tests do not open a database connection or send mail,
/// and they do not prove that SQL Server takes UPDLOCK or commits the two updates together.
/// </summary>
public sealed class AccountRegistrationAccessTests
{
	/// <summary>Registration codes last ten minutes. The other purposes keep the existing fifteen minutes.</summary>
	[Fact]
	public void Lifetimes_RegistrationIsTenMinutesAndOtherPurposesStayAtFifteen()
	{
		Assert.Equal(10, AccountCodeLifetimes.MinutesFor(AccountCodePurposes.RegistrationVerification));
		Assert.Equal(15, Globals.accountCodeExpirationMinutes);
		Assert.Equal(15, AccountCodeLifetimes.MinutesFor(AccountCodePurposes.PasswordReset));
		Assert.Equal(15, AccountCodeLifetimes.MinutesFor(AccountCodePurposes.UsernameRecovery));
		Assert.Equal(15, AccountCodeLifetimes.MinutesFor(AccountCodePurposes.ContactVerification));
		Assert.Equal("This code expires in 10 minutes.", AccountCodeEmailText.ExpiresLine(AccountCodePurposes.RegistrationVerification));
		Assert.Equal("This code expires in 15 minutes.", AccountCodeEmailText.ExpiresLine(AccountCodePurposes.PasswordReset));
		Assert.Equal("This code expires in 15 minutes.", AccountCodeEmailText.ExpiresLine(AccountCodePurposes.UsernameRecovery));
		Assert.Equal("This code expires in 15 minutes.", AccountCodeEmailText.ExpiresLine(AccountCodePurposes.ContactVerification));
		Assert.Contains("This code expires in 10 minutes.", RegistrationBody(AccountCodePurposes.RegistrationVerification), StringComparison.Ordinal);
		Assert.Contains("This code expires in 10 minutes.", RegistrationHtml(AccountCodePurposes.RegistrationVerification), StringComparison.Ordinal);
		Assert.DoesNotContain("15 minutes", RegistrationBody(AccountCodePurposes.RegistrationVerification), StringComparison.Ordinal);
		Assert.Contains("This code expires in 15 minutes.", RegistrationBody(AccountCodePurposes.PasswordReset), StringComparison.Ordinal);
		Assert.Contains("This code expires in 15 minutes.", RegistrationHtml(AccountCodePurposes.UsernameRecovery), StringComparison.Ordinal);
		Assert.Contains("This code expires in 15 minutes.", RegistrationBody(AccountCodePurposes.ContactVerification), StringComparison.Ordinal);
		Assert.DoesNotContain("10 minutes", RegistrationBody(AccountCodePurposes.PasswordReset), StringComparison.Ordinal);
	}

	private static string RegistrationBody(string purpose)
	{
		return AzureAccountCodeDelivery.CreatePlainTextBody(BodyCommand(purpose));
	}

	private static string RegistrationHtml(string purpose)
	{
		return AzureAccountCodeDelivery.CreateHtmlBody(BodyCommand(purpose));
	}

	private static AccountCodeDeliveryCommand BodyCommand(string purpose)
	{
		return new AccountCodeDeliveryCommand(
			Guid.NewGuid(),
			purpose,
			AccountDestinationTypes.Email,
			"ADA@EXAMPLE.COM",
			"000000",
			"correlation");
	}

	/// <summary>A time equal to the stored expiry is expired. One tick earlier is still eligible.</summary>
	[Fact]
	public void ExpiryBoundary_EqualDatabaseTimeIsExpired()
	{
		var now = new DateTime(2026, 1, 1, 0, 10, 0, DateTimeKind.Utc);
		Assert.True(AccountCodeLifetimes.IsExpired(now, now));
		Assert.False(AccountCodeLifetimes.IsExpired(now.AddTicks(1), now));
		Assert.False(AccountCodeLifetimes.IsWithinCooldown(now.AddSeconds(-60), now));
		Assert.True(AccountCodeLifetimes.IsWithinCooldown(now.AddSeconds(-60).AddTicks(1), now));
	}

	/// <summary>Active accounts continue to the password step. The call does not send mail.</summary>
	[Fact]
	public async Task Continue_ActiveVerifiedAccount_SelectsPasswordAndSendsNothing()
	{
		var fixture = CreateFixture();
		fixture.Gate.Seed("ada", "ada@example.com", RegistrationContinuation.ActiveStatusId, true);
		var result = await fixture.Service.ContinueLoginAsync(new LoginContinuationRequest("ada@example.com"), CancellationToken.None);

		Assert.True(result.Succeeded);
		Assert.Equal(RegistrationContinuation.PasswordStep, result.Value!.NextStep);
		Assert.Equal(0, fixture.Delivery.Calls);
	}

	/// <summary>A pending registration continues to code entry and still sends nothing.</summary>
	[Fact]
	public async Task Continue_PendingAccount_SelectsVerificationAndSendsNothing()
	{
		var fixture = CreateFixture();
		fixture.Gate.Seed("ada", "ada@example.com", RegistrationContinuation.PendingEmailVerificationStatusId, false);
		var result = await fixture.Service.ContinueLoginAsync(new LoginContinuationRequest("Ada"), CancellationToken.None);

		Assert.True(result.Succeeded);
		Assert.Equal(RegistrationContinuation.VerificationStep, result.Value!.NextStep);
		Assert.Equal(0, fixture.Delivery.Calls);
	}

	/// <summary>An identifier that matches no account selects registration and does not create an account or send mail.</summary>
	[Fact]
	public async Task Continue_UnusedIdentity_SelectsRegistrationAndCreatesNothing()
	{
		var fixture = CreateFixture();

		var unused = await fixture.Service.ContinueLoginAsync(new LoginContinuationRequest("missing@example.com"), CancellationToken.None);
		var again = await fixture.Service.ContinueLoginAsync(new LoginContinuationRequest("missing@example.com"), CancellationToken.None);
		var blank = await fixture.Service.ContinueLoginAsync(new LoginContinuationRequest("   "), CancellationToken.None);

		Assert.True(unused.Succeeded);
		Assert.Equal(RegistrationContinuation.RegistrationStep, unused.Value!.NextStep);
		Assert.Equal(Globals.accountLoginContinuationRegistration, unused.Message);
		Assert.Equal(RegistrationContinuation.RegistrationStep, again.Value!.NextStep);
		Assert.False(blank.Succeeded);
		Assert.Null(blank.Value!.NextStep);
		Assert.Equal(0, fixture.Delivery.Calls);
		Assert.Equal(0, fixture.Gate.AccountCount);
	}

	/// <summary>The correct code activates the pending account. The result has no session.</summary>
	[Fact]
	public async Task Verify_EligibleCode_ActivatesWithoutASession()
	{
		var fixture = CreateFixture();
		var account = fixture.Gate.Seed("ada", "ada@example.com", RegistrationContinuation.PendingEmailVerificationStatusId, false);
		var code = fixture.Gate.IssueCode(account, "123456", fixture.Gate.DatabaseNowUtc.AddMinutes(10));

		var result = await fixture.Service.VerifyRegistrationAsync(new VerifyRegistrationRequest("ada@example.com", "123456"), CancellationToken.None);

		Assert.True(result.Succeeded);
		Assert.Equal(Globals.accountRegistrationVerificationCompleted, result.Message);
		Assert.True(account.IsEmailVerified);
		Assert.Equal(RegistrationContinuation.ActiveStatusId, account.StatusId);
		Assert.NotNull(code.ConsumedAtUtc);
		Assert.Equal(0, fixture.Delivery.Calls);
	}

	/// <summary>Disabled, locked, and deleted accounts stay on the shared unsuccessful result.</summary>
	[Fact]
	public async Task Continue_RestrictedAccounts_StayDeclined()
	{
		var fixture = CreateFixture();
		fixture.Gate.Seed("disabled", "disabled@example.com", 3, false);
		fixture.Gate.Seed("locked", "locked@example.com", 4, true);
		fixture.Gate.Seed("deleted", "deleted@example.com", 5, true);

		var disabled = await fixture.Service.ContinueLoginAsync(new LoginContinuationRequest("disabled@example.com"), CancellationToken.None);
		var locked = await fixture.Service.ContinueLoginAsync(new LoginContinuationRequest("locked"), CancellationToken.None);
		var deleted = await fixture.Service.ContinueLoginAsync(new LoginContinuationRequest("deleted@example.com"), CancellationToken.None);

		Assert.False(disabled.Succeeded);
		Assert.Equal(Globals.accountLoginContinuationDeclined, disabled.Message);
		Assert.Equal(disabled.Message, locked.Message);
		Assert.Equal(disabled.Message, deleted.Message);
		Assert.Null(disabled.Value!.NextStep);
		Assert.Null(locked.Value!.NextStep);
		Assert.Null(deleted.Value!.NextStep);
		Assert.Equal(0, fixture.Delivery.Calls);
	}

	/// <summary>
	/// Two accounts matched by one input are declined. No mail is sent, no code is consumed, and neither account changes.
	/// </summary>
	[Fact]
	public async Task Identity_TwoAccounts_DoNotContinueVerifyOrResend()
	{
		var fixture = CreateFixture();
		var pending = fixture.Gate.Seed("shared", "pending@example.com", RegistrationContinuation.PendingEmailVerificationStatusId, false);
		var code = fixture.Gate.IssueCode(pending, "123456", fixture.Gate.DatabaseNowUtc.AddMinutes(10));
		var other = fixture.Gate.Seed("other", "shared", RegistrationContinuation.ActiveStatusId, true);

		var continued = await fixture.Service.ContinueLoginAsync(new LoginContinuationRequest("shared"), CancellationToken.None);
		var verified = await fixture.Service.VerifyRegistrationAsync(new VerifyRegistrationRequest("shared", "123456"), CancellationToken.None);
		var resent = await fixture.Service.ResendRegistrationCodeAsync(new ResendRegistrationCodeRequest("shared"), CancellationToken.None);

		Assert.False(continued.Succeeded);
		Assert.Null(continued.Value!.NextStep);
		Assert.Equal(Globals.accountLoginContinuationDeclined, continued.Message);
		Assert.False(verified.Succeeded);
		Assert.Equal(Globals.accountCodeCouldNotBeAccepted, verified.Message);
		Assert.True(resent.Succeeded);
		Assert.Equal(Globals.accountRegistrationCodeResendAccepted, resent.Message);
		Assert.Equal(0, fixture.Delivery.Calls);
		Assert.Null(code.ConsumedAtUtc);
		Assert.Equal(0, code.AttemptCount);
		Assert.False(pending.IsEmailVerified);
		Assert.Equal(RegistrationContinuation.PendingEmailVerificationStatusId, pending.StatusId);
		Assert.True(other.IsEmailVerified);
		Assert.Equal(RegistrationContinuation.ActiveStatusId, other.StatusId);
		Assert.Single(pending.Codes);
	}

	/// <summary>One account whose username and email are the same value stays eligible for continuation and verification.</summary>
	[Fact]
	public async Task Identity_OneAccountMatchingBothFields_RemainsEligible()
	{
		var fixture = CreateFixture();
		var account = fixture.Gate.Seed("ada@example.com", "ada@example.com", RegistrationContinuation.PendingEmailVerificationStatusId, false);
		var code = fixture.Gate.IssueCode(account, "123456", fixture.Gate.DatabaseNowUtc.AddMinutes(10));

		var continued = await fixture.Service.ContinueLoginAsync(new LoginContinuationRequest("ada@example.com"), CancellationToken.None);
		var verified = await fixture.Service.VerifyRegistrationAsync(new VerifyRegistrationRequest("ada@example.com", "123456"), CancellationToken.None);

		Assert.Equal(RegistrationContinuation.VerificationStep, continued.Value!.NextStep);
		Assert.True(verified.Succeeded);
		Assert.NotNull(code.ConsumedAtUtc);
		Assert.True(account.IsEmailVerified);
		Assert.Equal(0, fixture.Delivery.Calls);
	}

	/// <summary>A code whose expiry equals the gate clock is rejected and the account stays pending.</summary>
	[Fact]
	public async Task Verify_ExpiryEqualToTheDatabaseClock_DoesNotActivate()
	{
		var fixture = CreateFixture();
		var account = fixture.Gate.Seed("ada", "ada@example.com", RegistrationContinuation.PendingEmailVerificationStatusId, false);
		fixture.Gate.IssueCode(account, "123456", fixture.Gate.DatabaseNowUtc);

		var result = await fixture.Service.VerifyRegistrationAsync(new VerifyRegistrationRequest("ada@example.com", "123456"), CancellationToken.None);

		Assert.False(result.Succeeded);
		Assert.Equal(AccountReasonCodes.ExpiredCode, result.ReasonCode);
		Assert.False(account.IsEmailVerified);
	}

	/// <summary>Five wrong codes exhaust the row. Another wrong code does not activate the account.</summary>
	[Fact]
	public async Task Verify_WrongCodes_StopAtFiveAttempts()
	{
		var fixture = CreateFixture();
		var account = fixture.Gate.Seed("ada", "ada@example.com", RegistrationContinuation.PendingEmailVerificationStatusId, false);
		var code = fixture.Gate.IssueCode(account, "123456", fixture.Gate.DatabaseNowUtc.AddMinutes(10));

		for (var attempt = 0; attempt < 5; attempt++)
		{
			var wrong = await fixture.Service.VerifyRegistrationAsync(new VerifyRegistrationRequest("ada@example.com", "000000"), CancellationToken.None);
			Assert.Equal(AccountReasonCodes.InvalidCode, wrong.ReasonCode);
		}

		var exhausted = await fixture.Service.VerifyRegistrationAsync(new VerifyRegistrationRequest("ada@example.com", "000000"), CancellationToken.None);
		Assert.Equal(AccountReasonCodes.RateLimited, exhausted.ReasonCode);
		Assert.Equal(5, code.AttemptCount);
		Assert.False(account.IsEmailVerified);
	}

	/// <summary>A code replaced by resend cannot activate the account, and the replacement remains the live code.</summary>
	[Fact]
	public async Task Verify_SupersededCode_DoesNotActivate()
	{
		var fixture = CreateFixture();
		var account = fixture.Gate.Seed("ada", "ada@example.com", RegistrationContinuation.PendingEmailVerificationStatusId, false);
		var original = fixture.Gate.IssueCode(account, "111111", fixture.Gate.DatabaseNowUtc.AddMinutes(10));
		fixture.Gate.DatabaseNowUtc = fixture.Gate.DatabaseNowUtc.AddSeconds(61);
		var resend = await fixture.Service.ResendRegistrationCodeAsync(new ResendRegistrationCodeRequest("ada@example.com"), CancellationToken.None);
		Assert.True(resend.Succeeded);
		Assert.Equal(account.NormalizedEmail, fixture.Delivery.LastDestination);

		var superseded = await fixture.Service.VerifyRegistrationAsync(new VerifyRegistrationRequest("ada@example.com", "111111"), CancellationToken.None);

		Assert.Equal(AccountReasonCodes.ConsumedCode, superseded.ReasonCode);
		Assert.False(account.IsEmailVerified);
		Assert.NotNull(original.ConsumedAtUtc);
		Assert.Contains(account.Codes, code => code.ConsumedAtUtc is null && code != original);
	}

	/// <summary>A status change after the code matches, and before activation, rolls the consumption back.</summary>
	[Fact]
	public async Task Verify_StatusChangeBeforeActivation_LeavesTheCodeUnused()
	{
		var fixture = CreateFixture();
		var account = fixture.Gate.Seed("ada", "ada@example.com", RegistrationContinuation.PendingEmailVerificationStatusId, false);
		var code = fixture.Gate.IssueCode(account, "123456", fixture.Gate.DatabaseNowUtc.AddMinutes(10));
		fixture.Gate.BeforeActivation = _ => account.StatusId = 3;

		var result = await fixture.Service.VerifyRegistrationAsync(new VerifyRegistrationRequest("ada@example.com", "123456"), CancellationToken.None);

		Assert.False(result.Succeeded);
		Assert.Null(code.ConsumedAtUtc);
		Assert.Equal(3, account.StatusId);
		Assert.False(account.IsEmailVerified);
	}

	/// <summary>A second resend inside the cooldown does not send mail. Delivery failure keeps the new code and the old code consumed.</summary>
	[Fact]
	public async Task Resend_CooldownAndDeliveryFailure_KeepTheReplacement()
	{
		var fixture = CreateFixture();
		var account = fixture.Gate.Seed("ada", "ada@example.com", RegistrationContinuation.PendingEmailVerificationStatusId, false);
		var original = fixture.Gate.IssueCode(account, "111111", fixture.Gate.DatabaseNowUtc.AddMinutes(10));
		fixture.Gate.DatabaseNowUtc = fixture.Gate.DatabaseNowUtc.AddSeconds(61);
		fixture.Delivery.Fail = true;

		var failed = await fixture.Service.ResendRegistrationCodeAsync(new ResendRegistrationCodeRequest("ada@example.com"), CancellationToken.None);
		var cooldown = await fixture.Service.ResendRegistrationCodeAsync(new ResendRegistrationCodeRequest("missing@example.com"), CancellationToken.None);
		var tooSoon = await fixture.Service.ResendRegistrationCodeAsync(new ResendRegistrationCodeRequest("ada@example.com"), CancellationToken.None);

		Assert.Equal(AccountReasonCodes.DeliveryFailed, failed.ReasonCode);
		Assert.True(cooldown.Succeeded);
		Assert.Equal(AccountReasonCodes.RateLimited, tooSoon.ReasonCode);
		Assert.NotNull(original.ConsumedAtUtc);
		Assert.Contains(account.Codes, code => code.ConsumedAtUtc is null);
		Assert.Equal(1, fixture.Delivery.Calls);
		Assert.False(account.IsEmailVerified);
	}

	/// <summary>A committed replacement with no stored email is reported as a delivery failure and is not sent to the typed username.</summary>
	[Fact]
	public async Task Resend_MissingStoredEmail_DoesNotSendTheTypedUsername()
	{
		var fixture = CreateFixture();
		var account = fixture.Gate.Seed("ada", "ada@example.com", RegistrationContinuation.PendingEmailVerificationStatusId, false);
		fixture.Gate.IssueCode(account, "111111", fixture.Gate.DatabaseNowUtc.AddMinutes(10));
		fixture.Gate.DatabaseNowUtc = fixture.Gate.DatabaseNowUtc.AddSeconds(61);
		fixture.Gate.OmitDeliveryEmail = true;

		var result = await fixture.Service.ResendRegistrationCodeAsync(new ResendRegistrationCodeRequest("ada"), CancellationToken.None);

		Assert.Equal(AccountReasonCodes.DeliveryFailed, result.ReasonCode);
		Assert.Equal(0, fixture.Delivery.Calls);
		Assert.Contains(account.Codes, code => code.ConsumedAtUtc is null);
	}

	/// <summary>One locked gate lets only one of two correct verifications activate the account. The lock is the in-memory seam, not a SQL lock.</summary>
	[Fact]
	public async Task Verify_ConcurrentCorrectCodes_ActivateOnce()
	{
		var fixture = CreateFixture();
		var account = fixture.Gate.Seed("ada", "ada@example.com", RegistrationContinuation.PendingEmailVerificationStatusId, false);
		fixture.Gate.IssueCode(account, "123456", fixture.Gate.DatabaseNowUtc.AddMinutes(10));

		var first = fixture.Service.VerifyRegistrationAsync(new VerifyRegistrationRequest("ada@example.com", "123456"), CancellationToken.None);
		var second = fixture.Service.VerifyRegistrationAsync(new VerifyRegistrationRequest("ada@example.com", "123456"), CancellationToken.None);
		var results = await Task.WhenAll(first, second);

		Assert.Equal(1, results.Count(result => result.Succeeded));
		Assert.True(account.IsEmailVerified);
	}

	private static AccessFixture CreateFixture()
	{
		var gate = new InMemoryAccountRegistrationGate();
		var delivery = new RecordingDelivery();
		var service = new AccountService(gate, new Pbkdf2PasswordHasher(), new RecordingAudit(), delivery);
		return new AccessFixture(gate, delivery, service);
	}

	private sealed record AccessFixture(InMemoryAccountRegistrationGate Gate, RecordingDelivery Delivery, AccountService Service);

	private sealed class RecordingDelivery : IAccountCodeDelivery
	{
		public int Calls { get; private set; }

		public string? LastDestination { get; private set; }

		public bool Fail { get; set; }

		public Task DeliverAsync(AccountCodeDeliveryCommand command, CancellationToken cancellationToken)
		{
			Calls++;
			LastDestination = command.DestinationNormalized;
			if (Fail)
			{
				throw new InvalidOperationException("The isolated delivery substitute failed.");
			}

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

/// <summary>In-memory substitute for the registration gate. One lock covers continuation, verification, and resend.</summary>
internal sealed class InMemoryAccountRegistrationGate : IAccountRegistrationGate
{
	private readonly object _gate = new();
	private readonly List<MemoryAccount> _accounts = new();

	internal DateTime DatabaseNowUtc { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	internal int AccountCount
	{
		get
		{
			lock (_gate)
			{
				return _accounts.Count;
			}
		}
	}

	internal Action<MemoryAccount>? BeforeActivation { get; set; }

	internal bool OmitDeliveryEmail { get; set; }

	internal MemoryAccount Seed(string username, string email, int statusId, bool isEmailVerified)
	{
		var account = new MemoryAccount
		{
			AccountId = Guid.NewGuid(),
			NormalizedUsername = username.Trim().ToUpperInvariant(),
			NormalizedEmail = email.Trim().ToUpperInvariant(),
			StatusId = statusId,
			IsEmailVerified = isEmailVerified
		};
		lock (_gate)
		{
			_accounts.Add(account);
		}

		return account;
	}

	internal MemoryCode IssueCode(MemoryAccount account, string plaintext, DateTime expiresAtUtc)
	{
		var hash = new Pbkdf2PasswordHasher().HashPassword(plaintext);
		var code = new MemoryCode
		{
			AccountCodeId = Guid.NewGuid(),
			Hash = hash.PasswordHash,
			Algorithm = hash.Algorithm,
			Version = hash.Version,
			ExpiresAtUtc = expiresAtUtc,
			IssuedAtUtc = DatabaseNowUtc,
			MaxAttempts = 5
		};
		lock (_gate)
		{
			account.Codes.Add(code);
		}

		return code;
	}

	public Task<LoginContinuationKind> ReadContinuationAsync(string normalizedIdentifier, CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			var matches = _accounts.Where(account =>
				account.NormalizedUsername == normalizedIdentifier
				|| account.NormalizedEmail == normalizedIdentifier).ToList();
			if (matches.Count == 0)
			{
				return Task.FromResult(LoginContinuationKind.Registration);
			}

			if (!AccountIdentityMatches.TrySingle(matches, account => account.AccountId, out var account))
			{
				return Task.FromResult(LoginContinuationKind.Declined);
			}

			return Task.FromResult(RegistrationContinuation.KindFor(account.StatusId, account.IsEmailVerified));
		}
	}

	public Task<RegistrationAccessDecision> VerifyAsync(
		string normalizedIdentifier,
		string submittedCode,
		IPasswordHasher hasher,
		CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			var account = Find(normalizedIdentifier);
			if (account is null || RegistrationContinuation.KindFor(account.StatusId, account.IsEmailVerified) != LoginContinuationKind.VerificationCode)
			{
				return Task.FromResult(new RegistrationAccessDecision(RegistrationAccessDecisionKind.Ineligible, account?.AccountId));
			}

			var live = account.Codes.Where(code => code.ConsumedAtUtc is null).OrderByDescending(code => code.IssuedAtUtc).FirstOrDefault();
			var matchesConsumed = account.Codes.Any(code =>
				code.ConsumedAtUtc is not null
				&& hasher.VerifyPassword(submittedCode, code.Hash, code.Algorithm, code.Version));
			if (matchesConsumed && (live is null || !hasher.VerifyPassword(submittedCode, live.Hash, live.Algorithm, live.Version)))
			{
				return Task.FromResult(new RegistrationAccessDecision(RegistrationAccessDecisionKind.Consumed, account.AccountId));
			}

			if (live is null)
			{
				return Task.FromResult(new RegistrationAccessDecision(RegistrationAccessDecisionKind.InvalidCode, account.AccountId));
			}

			if (AccountCodeLifetimes.IsExpired(live.ExpiresAtUtc, DatabaseNowUtc))
			{
				return Task.FromResult(new RegistrationAccessDecision(RegistrationAccessDecisionKind.Expired, account.AccountId));
			}

			if (live.AttemptCount >= live.MaxAttempts)
			{
				return Task.FromResult(new RegistrationAccessDecision(RegistrationAccessDecisionKind.RateLimited, account.AccountId));
			}

			if (!hasher.VerifyPassword(submittedCode, live.Hash, live.Algorithm, live.Version))
			{
				if (live.AttemptCount >= live.MaxAttempts)
				{
					return Task.FromResult(new RegistrationAccessDecision(RegistrationAccessDecisionKind.RateLimited, account.AccountId));
				}

				live.AttemptCount++;
				return Task.FromResult(new RegistrationAccessDecision(RegistrationAccessDecisionKind.InvalidCode, account.AccountId, live.AccountCodeId));
			}

			BeforeActivation?.Invoke(account);
			if (RegistrationContinuation.KindFor(account.StatusId, account.IsEmailVerified) != LoginContinuationKind.VerificationCode)
			{
				return Task.FromResult(new RegistrationAccessDecision(RegistrationAccessDecisionKind.EligibilityLost, account.AccountId));
			}

			live.ConsumedAtUtc = DatabaseNowUtc;
			account.StatusId = RegistrationContinuation.ActiveStatusId;
			account.IsEmailVerified = true;
			return Task.FromResult(new RegistrationAccessDecision(RegistrationAccessDecisionKind.Success, account.AccountId, live.AccountCodeId));
		}
	}

	public Task<RegistrationAccessDecision> PrepareResendAsync(
		string normalizedIdentifier,
		PasswordHashResult codeHash,
		string correlationId,
		CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			var account = Find(normalizedIdentifier);
			if (account is null || RegistrationContinuation.KindFor(account.StatusId, account.IsEmailVerified) != LoginContinuationKind.VerificationCode)
			{
				return Task.FromResult(new RegistrationAccessDecision(RegistrationAccessDecisionKind.Ineligible, account?.AccountId));
			}

			var latest = account.Codes.Count == 0 ? (DateTime?)null : account.Codes.Max(code => code.IssuedAtUtc);
			if (latest is not null && AccountCodeLifetimes.IsWithinCooldown(latest.Value, DatabaseNowUtc))
			{
				return Task.FromResult(new RegistrationAccessDecision(RegistrationAccessDecisionKind.Cooldown, account.AccountId));
			}

			foreach (var code in account.Codes.Where(code => code.ConsumedAtUtc is null))
			{
				code.ConsumedAtUtc = DatabaseNowUtc;
			}

			account.Codes.Add(new MemoryCode
			{
				AccountCodeId = Guid.NewGuid(),
				Hash = codeHash.PasswordHash,
				Algorithm = codeHash.Algorithm,
				Version = codeHash.Version,
				IssuedAtUtc = DatabaseNowUtc,
				ExpiresAtUtc = DatabaseNowUtc.AddMinutes(Globals.registrationVerificationCodeExpirationMinutes),
				MaxAttempts = 5
			});

			return Task.FromResult(new RegistrationAccessDecision(
				RegistrationAccessDecisionKind.Issued,
				account.AccountId,
				null,
				OmitDeliveryEmail ? null : account.NormalizedEmail));
		}
	}

	/// <summary>
	/// Returns the one matching account. Two distinct accounts are treated as no match.
	/// One account is one match even when both of its identity fields equal the input.
	/// </summary>
	private MemoryAccount? Find(string normalizedIdentifier)
	{
		var matches = _accounts.Where(account =>
			account.NormalizedUsername == normalizedIdentifier
			|| account.NormalizedEmail == normalizedIdentifier).ToList();
		return AccountIdentityMatches.TrySingle(matches, account => account.AccountId, out var account)
			? account
			: null;
	}
}

internal sealed class MemoryAccount
{
	internal Guid AccountId { get; set; }

	internal string NormalizedUsername { get; set; } = string.Empty;

	internal string NormalizedEmail { get; set; } = string.Empty;

	internal int StatusId { get; set; }

	internal bool IsEmailVerified { get; set; }

	internal List<MemoryCode> Codes { get; } = new();
}

internal sealed class MemoryCode
{
	internal Guid AccountCodeId { get; set; }

	internal byte[] Hash { get; set; } = [];

	internal string Algorithm { get; set; } = string.Empty;

	internal int Version { get; set; }

	internal DateTime ExpiresAtUtc { get; set; }

	internal DateTime? ConsumedAtUtc { get; set; }

	internal DateTime IssuedAtUtc { get; set; }

	internal int AttemptCount { get; set; }

	internal int MaxAttempts { get; set; }
}
