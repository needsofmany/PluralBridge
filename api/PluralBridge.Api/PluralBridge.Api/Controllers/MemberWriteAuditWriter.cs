using System.Data;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

internal static class MemberWriteAuditWriter
{
	internal const string MemberAddOperation = "member.add";
	internal const string MemberEditOperation = "member.edit";

	internal sealed record MemberWriteAuditInput(
		Guid SystemId,
		Guid AccountId,
		Guid? SystemMembershipId,
		Guid MemberId,
		string Operation,
		string? RequestTraceId);

	internal static async Task WriteAsync(
		SqlConnection connection,
		MemberWriteAuditInput input,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(connection);

		ValidateInput(input);

		if (connection.State != ConnectionState.Open)
		{
			await connection.OpenAsync(cancellationToken);
		}

		await using var command = connection.CreateCommand();

		command.CommandText = @"
INSERT INTO dbo.pb_member_write_audit
(
    SystemId,
    AccountId,
    SystemMembershipId,
    MemberId,
    Operation,
    RequestTraceId
)
VALUES
(
    @SystemId,
    @AccountId,
    @SystemMembershipId,
    @MemberId,
    @Operation,
    @RequestTraceId
);";

		AddGuidParameter(command, "@SystemId", input.SystemId);
		AddGuidParameter(command, "@AccountId", input.AccountId);
		AddNullableGuidParameter(command, "@SystemMembershipId", input.SystemMembershipId);
		AddGuidParameter(command, "@MemberId", input.MemberId);
		AddStringParameter(command, "@Operation", input.Operation, 32);
		AddNullableStringParameter(command, "@RequestTraceId", input.RequestTraceId, 100);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static void ValidateInput(MemberWriteAuditInput input)
	{
		if (input.SystemId == Guid.Empty)
		{
			throw new ArgumentException("SystemId is required.", nameof(input));
		}

		if (input.AccountId == Guid.Empty)
		{
			throw new ArgumentException("AccountId is required.", nameof(input));
		}

		if (input.MemberId == Guid.Empty)
		{
			throw new ArgumentException("MemberId is required.", nameof(input));
		}

		if (string.IsNullOrWhiteSpace(input.Operation))
		{
			throw new ArgumentException("Operation is required.", nameof(input));
		}

		if (input.Operation is not MemberAddOperation and not MemberEditOperation)
		{
			throw new ArgumentException("Operation must be member.add or member.edit.", nameof(input));
		}

		if (input.RequestTraceId is { Length: > 100 })
		{
			throw new ArgumentException("RequestTraceId cannot exceed 100 characters.", nameof(input));
		}
	}

	private static void AddGuidParameter(SqlCommand command, string name, Guid value)
	{
		var parameter = command.Parameters.Add(name, SqlDbType.UniqueIdentifier);
		parameter.Value = value;
	}

	private static void AddNullableGuidParameter(SqlCommand command, string name, Guid? value)
	{
		var parameter = command.Parameters.Add(name, SqlDbType.UniqueIdentifier);
		parameter.Value = value.HasValue ? value.Value : DBNull.Value;
	}

	private static void AddStringParameter(SqlCommand command, string name, string value, int size)
	{
		var parameter = command.Parameters.Add(name, SqlDbType.NVarChar, size);
		parameter.Value = value;
	}

	private static void AddNullableStringParameter(SqlCommand command, string name, string? value, int size)
	{
		var parameter = command.Parameters.Add(name, SqlDbType.NVarChar, size);
		parameter.Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
	}
}