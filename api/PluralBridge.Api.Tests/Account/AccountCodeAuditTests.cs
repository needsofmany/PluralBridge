// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using PluralBridge.Api.Account;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PluralBridge.Api.Tests.Account;

[Collection(AccountTestGlobals.Collections.AccountDatabase)]
public sealed class AccountCodeAuditTests
{
	[Fact]
	public async Task Register_WritesSafeDetailJson_ForRegistrationCreatedAndCodeIssued()
	{
		var testId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}audit_register_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Audit Register";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();
			using var client = factory.CreateClient();

			var response = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					username,
					email,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				response.IsSuccessStatusCode,
				await response.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			var auditRows = await ReadAuditRowsAsync(account.AccountId);

			var registrationCreated = Assert.Single(
				auditRows,
				row => row.EventName == AccountAuditEvents.RegistrationCreated);

			Assert.Equal(AccountOutcomes.Succeeded, registrationCreated.Outcome);
			Assert.Equal(AccountReasonCodes.None, registrationCreated.ReasonCode);

			var registrationCreatedJson = ReadJsonObject(registrationCreated.SafeDetailJson);

			Assert.Equal(account.AccountId, ReadGuid(registrationCreatedJson, "accountId"));
			Assert.Equal(AccountText.NormalizeUsername(username), ReadString(registrationCreatedJson, "normalizedUsername"));
			Assert.Equal(AccountText.NormalizeEmail(email), ReadString(registrationCreatedJson, "normalizedEmail"));
			Assert.Equal(2, registrationCreatedJson.GetProperty("accountStatusId").GetInt32());

			var codeIssued = Assert.Single(
				auditRows,
				row => row.EventName == AccountAuditEvents.CodeIssued);

			Assert.Equal(AccountOutcomes.Succeeded, codeIssued.Outcome);
			Assert.Equal(AccountReasonCodes.None, codeIssued.ReasonCode);

			var codeIssuedJson = ReadJsonObject(codeIssued.SafeDetailJson);

