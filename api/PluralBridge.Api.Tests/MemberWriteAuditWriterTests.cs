using Microsoft.Data.SqlClient;
using PluralBridge.Api.Controllers;
using Xunit;

// ReSharper disable once CheckNamespace
namespace PluralBridge.Api.Tests.Controllers;

public sealed class MemberWriteAuditWriterTests
{
	[Fact]
	public void MemberAddAuditInput_AllowsValidInputShape()
	{
		var input = new MemberWriteAuditWriter.MemberWriteAuditInput(
			SystemId: Guid.NewGuid(),
			AccountId: Guid.NewGuid(),
			SystemMembershipId: Guid.NewGuid(),
			MemberId: Guid.NewGuid(),
			Operation: MemberWriteAuditWriter.MemberAddOperation,
			RequestTraceId: "trace-member-add");

		Assert.NotEqual(Guid.Empty, input.SystemId);
		Assert.NotEqual(Guid.Empty, input.AccountId);
		Assert.NotNull(input.SystemMembershipId);
		Assert.NotEqual(Guid.Empty, input.MemberId);
		Assert.Equal("member.add", input.Operation);
		Assert.Equal("trace-member-add", input.RequestTraceId);
	}

	[Fact]
	public void MemberEditAuditInput_AllowsValidInputShape()
	{
		var input = new MemberWriteAuditWriter.MemberWriteAuditInput(
			SystemId: Guid.NewGuid(),
			AccountId: Guid.NewGuid(),
			SystemMembershipId: Guid.NewGuid(),
			MemberId: Guid.NewGuid(),
			Operation: MemberWriteAuditWriter.MemberEditOperation,
			RequestTraceId: "trace-member-edit");

		Assert.NotEqual(Guid.Empty, input.SystemId);
		Assert.NotEqual(Guid.Empty, input.AccountId);
		Assert.NotNull(input.SystemMembershipId);
		Assert.NotEqual(Guid.Empty, input.MemberId);
		Assert.Equal("member.edit", input.Operation);
		Assert.Equal("trace-member-edit", input.RequestTraceId);
	}

	[Fact]
	public async Task WriteAsync_RejectsMissingSystemId()
	{
		await using var connection = new SqlConnection();

		var input = new MemberWriteAuditWriter.MemberWriteAuditInput(
			SystemId: Guid.Empty,
			AccountId: Guid.NewGuid(),
			SystemMembershipId: Guid.NewGuid(),
			MemberId: Guid.NewGuid(),
			Operation: MemberWriteAuditWriter.MemberAddOperation,
			RequestTraceId: "trace-missing-system");

		var exception = await Assert.ThrowsAsync<ArgumentException>(
			() => MemberWriteAuditWriter.WriteAsync(connection, input));

		Assert.Contains("SystemId is required.", exception.Message);
	}

	[Fact]
	public async Task WriteAsync_RejectsMissingAccountId()
	{
		await using var connection = new SqlConnection();

		var input = new MemberWriteAuditWriter.MemberWriteAuditInput(
			SystemId: Guid.NewGuid(),
			AccountId: Guid.Empty,
			SystemMembershipId: Guid.NewGuid(),
			MemberId: Guid.NewGuid(),
			Operation: MemberWriteAuditWriter.MemberAddOperation,
			RequestTraceId: "trace-missing-account");

		var exception = await Assert.ThrowsAsync<ArgumentException>(
			() => MemberWriteAuditWriter.WriteAsync(connection, input));

		Assert.Contains("AccountId is required.", exception.Message);
	}

	[Fact]
	public async Task WriteAsync_RejectsMissingMemberId()
	{
		await using var connection = new SqlConnection();

		var input = new MemberWriteAuditWriter.MemberWriteAuditInput(
			SystemId: Guid.NewGuid(),
			AccountId: Guid.NewGuid(),
			SystemMembershipId: Guid.NewGuid(),
			MemberId: Guid.Empty,
			Operation: MemberWriteAuditWriter.MemberEditOperation,
			RequestTraceId: "trace-missing-member");

		var exception = await Assert.ThrowsAsync<ArgumentException>(
			() => MemberWriteAuditWriter.WriteAsync(connection, input));

		Assert.Contains("MemberId is required.", exception.Message);
	}

	[Fact]
	public async Task WriteAsync_RejectsUnsupportedOperation()
	{
		await using var connection = new SqlConnection();

		var input = new MemberWriteAuditWriter.MemberWriteAuditInput(
			SystemId: Guid.NewGuid(),
			AccountId: Guid.NewGuid(),
			SystemMembershipId: Guid.NewGuid(),
			MemberId: Guid.NewGuid(),
			Operation: "member.delete",
			RequestTraceId: "trace-unsupported-operation");

		var exception = await Assert.ThrowsAsync<ArgumentException>(
			() => MemberWriteAuditWriter.WriteAsync(connection, input));

		Assert.Contains("Operation must be member.add or member.edit.", exception.Message);
	}

	[Fact]
	public async Task WriteAsync_RejectsLongRequestTraceId()
	{
		await using var connection = new SqlConnection();

		var input = new MemberWriteAuditWriter.MemberWriteAuditInput(
			SystemId: Guid.NewGuid(),
			AccountId: Guid.NewGuid(),
			SystemMembershipId: Guid.NewGuid(),
			MemberId: Guid.NewGuid(),
			Operation: MemberWriteAuditWriter.MemberAddOperation,
			RequestTraceId: new string('x', 101));

		var exception = await Assert.ThrowsAsync<ArgumentException>(
			() => MemberWriteAuditWriter.WriteAsync(connection, input));

		Assert.Contains("RequestTraceId cannot exceed 100 characters.", exception.Message);
	}
}