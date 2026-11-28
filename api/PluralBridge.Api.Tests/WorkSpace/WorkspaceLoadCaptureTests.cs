// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Microsoft.Extensions.Logging;
using PluralBridge.Api.Controllers;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// Opening-command and profile-read diagnostics stay inside an enabled workspace-load profile.
/// These tests do not open a database connection.
/// </summary>
public sealed class WorkspaceLoadCaptureTests
{
	/// <summary>The same four arguments share a fingerprint. A different email does not. The source text is not stored.</summary>
	[Fact]
	public void Fingerprint_MatchesArgumentsWithoutKeepingThem()
	{
		const string email = "owner@example.test";
		const string anchor = "Harbor";
		const string branch = "[{\"objectType\":\"system\"}]";
		var member = new AnchorProfileReadCapture(AnchorProfileReadCapture.MemberCaller);
		member.Bind(email, "system", anchor, branch);
		var again = new AnchorProfileReadCapture(AnchorProfileReadCapture.MemberCaller);
		again.Bind(email, "system", anchor, branch);
		var changed = new AnchorProfileReadCapture(AnchorProfileReadCapture.MemberCaller);
		changed.Bind("other@example.test", "system", anchor, branch);
		var unexpected = new AnchorProfileReadCapture("member");
		unexpected.Bind(email, "person", anchor, branch);

		Assert.Equal(member.ArgumentFingerprint, again.ArgumentFingerprint);
		Assert.NotEqual(member.ArgumentFingerprint, changed.ArgumentFingerprint);
		Assert.Equal(64, member.ArgumentFingerprint.Length);
		Assert.Equal(email.Length, member.EmailLength);
		Assert.Equal(anchor.Length, member.AnchorNameLength);
		Assert.Equal(branch.Length, member.BranchJsonLength);
		Assert.Equal("system", member.ObjectType);
		Assert.Equal("other", unexpected.ObjectType);
		Assert.Equal("member", member.Caller);
	}

	/// <summary>A tree profile records command times beside the existing opening-build total.</summary>
	[Fact]
	public void TreeHeader_RecordsOpeningCommandsWithoutReplacingTheBuildTotal()
	{
		var profile = WorkspaceTreeLoadProfile.TryStart(Guid.NewGuid().ToString("D"));
		Assert.NotNull(profile);
		profile.SetOpeningBuild(1690);
		profile.AddOpeningCommand("tempTables", 12);
		profile.AddOpeningCommand("topologySeed", 400);
		profile.SetOutcome("completed");

		using var header = JsonDocument.Parse(profile.HeaderJson());
		Assert.Equal(1690, header.RootElement.GetProperty("serverMs").GetProperty("openingBuild").GetInt64());
		var commands = header.RootElement.GetProperty("openingCommands");
		Assert.Equal("tempTables", commands[0].GetProperty("name").GetString());
		Assert.Equal(12, commands[0].GetProperty("ms").GetInt64());
		Assert.Equal("topologySeed", commands[1].GetProperty("name").GetString());
		Assert.Equal(400, commands[1].GetProperty("ms").GetInt64());

		var logger = new CapturingLogger();
		profile.Log(logger);
		Assert.Contains("OpeningBuildMs=1690", logger.Messages[0], StringComparison.Ordinal);
		Assert.Contains("tempTables:12", logger.Messages[0], StringComparison.Ordinal);
	}

	/// <summary>An untimed tree profile does not add an opening-command list.</summary>
	[Fact]
	public void TreeHeader_OmitsCommandsWhenNoneWereTimed()
	{
		var profile = WorkspaceTreeLoadProfile.TryStart(Guid.NewGuid().ToString("D"));
		Assert.NotNull(profile);
		profile.SetOutcome("completed");
		Assert.DoesNotContain("openingCommands", profile.HeaderJson(), StringComparison.Ordinal);
	}