			Assert.Equal(account.AccountId, ReadGuid(codeIssuedJson, "accountId"));
			Assert.Equal(AccountCodePurposes.RegistrationVerification, ReadString(codeIssuedJson, "codePurpose"));
			Assert.Equal(AccountDestinationTypes.Email, ReadString(codeIssuedJson, "destinationType"));
			Assert.Equal(AccountText.NormalizeEmail(email), ReadString(codeIssuedJson, "destinationNormalized"));
			Assert.NotEqual(Guid.Empty, ReadGuid(codeIssuedJson, "accountCodeId"));
			Assert.NotEqual(default, codeIssuedJson.GetProperty("expiresAtUtc").GetDateTime());
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task VerifyRegistration_WritesSafeDetailJson_WhenCodeLookupFindsNoRows()
	{
		var testId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);
		var email = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}audit_not_found_{testId}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var normalizedEmail = AccountText.NormalizeEmail(email);

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();
			using var client = factory.CreateClient();

			var response = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.VerifyRegistration,
				new VerifyRegistrationRequest(
					email,
					"123456"));

			Assert.False(response.IsSuccessStatusCode);

			var auditRow = await ReadLatestAuditRowBySafeDetailAsync(
				AccountAuditEvents.RegistrationVerificationRejected,
				AccountReasonCodes.InvalidCode,
				normalizedEmail);

			var detailJson = ReadJsonObject(auditRow.SafeDetailJson);

			Assert.Equal(normalizedEmail, ReadString(detailJson, "submittedEmailNormalized"));
			Assert.Equal(AccountCodePurposes.RegistrationVerification, ReadString(detailJson, "codePurpose"));
			Assert.Equal(AccountDestinationTypes.Email, ReadString(detailJson, "destinationType"));
			Assert.Equal(JsonValueKind.Null, detailJson.GetProperty("selectedAccountCodeId").ValueKind);
			Assert.Equal(JsonValueKind.Null, detailJson.GetProperty("selectedAccountId").ValueKind);
			Assert.Equal(JsonValueKind.Null, detailJson.GetProperty("expiresAtUtc").ValueKind);
			Assert.Equal(JsonValueKind.Null, detailJson.GetProperty("attemptCount").ValueKind);
			Assert.Equal(JsonValueKind.Null, detailJson.GetProperty("maxAttempts").ValueKind);
			Assert.NotEqual(default, detailJson.GetProperty("nowUtc").GetDateTime());
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task VerifyRegistration_WritesSafeDetailJson_WhenSelectedCodeIsExpired()
	{
		var testId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}audit_expired_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Audit Expired";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();
			using var client = factory.CreateClient();

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					username,
					email,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			var verificationCode = await ReadLatestOutboxCodeAsync(
				account.AccountId,
				AccountCodePurposes.RegistrationVerification);

			await ExpireLatestRegistrationCodeAsync(account.AccountId);

			var verifyResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.VerifyRegistration,
				new VerifyRegistrationRequest(
					email,
					verificationCode));

			Assert.False(verifyResponse.IsSuccessStatusCode);

			var auditRows = await ReadAuditRowsAsync(account.AccountId);
			var codeRejected = Assert.Single(
				auditRows,
				row => row.EventName == AccountAuditEvents.CodeRejected
					&& row.ReasonCode == AccountReasonCodes.ExpiredCode);

			Assert.Equal(AccountOutcomes.Expired, codeRejected.Outcome);

			AssertVerificationDetail(
				codeRejected,
				account.AccountId,
				email);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task VerifyRegistration_WritesSafeDetailJson_ForAcceptedConsumedAndVerifiedEvents()
	{
		var testId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}audit_verify_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Audit Verify";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory();
			using var client = factory.CreateClient();

			var registerResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					username,
					email,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.True(
				registerResponse.IsSuccessStatusCode,
				await registerResponse.Content.ReadAsStringAsync());

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
				?? throw new InvalidOperationException(TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			var verificationCode = await ReadLatestOutboxCodeAsync(
				account.AccountId,
				AccountCodePurposes.RegistrationVerification);

			var verifyResponse = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.VerifyRegistration,
				new VerifyRegistrationRequest(
					email,
					verificationCode));

			Assert.True(
				verifyResponse.IsSuccessStatusCode,
				await verifyResponse.Content.ReadAsStringAsync());

			var auditRows = await ReadAuditRowsAsync(account.AccountId);

			AssertVerificationDetail(
				Assert.Single(auditRows, row => row.EventName == AccountAuditEvents.CodeAccepted),
				account.AccountId,
				email);

			AssertVerificationDetail(
				Assert.Single(auditRows, row => row.EventName == AccountAuditEvents.CodeConsumed),
				account.AccountId,
				email);

			AssertVerificationDetail(
				Assert.Single(auditRows, row => row.EventName == AccountAuditEvents.RegistrationVerified),
				account.AccountId,
				email);
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	[Fact]
	public async Task Register_WritesSafeDetailJson_WhenCodeDeliveryFails()
	{
		var testId = Guid.NewGuid().ToString(Globals.guidFormatNoHyphens);
		var username = $"{AccountTestGlobals.TestAccounts.UsernamePrefix}audit_delivery_failed_{testId}";
		var email = $"{username}@{AccountTestGlobals.TestAccounts.EmailDomain}";
		var displayName = $"{AccountTestGlobals.TestAccounts.DisplayNamePrefix}Audit Delivery Failed";

		await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();

		try
		{
			await using var factory = AccountTestHost.CreateFactory(services =>
			{
				services.RemoveAll<IAccountCodeDelivery>();
				services.AddScoped<IAccountCodeDelivery, FailingAccountCodeDelivery>();
			});

			using var client = factory.CreateClient();

			var response = await client.PostAsJsonAsync(
				AccountTestGlobals.Routes.Register,
				new RegisterAccountRequest(
					username,
					email,
					displayName,
					AccountTestGlobals.TestAccounts.DefaultPassword));

			Assert.False(response.IsSuccessStatusCode);

			var account = await AccountTestDatabase.ReadAccountStateAsync(username)
			              ?? throw new InvalidOperationException(TestGlobals.Diagnostics.RuntimeTestAccountWasNotCreated);

			var auditRows = await ReadAuditRowsAsync(account.AccountId);
			var registrationRejected = Assert.Single(
				auditRows,
				row => row is { EventName: AccountAuditEvents.RegistrationRejected, ReasonCode: AccountReasonCodes.DeliveryFailed });

			Assert.Equal(AccountOutcomes.Failed, registrationRejected.Outcome);

			var detailJson = ReadJsonObject(registrationRejected.SafeDetailJson);

			Assert.Equal(AccountCodePurposes.RegistrationVerification, ReadString(detailJson, "codePurpose"));
			Assert.Equal(AccountDestinationTypes.Email, ReadString(detailJson, "destinationType"));
			Assert.Equal(AccountText.NormalizeEmail(email), ReadString(detailJson, "destinationNormalized"));
		}
		finally
		{
			await AccountTestDatabase.CleanupRuntimeTestAccountsAsync();
		}
	}

	private static async Task<IReadOnlyList<AuditRow>> ReadAuditRowsAsync(Guid accountId)
	{
		const string sql = """
			SELECT
				EventName,
				Outcome,
				ReasonCode,
				SafeDetailJson
			FROM dbo.pb_account_audit_events
			WHERE TargetAccountId = @AccountId
			ORDER BY CreatedAtUtc;
			""";

		var rows = new List<AuditRow>();

		await using var connection = new SqlConnection(AccountTestDatabase.GetConnectionString());
		await connection.OpenAsync();

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);

		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			rows.Add(new AuditRow(
				reader.GetString(0),
				reader.GetString(1),
				reader.GetString(2),
				reader.IsDBNull(3) ? null : reader.GetString(3)));
		}

		return rows;
	}

	private static async Task<AuditRow> ReadLatestAuditRowBySafeDetailAsync(
		string eventName,
		string reasonCode,
		string safeDetailText)
	{
		const string sql = """
			SELECT TOP (1)
				EventName,
				Outcome,
				ReasonCode,
				SafeDetailJson
			FROM dbo.pb_account_audit_events
			WHERE EventName = @EventName
			  AND ReasonCode = @ReasonCode
			  AND SafeDetailJson LIKE @SafeDetailPattern
			ORDER BY CreatedAtUtc DESC;
			""";

		await using var connection = new SqlConnection(AccountTestDatabase.GetConnectionString());
		await connection.OpenAsync();

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@EventName", eventName);
		command.Parameters.AddWithValue("@ReasonCode", reasonCode);
		command.Parameters.AddWithValue("@SafeDetailPattern", $"%{safeDetailText}%");

		await using var reader = await command.ExecuteReaderAsync();

		if (!await reader.ReadAsync())
		{
			throw new InvalidOperationException($"Expected audit row was not found for {eventName} / {reasonCode}.");
		}

		return new AuditRow(
			reader.GetString(0),
			reader.GetString(1),
			reader.GetString(2),
			reader.IsDBNull(3) ? null : reader.GetString(3));
	}

	private static async Task<string> ReadLatestOutboxCodeAsync(
		Guid accountId,
		string codePurpose)
	{
		const string sql = """
			SELECT TOP (1)
				PlaintextCode
			FROM dbo.pb_account_code_delivery_outbox
			WHERE AccountId = @AccountId
			  AND CodePurpose = @CodePurpose;
			""";

		await using var connection = new SqlConnection(AccountTestDatabase.GetConnectionString());
		await connection.OpenAsync();

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@CodePurpose", codePurpose);

		var value = await command.ExecuteScalarAsync();

		return value?.ToString()
			?? throw new InvalidOperationException("Expected account code delivery outbox row was not found.");
	}

	private static async Task ExpireLatestRegistrationCodeAsync(Guid accountId)
	{
		const string sql = """
			UPDATE dbo.pb_account_codes
			SET ExpiresAtUtc = DATEADD(MINUTE, -1, SYSUTCDATETIME())
			WHERE AccountId = @AccountId
			  AND CodePurpose = @CodePurpose
			  AND DestinationType = @DestinationType;
			""";

		await using var connection = new SqlConnection(AccountTestDatabase.GetConnectionString());
		await connection.OpenAsync();

		await using var command = new SqlCommand(sql, connection);

		command.Parameters.AddWithValue("@AccountId", accountId);
		command.Parameters.AddWithValue("@CodePurpose", AccountCodePurposes.RegistrationVerification);
		command.Parameters.AddWithValue("@DestinationType", AccountDestinationTypes.Email);

		var rowsAffected = await command.ExecuteNonQueryAsync();

		Assert.Equal(1, rowsAffected);
	}

	private static void AssertVerificationDetail(
		AuditRow auditRow,
		Guid accountId,
		string email)
	{
		Assert.False(string.IsNullOrWhiteSpace(auditRow.SafeDetailJson));

		var detailJson = ReadJsonObject(auditRow.SafeDetailJson);

		Assert.Equal(AccountText.NormalizeEmail(email), ReadString(detailJson, "submittedEmailNormalized"));
		Assert.Equal(AccountCodePurposes.RegistrationVerification, ReadString(detailJson, "codePurpose"));
		Assert.Equal(AccountDestinationTypes.Email, ReadString(detailJson, "destinationType"));
		Assert.Equal(accountId, ReadGuid(detailJson, "selectedAccountId"));
		Assert.NotEqual(Guid.Empty, ReadGuid(detailJson, "selectedAccountCodeId"));
		Assert.NotEqual(default, detailJson.GetProperty("expiresAtUtc").GetDateTime());
		Assert.Equal(0, detailJson.GetProperty("attemptCount").GetInt32());
		Assert.True(detailJson.GetProperty("maxAttempts").GetInt32() > 0);
		Assert.NotEqual(default, detailJson.GetProperty("nowUtc").GetDateTime());
	}

	private static JsonElement ReadJsonObject(string? json)
	{
		Assert.False(string.IsNullOrWhiteSpace(json));

		using var document = JsonDocument.Parse(json);

		return document.RootElement.Clone();
	}

	private static string ReadString(JsonElement json, string propertyName)
	{
		return json.GetProperty(propertyName).GetString()
			?? throw new InvalidOperationException($"JSON property {propertyName} was null.");
	}

	private static Guid ReadGuid(JsonElement json, string propertyName)
	{
		return Guid.Parse(ReadString(json, propertyName));
	}

	private sealed class FailingAccountCodeDelivery : IAccountCodeDelivery
	{
		public Task DeliverAsync(
			AccountCodeDeliveryCommand command,
			CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("Test account code delivery failure.");
		}
	}

	private sealed record AuditRow(
		string EventName,
		string Outcome,
		string ReasonCode,
		string? SafeDetailJson);
}
