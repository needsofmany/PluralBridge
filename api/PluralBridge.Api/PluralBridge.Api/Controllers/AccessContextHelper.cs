using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers
{
	internal static class AccessContextHelper
	{
		/// <summary>
		/// Return an Account record by the user's email address
		/// </summary>
		/// <param name="connection">current database connection</param>
		/// <param name="email">user's email address</param>
		/// <returns>return the user's account record</returns>
		internal static async Task<Account?> ReadAccountByEmailAsync(
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
		/// Reads the active system membership for one account.
		/// </summary>
		/// <param name="connection">current database connection</param>
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

			await using var command = new SqlCommand(sql, connection);
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
					reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
					reader.GetInt32(6),
					reader.GetBoolean(7));

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
		/// Reads all roles attached to one system membership.
		/// </summary>
		/// <param name="connection">current database connection</param>
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
			                       r.RoleDesc,
			                       r.DisplayOrder,
			                       r.IsActive
			                   FROM dbo.pb_system_membership_roles AS mr
			                   INNER JOIN dbo.pb_roles AS r
			                       ON r.RoleId = mr.RoleId
			                   WHERE mr.SystemMembershipId = @SystemMembershipId
			                   ORDER BY r.DisplayOrder, r.RoleName, r.RoleId;
			                   """;

			await using var command = new SqlCommand(sql, connection);
			command.Parameters.AddWithValue("@SystemMembershipId", systemMembershipId);

			var roles = new List<Role>();

			await using var reader = await command.ExecuteReaderAsync();

			while (await reader.ReadAsync())
			{
				roles.Add(new Role(
					reader.GetInt32(0),
					reader.GetString(1),
					reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
					reader.GetInt32(3),
					reader.GetBoolean(4)));
			}

			return roles;
		}

		/// <summary>
		/// Resolves the current account and its available system memberships.
		/// </summary>
		/// <param name="connection">current database connection</param>
		/// <returns></returns>
		internal static async Task<AccessContext?> ResolveCurrentAccessAsync(SqlConnection connection)
		{
			var currentAccount = await ResolveCurrentAccountAsync(connection);
			if (currentAccount is null)
			{
				return null;
			}

			var membershipAccess = await ResolveMembershipAccessAsync(
				connection,
				currentAccount);

			var currentSystem = await ResolveCurrentSystemFromMembershipAccessAsync(
				connection,
				membershipAccess,
				CancellationToken.None);

			if (currentSystem is null)
			{
				return null;
			}

			return new AccessContext(
				currentAccount,
				membershipAccess,
				currentSystem);
		}

		/// <summary>
		/// Retrieve the root System associated with this membership
		/// </summary>
		/// <param name="connection">current database connection</param>
		/// <param name="membershipAccess"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		internal static async Task<CurrentSystem?> ResolveCurrentSystemFromMembershipAccessAsync(
			SqlConnection connection,
			IReadOnlyList<SystemMembership> membershipAccess,
			CancellationToken cancellationToken)
		{
			var currentMembership = membershipAccess.FirstOrDefault();

			if (currentMembership is null)
			{
				return null;
			}

			return await ResolveCurrentSystemAsync(
				connection,
				currentMembership.SystemId,
				currentMembership.SystemMembershipId,
				cancellationToken);
		}

		/// <summary>
		/// does the account for the current System have access to this resource?
		/// </summary>
		/// <param name="accessContext"></param>
		/// <returns></returns>
		internal static bool IsAuthorizedForCurrentSystem(
			AccessContext accessContext)
		{
			return accessContext.MembershipAccess.Any(membership =>
				membership.SystemId == accessContext.CurrentSystem.SystemId
				&& membership.SystemMembershipId == accessContext.CurrentSystem.SystemMembershipId
				&& membership.MembershipStatus.IsActive
				&& string.Equals(
					membership.MembershipStatus.StatusName,
					"Active",
					StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Resolves the current working account for the Chapter 2 safe-spine path.
		/// </summary>
		/// <param name="connection">current database connection</param>
		/// <returns></returns>
		internal static async Task<Account?> ResolveCurrentAccountAsync(SqlConnection connection)
		{
			const string currentAccountEmail = "demo@thepluralbridge.local";

			var account = await ReadAccountByEmailAsync(
				connection,
				currentAccountEmail);

			return account;
		}

		/// <summary>
		/// Resolves the active system memberships available to the current account.
		/// </summary>
		/// <param name="connection"></param>
		/// <param name="currentAccount"></param>
		/// <returns></returns>
		internal static async Task<IReadOnlyList<SystemMembership>> ResolveMembershipAccessAsync(
			SqlConnection connection,
			Account currentAccount)
		{
			var membership = await ReadActiveMembershipAsync(
				connection,
				currentAccount.AccountId);

			if (membership is null)
			{
				return [];
			}

			return [membership];
		}

		/// <summary>
		/// Returns the current System for this Account's membership
		/// </summary>
		/// <param name="connection">current database connection</param>
		/// <param name="systemId"></param>
		/// <param name="systemMembershipId"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		internal static async Task<CurrentSystem?> ResolveCurrentSystemAsync(
			SqlConnection connection,
			Guid systemId,
			Guid systemMembershipId,
			CancellationToken cancellationToken)
		{
			const string sql = """
			                   SELECT TOP (1)
			                       s.SystemId,
			                       s.SystemName
			                   FROM dbo.pb_systems AS s
			                   WHERE s.SystemId = @SystemId;
			                   """;

			await using var command = new SqlCommand(sql, connection);
			command.Parameters.AddWithValue("@SystemId", systemId);

			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			if (!await reader.ReadAsync(cancellationToken))
			{
				return null;
			}

			return new CurrentSystem(
				reader.GetGuid(reader.GetOrdinal("SystemId")),
				reader.IsDBNull(reader.GetOrdinal("SystemName"))
					? null
					: reader.GetString(reader.GetOrdinal("SystemName")),
				systemMembershipId);
		}



		/// <summary>
		/// Holds the current Account, it's membership list and the current System
		/// </summary>
		/// <param name="CurrentAccount"></param>
		/// <param name="MembershipAccess"></param>
		internal sealed record AccessContext(
			Account CurrentAccount,
			IReadOnlyList<SystemMembership> MembershipAccess,
			CurrentSystem CurrentSystem);

		/// <summary>
		/// Represents one account status row.
		/// </summary>
		/// <param name="AccountStatusId"></param>
		/// <param name="StatusName"></param>
		/// <param name="StatusDesc"></param>
		/// <param name="DisplayOrder"></param>
		/// <param name="IsActive"></param>
		internal sealed record AccountStatus(
			int AccountStatusId,
			string StatusName,
			string StatusDesc,
			int DisplayOrder,
			bool IsActive);

		/// <summary>
		/// principal / login identity
		/// </summary>
		/// <param name="AccountId"></param>
		/// <param name="Email"></param>
		/// <param name="DisplayName"></param>
		/// <param name="AccountStatusId"></param>
		/// <param name="AccountStatus"></param>
		/// <param name="CreatedAtUtc"></param>
		/// <param name="UpdatedAtUtc"></param>
		internal sealed record Account(
			Guid AccountId,
			string Email,
			string DisplayName,
			int AccountStatusId,
			AccountStatus AccountStatus,
			DateTime CreatedAtUtc,
			DateTime? UpdatedAtUtc);

		/// <summary>
		/// permission bundle attached to that relationship
		/// </summary>
		/// <param name="RoleId"></param>
		/// <param name="RoleName"></param>
		/// <param name="RoleDesc"></param>
		/// <param name="DisplayOrder"></param>
		/// <param name="IsActive"></param>
		internal sealed record Role(
			int RoleId,
			string RoleName,
			string RoleDesc,
			int DisplayOrder,
			bool IsActive);

		/// <summary>
		/// Status of a membership for the user's Accout
		/// </summary>
		/// <param name="MembershipStatusId"></param>
		/// <param name="StatusName"></param>
		/// <param name="StatusDesc"></param>
		/// <param name="DisplayOrder"></param>
		/// <param name="IsActive"></param>
		internal sealed record MembershipStatus(
			int MembershipStatusId,
			string StatusName,
			string StatusDesc,
			int DisplayOrder,
			bool IsActive);


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
		internal sealed record SystemMembership(
			Guid SystemMembershipId,
			Guid AccountId,
			Guid SystemId,
			int MembershipStatusId,
			MembershipStatus MembershipStatus,
			IReadOnlyList<Role> Roles,
			DateTime CreatedAtUtc,
			DateTime? UpdatedAtUtc);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="SystemId"></param>
		/// <param name="SystemName"></param>
		/// <param name="SystemMembershipId"></param>
		internal sealed record CurrentSystem(
			Guid SystemId,
			string? SystemName,
			Guid SystemMembershipId);
	}
}