	/// <summary>A member profile read reports the caller and fingerprint. The email and anchor text stay out of the header and log.</summary>
	[Fact]
	public void MemberHeader_ReportsEquivalenceWithoutArgumentText()
	{
		const string email = "owner@example.test";
		const string anchor = "Harbor";
		const string branch = "[{\"name\":\"private-branch\"}]";
		var profile = WorkspaceMemberDetailProfile.ForInitialPage(Guid.NewGuid().ToString("D"), 0);
		Assert.NotNull(profile);
		var capture = new AnchorProfileReadCapture(AnchorProfileReadCapture.MemberCaller);
		capture.Bind(email, "group", anchor, branch);
		capture.ExecuteReaderAwaitMs = 20;
		capture.TotalReaderMs = 40;
		capture.AddDrain(0, 3, 5);
		capture.AddMap(0, 1);
		((IWorkspaceGatewayTiming)profile).SetProfileRead(capture);
		profile.SetProfileAuthorization(1478);
		profile.SetOutcome("completed");

		var json = profile.HeaderJson();
		using var header = JsonDocument.Parse(json);
		var read = header.RootElement.GetProperty("profileRead");
		Assert.Equal("member", read.GetProperty("caller").GetString());
		Assert.Equal(capture.ArgumentFingerprint, read.GetProperty("argumentFingerprint").GetString());
		Assert.Equal(email.Length, read.GetProperty("emailLength").GetInt32());
		Assert.Equal(20, read.GetProperty("executeReaderAwaitMs").GetInt64());
		Assert.Equal(40, read.GetProperty("totalReaderMs").GetInt64());
		Assert.Equal("profiles", read.GetProperty("resultSets")[0].GetProperty("name").GetString());
		Assert.Equal(3, read.GetProperty("resultSets")[0].GetProperty("rows").GetInt32());
		Assert.False(read.TryGetProperty("graphCopyInsert", out _));
		Assert.DoesNotContain(email, json, StringComparison.Ordinal);
		Assert.DoesNotContain(anchor, json, StringComparison.Ordinal);
		Assert.DoesNotContain("private-branch", json, StringComparison.Ordinal);

		var logger = new CapturingLogger();
		profile.Log(logger);
		Assert.Contains("ProfileCaller=member", logger.Messages[0], StringComparison.Ordinal);
		Assert.DoesNotContain(email, logger.Messages[0], StringComparison.Ordinal);
		Assert.DoesNotContain(anchor, logger.Messages[0], StringComparison.Ordinal);
	}

	/// <summary>
	/// An avatar profile keeps the graph-copy table, miss scan, and retain times.
	/// The in-procedure graph insert is named and is not given a duration.
	/// </summary>
	[Fact]
	public void AvatarHeader_SeparatesCopyTableScanAndRetainWithoutAnInsertDuration()
	{
		const string email = "owner@example.test";
		var profile = WorkspaceAvatarBatchProfile.ForInitialPage(Guid.NewGuid().ToString("D"), 0);
		Assert.NotNull(profile);
		var capture = new AnchorProfileReadCapture(AnchorProfileReadCapture.AvatarCaller);
		capture.Bind(email, "place", "Pier", "[]");
		capture.ExecuteReaderAwaitMs = 8;
		capture.TotalReaderMs = 105;
		profile.SetGraphCopyTable(4);
		profile.SetMissScan(3);
		profile.SetRetain(6);
		profile.SetProfileRead(capture);
		profile.SetProfileAuthorization(105);
		var query = new AnchorMemberProfileSave.AvatarReadTiming();
		query.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.AvatarQueryStage, 80);
		profile.RecordSharedRead(null, query);
		profile.SetImageLoop(408);
		profile.SetOutcome("completed");

		var json = profile.HeaderJson();
		using var header = JsonDocument.Parse(json);
		var server = header.RootElement.GetProperty("serverMs");
		Assert.Equal(4, server.GetProperty("graphCopyTable").GetInt64());
		Assert.Equal(3, server.GetProperty("missScan").GetInt64());
		Assert.Equal(6, server.GetProperty("retain").GetInt64());
		Assert.Equal(80, server.GetProperty("avatarQuery").GetInt64());
		Assert.Equal(408, server.GetProperty("imageLoop").GetInt64());
		var read = header.RootElement.GetProperty("profileRead");
		Assert.Equal("avatar", read.GetProperty("caller").GetString());
		Assert.Equal("includedInProcedureReader", read.GetProperty("graphCopyInsert").GetString());
		Assert.False(read.TryGetProperty("graphCopyInsertMs", out _));
		Assert.DoesNotContain(email, json, StringComparison.Ordinal);
		Assert.DoesNotContain("Pier", json, StringComparison.Ordinal);
	}
}

/// <summary>Keeps the profiling log text written by a profile.</summary>
file sealed class CapturingLogger : ILogger
{
	/// <summary>Messages written at information or above.</summary>
	public List<string> Messages { get; } = [];

	/// <inheritdoc />
	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	/// <inheritdoc />
	public bool IsEnabled(LogLevel logLevel) => true;

	/// <inheritdoc />
	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		Messages.Add(formatter(state, exception));
	}
}
