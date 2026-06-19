using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the Phase 2B read-only proof endpoint for the current proof context.
/// This controller verifies that the API can reach the validated PluralBridge cloud proof database
/// and returns the system identifier plus table counts needed by the browser proof surface.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class MeController(IConfiguration configuration) : ControllerBase
{
	/// <summary>
	/// Returns the current Phase 2B proof context.
	/// The response is intentionally read-only and includes the proof system plus validated table counts
	/// so the browser can confirm that it is receiving real database-backed data.
	/// </summary>
	/// <returns>
	/// HTTP 200 with proof metadata, the selected proof system, write capability set to false,
	/// and counts for the Phase 2B source and 1-6 slice tables.
	/// </returns>
	[HttpGet]
	public async Task<IActionResult> Get()
	{
		var connectionString = configuration.GetConnectionString("PluralBridgeProof");

		if (string.IsNullOrWhiteSpace(connectionString))
		{
			return Problem(
				title: "Missing connection string",
				detail: "ConnectionStrings:PluralBridgeProof was not found.",
				statusCode: StatusCodes.Status500InternalServerError);
		}

		// get connection to the database
		await using SqlConnection connection = new(connectionString);
		await connection.OpenAsync();

		var currentAccount = await ResolveCurrentAccountAsync(connection);
		if (currentAccount is null)
		{
			return Problem(
				title: "Current account not found",
				detail: "The configured Chapter 2 current account could not be resolved.",
				statusCode: StatusCodes.Status500InternalServerError);
		}

		var counts = await ReadCountsAsync(connection);
		var proofSystem = await ReadProofSystemAsync(connection);

		return Ok(new
		{
			api = "PluralBridge.Api",
			phase = "Phase 2B",
			mode = "read-only proof",
			database = "PluralBridgeCloudProof001",
			canWrite = false,
			currentAccount,
			proofSystem,
			counts
		});
	}

	/// <summary>
	/// Reads count totals from the source and 1-6 proof tables in the validated database.
	/// These totals are used as a compact integrity check for the Phase 2B browser proof.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <returns>A dictionary keyed by API-facing count names.</returns>
	private static async Task<Dictionary<string, long>> ReadCountsAsync(SqlConnection connection)
	{
		const string sql = """
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_source_systems;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_import_batches;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_systems;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_members;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_privacy_buckets;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_custom_fields;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_front_history;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_source_records;
		                   SELECT CAST(COUNT_BIG(*) AS bigint) FROM dbo.pb_source_id_map;
		                   """;

		string[] names =
		[
			"sourceSystems",
			"importBatches",
			"systems",
			"members",
			"privacyBuckets",
			"customFields",
			"frontHistory",
			"sourceRecords",
			"sourceIdMappings"
		];

		Dictionary<string, long> counts = new();

		await using SqlCommand command = new(sql, connection);
		await using var reader = await command.ExecuteReaderAsync();

		for (var index = 0; index < names.Length; index++)
		{
			if (await reader.ReadAsync())
			{
				counts[names[index]] = reader.GetInt64(0);
			}

			if (index < names.Length - 1)
			{
				await reader.NextResultAsync();
			}
		}

		return counts;
	}

	/// <summary>
	/// Reads the first proof system from the validated database.
	/// Phase 2B uses this fixed proof context until Phase 3 replaces it with login/session mapping.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <returns>The selected proof system, or null when the proof database has no system row.</returns>
	private static async Task<ProofSystem?> ReadProofSystemAsync(SqlConnection connection)
	{
		const string sql = """
		                   SELECT TOP (1)
		                       SystemId,
		                       SystemName
		                   FROM dbo.pb_systems
		                   ORDER BY CreatedAtUtc, SystemId;
		                   """;

		await using SqlCommand command = new(sql, connection);
		await using var reader = await command.ExecuteReaderAsync();

		if (!await reader.ReadAsync())
		{
			return null;
		}

		return new ProofSystem(
			reader.GetGuid(0),
			reader.IsDBNull(1) ? null : reader.GetString(1));
	}

	/// <summary>
	/// Resolves the current working account for the Chapter 2 safe-spine path.
	/// </summary>
	/// <param name="connection"></param>
	/// <returns></returns>
	private static async Task<Account?> ResolveCurrentAccountAsync(SqlConnection connection)
	{
		const string currentAccountEmail = "demo@thepluralbridge.local";

		var account = await ReadAccountByEmailAsync(
			connection,
			currentAccountEmail);

		return account;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="connection"></param>
	/// <param name="email"></param>
	/// <returns></returns>
	private static async Task<Account?> ReadAccountByEmailAsync(
	SqlConnection connection,
	string email)
	{
		const string sql = """
		                   SELECT TOP (1)
		                       a.AccountId,
		                       a.Email,
		                       a.DisplayName,
		                       a.AccountStatusId,
		                       s.StatusName,
		                       s.StatusDesc,
		                       s.DisplayOrder,
		                       s.IsActive,
		                       a.CreatedAtUtc,
		                       a.UpdatedAtUtc
		                   FROM dbo.pb_accounts AS a
		                   INNER JOIN dbo.pb_account_statuses AS s
		                       ON s.AccountStatusId = a.AccountStatusId
		                   WHERE a.Email = @Email
		                   ORDER BY a.CreatedAtUtc, a.AccountId;
		                   """;

		await using SqlCommand command = new(sql, connection);
		command.Parameters.AddWithValue("@Email", email);

		await using var reader = await command.ExecuteReaderAsync();

		if (!await reader.ReadAsync())
		{
			return null;
		}

		var accountStatus = new AccountStatus(
			reader.GetInt32(3),
			reader.GetString(4),
			reader.GetString(5),
			reader.GetInt32(6),
			reader.GetBoolean(7));

		return new Account(
			reader.GetGuid(0),
			reader.GetString(1),
			reader.GetString(2),
			reader.GetInt32(3),
			accountStatus,
			reader.GetDateTime(8),
			reader.IsDBNull(9) ? null : reader.GetDateTime(9));
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="connection"></param>
	/// <param name="accountId"></param>
	/// <returns></returns>
	private static async Task<SystemMembership?> ReadActiveMembershipAsync(
		SqlConnection connection,
		Guid accountId)
	{
		const string sql = """
		                   SELECT TOP (1)
		                       m.SystemMembershipId,
		                       m.AccountId,
		                       m.SystemId,
		                       m.MembershipStatusId,
		                       s.StatusName,
		                       s.StatusDesc,
		                       s.DisplayOrder,
		                       s.IsActive,
		                       m.CreatedAtUtc,
		                       m.UpdatedAtUtc
		                   FROM dbo.pb_system_memberships AS m
		                   INNER JOIN dbo.pb_system_membership_statuses AS s
		                       ON s.MembershipStatusId = m.MembershipStatusId
		                   WHERE m.AccountId = @AccountId
		                     AND s.StatusName = 'Active'
		                   ORDER BY m.CreatedAtUtc, m.SystemMembershipId;
		                   """;

		await using SqlCommand command = new(sql, connection);
		command.Parameters.AddWithValue("@AccountId", accountId);

		Guid systemMembershipId;
		Guid membershipAccountId;
		Guid systemId;
		int membershipStatusId;
		MembershipStatus membershipStatus;
		DateTime createdAtUtc;
		DateTime? updatedAtUtc;

		await using (var reader = await command.ExecuteReaderAsync())
		{
			if (!await reader.ReadAsync())
			{
				return null;
			}

			systemMembershipId = reader.GetGuid(0);
			membershipAccountId = reader.GetGuid(1);
			systemId = reader.GetGuid(2);
			membershipStatusId = reader.GetInt32(3);

			membershipStatus = new MembershipStatus(
				membershipStatusId,
				reader.GetString(4),
				reader.GetInt32(5),
				reader.GetBoolean(6),
				reader.GetDateTime(7));

			createdAtUtc = reader.GetDateTime(8);
			updatedAtUtc = reader.IsDBNull(9) ? null : reader.GetDateTime(9);
		}

		var roles = await ReadRolesForMembershipAsync(
			connection,
			systemMembershipId);

		return new SystemMembership(
			systemMembershipId,
			membershipAccountId,
			systemId,
			membershipStatusId,
			membershipStatus,
			roles,
			createdAtUtc,
			updatedAtUtc);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="connection"></param>
	/// <param name="systemMembershipId"></param>
	/// <returns></returns>
	private static async Task<IReadOnlyList<Role>> ReadRolesForMembershipAsync(
		SqlConnection connection,
		Guid systemMembershipId)
	{
		const string sql = """
		                   SELECT
		                       r.RoleId,
		                       r.RoleName,
		                       r.SortOrder,
		                       r.IsActive,
		                       r.CreatedAtUtc
		                   FROM dbo.pb_system_membership_roles AS mr
		                   INNER JOIN dbo.pb_roles AS r
		                       ON r.RoleId = mr.RoleId
		                   WHERE mr.SystemMembershipId = @SystemMembershipId
		                   ORDER BY r.SortOrder, r.RoleName, r.RoleId;
		                   """;

		await using SqlCommand command = new(sql, connection);
		command.Parameters.AddWithValue("@SystemMembershipId", systemMembershipId);

		List<Role> roles = [];

		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			roles.Add(new Role(
				reader.GetInt32(0),
				reader.GetString(1),
				reader.GetInt32(2),
				reader.GetBoolean(3),
				reader.GetDateTime(4)));
		}

		return roles;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="SystemId"></param>
	/// <param name="SystemName"></param>
	private sealed record ProofSystem(
		Guid SystemId, 
		string? SystemName);

	/// <summary>
	/// Represents one account status row.
	/// </summary>
	/// <param name="AccountStatusId"></param>
	/// <param name="StatusName"></param>
	/// <param name="StatusDesc"></param>
	/// <param name="DisplayOrder"></param>
	/// <param name="IsActive"></param>
	private sealed record AccountStatus(
		int AccountStatusId,
		string StatusName,
		string StatusDesc,
		int DisplayOrder,
		bool IsActive)
	{

	};
	/// <summary>
	/// 
	/// </summary>
	/// <param name="AccountId"></param>
	/// <param name="Email"></param>
	/// <param name="DisplayName"></param>
	/// <param name="AccountStatusId"></param>
	/// <param name="AccountStatus"></param>
	/// <param name="CreatedAtUtc"></param>
	/// <param name="UpdatedAtUtc"></param>
	private sealed record Account(
		Guid AccountId,
		string Email,
		string DisplayName,
		int AccountStatusId,
		AccountStatus AccountStatus,
		DateTime CreatedAtUtc,
		DateTime? UpdatedAtUtc);

	/// <summary>
	/// 
	/// </summary>
	/// <param name="RoleId"></param>
	/// <param name="RoleName"></param>
	/// <param name="SortOrder"></param>
	/// <param name="IsActive"></param>
	/// <param name="CreatedAtUtc"></param>
	private sealed record Role(
		int RoleId,
		string RoleName,
		int SortOrder,
		bool IsActive,
		DateTime CreatedAtUtc);
	
	/// <summary>
	/// 
	/// </summary>
	/// <param name="MembershipStatusId"></param>
	/// <param name="StatusName"></param>
	/// <param name="SortOrder"></param>
	/// <param name="IsActive"></param>
	/// <param name="CreatedAtUtc"></param>
	private sealed record MembershipStatus(
		int MembershipStatusId,
		string StatusName,
		int SortOrder,
		bool IsActive,
		DateTime CreatedAtUtc);

	/// <summary>
	/// 
	/// </summary>
	/// <param name="SystemMembershipId"></param>
	/// <param name="AccountId"></param>
	/// <param name="SystemId"></param>
	/// <param name="MembershipStatusId"></param>
	/// <param name="MembershipStatus"></param>
	/// <param name="Roles"></param>
	/// <param name="CreatedAtUtc"></param>
	/// <param name="UpdatedAtUtc"></param>
	private sealed record SystemMembership(
		Guid SystemMembershipId,
		Guid AccountId,
		Guid SystemId,
		int MembershipStatusId,
		MembershipStatus MembershipStatus,
		IReadOnlyList<Role> Roles,
		DateTime CreatedAtUtc,
		DateTime? UpdatedAtUtc);
}
