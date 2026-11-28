// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PluralBridge.Api.Controllers;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// The first avatar batch records durations only for a valid workspace-open run id.
/// These calls use the production batch service and the avatar factory. They do not open a database connection.
/// </summary>
public sealed class WorkspaceAvatarBatchProfileTests
{
	/// <summary>A retained image, a loaded image, and a null image stay distinct. The image bytes are unchanged.</summary>
	[Fact]
	public async Task HitsMissesAndNullImages_RecordOutcomesWithoutChangingBytes()
	{
		var runId = Guid.NewGuid();
		var profile = WorkspaceAvatarBatchProfile.ForInitialPage(runId.ToString("D"), 0);
		Assert.NotNull(profile);
		var harness = NewHarness(retainRed: true);
		var result = await harness.Service.LoadAsync(
			new System.Security.Claims.ClaimsPrincipal(),
			harness.Context.ContextId,
			"system-a",
			CancellationToken.None,
			0,
			profile);
		var ready = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(result);
		profile.SetResponsePreparation(0);
		profile.AddBase64(0);
		profile.SetOutcome("completed");

		Assert.Equal(new[] { "hit", "miss", "miss" }, profile.Members.Select(member => member.Retention).ToArray());
		Assert.Equal(new string?[] { "bytes", "null", "bytes" }, profile.Members.Select(member => member.Image).ToArray());
		Assert.Equal(AvatarTestDataFactory.RedMemberId, profile.Members[0].MemberId);
		Assert.Equal(AvatarTestDataFactory.MemberWithoutAvatarId, profile.Members[1].MemberId);
		Assert.Equal(AvatarTestDataFactory.BlueMemberId, profile.Members[2].MemberId);
		Assert.Null(profile.Members[0].ConnectionOpenMs);
		Assert.Null(profile.Members[1].GraphTablesMs);
		Assert.Null(profile.Members[2].AvatarQueryMs);
		Assert.Null(profile.Members[1].ByteLength);
		Assert.NotNull(profile.Members[2].ByteLength);
		Assert.NotNull(profile.ImageLoopMs);
		Assert.NotNull(profile.ActionTotalMs);
		Assert.InRange(profile.ImageLoopMs.Value, 0, long.MaxValue);
		Assert.InRange(profile.ActionTotalMs.Value, 0, long.MaxValue);
		Assert.Null(profile.ResolveConnectionOpenMs);
		Assert.Null(profile.ProfileAuthorizationMs);

		Assert.Same(AvatarTestDataFactory.RedPng, ready.Images[0].Bytes);
		Assert.Null(ready.Images[1].Bytes);
		Assert.Same(AvatarTestDataFactory.BluePng, ready.Images[2].Bytes);
		Assert.Equal([AvatarTestDataFactory.MemberWithoutAvatarId, AvatarTestDataFactory.BlueMemberId], harness.Reader.Members);

		using var header = JsonDocument.Parse(profile.HeaderJson());
		Assert.Equal(profile.RunId, header.RootElement.GetProperty("runId").GetString());
		Assert.Equal("avatars", header.RootElement.GetProperty("phase").GetString());
		Assert.Equal(3, header.RootElement.GetProperty("members").GetArrayLength());
		Assert.Equal(JsonValueKind.Null, header.RootElement.GetProperty("serverMs").GetProperty("profileConnectionOpen").ValueKind);

		var logger = new CapturingLogger();
		profile.Log(logger);
		var message = Assert.Single(logger.Messages);
		Assert.Contains("RunId=" + profile.RunId, message, StringComparison.Ordinal);
		Assert.Contains("ImageLoopMs=" + profile.ImageLoopMs.Value.ToString(), message, StringComparison.Ordinal);
		Assert.Contains("ActionTotalMs=" + profile.ActionTotalMs.Value.ToString(), message, StringComparison.Ordinal);
		Assert.Contains("Members=3", message, StringComparison.Ordinal);

		var json = JsonSerializer.Serialize(ready.Images);
		Assert.DoesNotContain(profile.RunId, json, StringComparison.Ordinal);
		Assert.DoesNotContain("serverMs", json, StringComparison.Ordinal);
	}

	/// <summary>A miss keeps the avatar-read stages the batch used to discard.</summary>
	[Fact]
	public void Miss_CopiesAvatarReadTiming()
	{
		var profile = WorkspaceAvatarBatchProfile.ForInitialPage(Guid.NewGuid().ToString("D"), 0);
		Assert.NotNull(profile);
		var timing = new AnchorMemberProfileSave.AvatarReadTiming();
		timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.GraphTablesStage, 4);
		timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.TopologySeedStage, 5);
		timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.TopologyWalkerStage, 6);
		timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.GraphExpansionStage, 7);
		timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.AvatarQueryStage, 8);
		timing.ByteLength = AvatarTestDataFactory.BluePng.Length;
		profile.BeginMiss(AvatarTestDataFactory.BlueMemberId);
		profile.RecordRead(AvatarTestDataFactory.BlueMemberId, 3, timing);
		profile.CompleteMiss(AvatarTestDataFactory.BlueMemberId, AvatarTestDataFactory.BluePng.Length);

		var member = Assert.Single(profile.Members);
		Assert.Equal("miss", member.Retention);
		Assert.Equal("bytes", member.Image);
		Assert.Equal(timing.GraphTablesMilliseconds, member.GraphTablesMs);
		Assert.Equal(timing.TopologySeedMilliseconds, member.TopologySeedMs);
		Assert.Equal(timing.TopologyWalkerMilliseconds, member.TopologyWalkerMs);
		Assert.Equal(timing.GraphExpansionMilliseconds, member.GraphExpansionMs);
		Assert.Equal(timing.AvatarQueryMilliseconds, member.AvatarQueryMs);
		Assert.Equal(timing.ByteLength, member.ByteLength);
		Assert.Equal(3, member.ConnectionOpenMs);
	}

	/// <summary>
	/// The shared miss read stores its stages once. Member lines keep byte length and do not repeat those stages.
	/// </summary>
	[Fact]
	public void SharedRead_RecordsBatchStagesOnce()
	{
		var profile = WorkspaceAvatarBatchProfile.ForInitialPage(Guid.NewGuid().ToString("D"), 0);
		Assert.NotNull(profile);
		var timing = new AnchorMemberProfileSave.AvatarReadTiming();
		timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.GraphTablesStage, 4);
		timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.TopologySeedStage, 5);
		timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.TopologyWalkerStage, 6);
		timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.GraphExpansionStage, 7);
		timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.AvatarQueryStage, 8);
		timing.ByteLength = AvatarTestDataFactory.BluePng.Length;
		profile.BeginMiss(AvatarTestDataFactory.RedMemberId);
		profile.BeginMiss(AvatarTestDataFactory.BlueMemberId);
		profile.RecordSharedRead(3, timing);
		profile.RecordSharedRead(99, timing);
		profile.CompleteMiss(AvatarTestDataFactory.RedMemberId, null);
		profile.CompleteMiss(AvatarTestDataFactory.BlueMemberId, AvatarTestDataFactory.BluePng.Length);

		Assert.Equal(3, profile.ImageConnectionOpenMs);
		Assert.Equal(4, profile.SharedGraphTablesMs);
		Assert.Equal(8, profile.SharedAvatarQueryMs);
		Assert.All(profile.Members, member => Assert.Null(member.GraphTablesMs));
		Assert.All(profile.Members, member => Assert.Null(member.ConnectionOpenMs));
		Assert.Null(profile.Members[0].ByteLength);
		Assert.Equal(AvatarTestDataFactory.BluePng.Length, profile.Members[1].ByteLength);
		Assert.Equal("null", profile.Members[0].Image);
		Assert.Equal("bytes", profile.Members[1].Image);

		profile.SetOutcome("completed");
		var header = profile.HeaderJson();
		Assert.Equal(1, header.Split("\"avatarQuery\":8").Length - 1);
		Assert.Equal(2, header.Split("\"avatarQuery\":null").Length - 1);
	}

	/// <summary>A cancelled read leaves the phase incomplete and keeps the loop time already started.</summary>
	[Fact]
	public async Task Cancellation_LeavesThePhaseIncomplete()
	{
		var profile = WorkspaceAvatarBatchProfile.ForInitialPage(Guid.NewGuid().ToString("D"), 0);
		Assert.NotNull(profile);
		var harness = NewHarness(retainRed: false, cancel: true);
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Service.LoadAsync(
			new System.Security.Claims.ClaimsPrincipal(),
			harness.Context.ContextId,
			"system-a",
			CancellationToken.None,
			0,
			profile));

		Assert.Equal("incomplete", profile.Outcome);
		Assert.Null(profile.ActionTotalMs);
		Assert.NotNull(profile.ImageLoopMs);
		Assert.Contains(profile.Members, member => member.Image == null);
	}

	/// <summary>An invalid id and a later page create no profile and write no log.</summary>
	[Fact]
	public async Task DisabledAndLaterPage_CreateNoProfile()
	{
		Assert.Null(WorkspaceAvatarBatchProfile.ForInitialPage(null, 0));
		Assert.Null(WorkspaceAvatarBatchProfile.ForInitialPage("", 0));
		Assert.Null(WorkspaceAvatarBatchProfile.ForInitialPage("true", 0));
		Assert.Null(WorkspaceAvatarBatchProfile.ForInitialPage(Guid.Empty.ToString("D"), 0));
		var laterId = Guid.NewGuid();
		Assert.Null(WorkspaceAvatarBatchProfile.ForInitialPage(laterId.ToString("D"), 20));

		var harness = NewHarness(retainRed: false);
		var result = await harness.Service.LoadAsync(
			new System.Security.Claims.ClaimsPrincipal(),
			harness.Context.ContextId,
			"system-a",
			CancellationToken.None);
		Assert.IsType<WorkspaceAvatarBatchResult.Ready>(result);

		var unused = WorkspaceAvatarBatchProfile.ForInitialPage(laterId.ToString("D"), 0);
		Assert.NotNull(unused);
		await harness.Service.LoadAsync(
			new System.Security.Claims.ClaimsPrincipal(),
			harness.Context.ContextId,
			"system-a",
			CancellationToken.None,
			20,
			unused);
		Assert.Empty(unused.Members);
		Assert.Null(unused.ImageLoopMs);
		Assert.Null(unused.ActionTotalMs);
	}

	/// <summary>A profiling log failure does not throw, so the prepared images can still be returned.</summary>
	[Fact]
	public void ProfilingFailure_DoesNotThrow()
	{
		var profile = WorkspaceAvatarBatchProfile.ForInitialPage(Guid.NewGuid().ToString("D"), 0);
		Assert.NotNull(profile);
		profile.SetOutcome("completed");
		var headers = new HeaderDictionary();
		var written = profile.TryWrite(headers, new ThrowingLogger());
		Assert.False(written);
	}

	/// <summary>
	/// A 20-member miss header stays inside one response header.
	/// The limit used here is 8 KB so the rest of the response headers still fit under the 16 KB http.sys default.
	/// </summary>
	[Fact]
	public void TwentyMemberHeader_FitsTheExistingResponseHeader()
	{
		var profile = WorkspaceAvatarBatchProfile.ForInitialPage(Guid.NewGuid().ToString("D"), 0);
		Assert.NotNull(profile);
		for (var index = 0; index < 20; index++)
		{
			var memberId = Guid.NewGuid();
			var timing = new AnchorMemberProfileSave.AvatarReadTiming();
			timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.GraphTablesStage, 999999);
			timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.TopologySeedStage, 999999);
			timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.TopologyWalkerStage, 999999);
			timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.GraphExpansionStage, 999999);
			timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.AvatarQueryStage, 999999);
			timing.ByteLength = 2_097_152;
			profile.BeginMiss(memberId);
			profile.RecordRead(memberId, 999999, timing);
			profile.CompleteMiss(memberId, timing.ByteLength);
		}

		profile.SetImageLoop(999999);
		var shared = new AnchorMemberProfileSave.AvatarReadTiming();
		shared.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.GraphTablesStage, 999999);
		shared.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.TopologySeedStage, 999999);
		shared.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.TopologyWalkerStage, 999999);
		shared.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.GraphExpansionStage, 999999);
		shared.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.AvatarQueryStage, 999999);
		profile.RecordSharedRead(999999, shared);
		profile.AddBase64(999999);
		profile.SetResponsePreparation(999999);
		profile.SetOutcome("completed");
		var bytes = Encoding.UTF8.GetByteCount(profile.HeaderJson());
		Assert.InRange(bytes, 1, 8192);
	}

	/// <summary>The browser marks the first avatar batch from the open origin and does not print a combined report.</summary>
	[Fact]
	public void Browser_TimesTheInitialBatchWithoutACombinedReport()
	{
		var navigator = File.ReadAllText(SourcePath("js", "navigator.js"));
		var client = File.ReadAllText(SourcePath("js", "api-client.js"));
		var request = FunctionBody(navigator, "function requestFirstBatchAvatars(row, request, serial)");
		var apply = FunctionBody(navigator, "function applyFirstBatchAvatars(images, profileRequest, loadingCards)");
		var settle = FunctionBody(navigator, "function settleAppliedAvatars(profileRequest, notes)");
		var mark = FunctionBody(navigator, "function markAvatarProfile(request, name)");
		var reader = FunctionBody(client, "async function readWorkspaceMemberAvatars(contextId, occurrence, signal, profileId, profileMarks)");

		var start = request.IndexOf("markAvatarProfile(request, \"avatarRequestStart\")", StringComparison.Ordinal);
		var call = request.IndexOf("readWorkspaceMemberAvatars(", StringComparison.Ordinal);
		var stale = request.IndexOf("avatarBrowser.phase = \"incomplete\"", StringComparison.Ordinal);
		var applyCall = request.IndexOf("applyFirstBatchAvatars(images, request,", StringComparison.Ordinal);
		Assert.True(start >= 0 && start < call);
		Assert.True(call < stale && stale < applyCall);
		Assert.Contains("avatarBodyReceived", request, StringComparison.Ordinal);
		Assert.Contains("avatarJsonParsed", request, StringComparison.Ordinal);
		Assert.Contains("window.performance.now() - request.treeProfile.origin", mark, StringComparison.Ordinal);
		Assert.Contains("avatarApplyComplete", apply, StringComparison.Ordinal);
		Assert.Contains("placeholders", apply, StringComparison.Ordinal);
		Assert.Contains("pendingEdits", apply, StringComparison.Ordinal);
		Assert.Contains("image.decode", settle, StringComparison.Ordinal);
		Assert.Contains("addEventListener(\"load\"", settle, StringComparison.Ordinal);
		Assert.Contains("addEventListener(\"error\"", settle, StringComparison.Ordinal);
		Assert.Contains("It is not screen paint.", settle, StringComparison.Ordinal);
		Assert.DoesNotContain("console.info", mark, StringComparison.Ordinal);
		Assert.DoesNotContain("console.info", settle, StringComparison.Ordinal);
		Assert.Contains("profileMarks && profileMarks.jsonParsed", reader, StringComparison.Ordinal);
	}

	/// <summary>Publishes one page and a factory reader.</summary>
	/// <param name="retainRed">True when the red image is already retained.</param>
	/// <param name="cancel">True when the next image read is cancelled.</param>
	/// <returns>The production service, the context, and the reader.</returns>
	private static Harness NewHarness(bool retainRed, bool cancel = false)
	{
		var systemId = Guid.Parse("11111111-1111-4111-8111-111111111111");
		var memberIds = new[]
		{
			AvatarTestDataFactory.RedMemberId,
			AvatarTestDataFactory.MemberWithoutAvatarId,
			AvatarTestDataFactory.BlueMemberId
		};
		var rows = new List<PluralMapPreviewRow>
		{
			new("preview", "system-a", "system-a", "Harbor", "system", systemId, null, false, "Harbor")
		};
		for (var index = 0; index < memberIds.Length; index++)
		{
			rows.Add(new PluralMapPreviewRow(
				"preview",
				"member-" + index.ToString(),
				"member-" + index.ToString(),
				"Member",
				"member",
				memberIds[index],
				"system-a",
				false,
				null));
		}

		var context = WorkspaceContext.Create(
			AvatarTestDataFactory.AccountId,
			[],
			[new WorkspaceOccurrence("account", AvatarTestDataFactory.AccountId, "system", systemId, 0, "system-a")],
			[],
			[],
			rows,
			[new WorkspaceAccessFact(Guid.NewGuid(), "system", systemId, 1, "Active", true)]);
		if (retainRed)
		{
			var loaded = AnchorMemberProfileSave.LoadAvatarAsync(
				useDatabase: false,
				AvatarTestDataFactory.AccountId,
				AvatarTestDataFactory.RedMemberId,
				CancellationToken.None,
				testDataFactory: AvatarTestDataFactory.Read).GetAwaiter().GetResult();
			Assert.NotNull(loaded);
			context.RetainAvatar(AvatarTestDataFactory.RedMemberId, loaded.Value);
		}

		var store = new WorkspaceContextStore(
			new ManualWorkspaceClock(),
			new MutableWorkspaceContextSettings(),
			new SilentLogger());
		store.Publish(context);
		var reader = new FactoryAvatarReader { Cancel = cancel };
		var gateway = new AuthorizingGateway(memberIds);
		return new Harness(new WorkspaceAvatarBatchService(store, gateway, reader), context, reader);
	}

	/// <summary>Reads one function, starting at its declaration, through the matching closing brace.</summary>
	/// <param name="script">The source file.</param>
	/// <param name="signature">The declaration that starts the function.</param>
	/// <returns>The declaration and its body.</returns>
	private static string FunctionBody(string script, string signature)
	{
		var start = script.IndexOf(signature, StringComparison.Ordinal);
		Assert.True(start >= 0, signature);
		var open = script.IndexOf('{', start);
		var depth = 0;
		for (var index = open; index < script.Length; index++)
		{
			if (script[index] == '{')
			{
				depth++;
			}
			else if (script[index] == '}')
			{
				depth--;
				if (depth == 0)
				{
					return script[start..(index + 1)];
				}
			}
		}

		throw new InvalidOperationException("The function body was not closed.");
	}

	/// <summary>Finds a browser source file from this test file.</summary>
	/// <param name="folder">The source folder under app/src.</param>
	/// <param name="fileName">The file name.</param>
	/// <param name="sourceFile">The test file path supplied by the compiler.</param>
	/// <returns>The absolute source path.</returns>
	private static string SourcePath(string folder, string fileName, [CallerFilePath] string sourceFile = "")
	{
		return Path.GetFullPath(Path.Combine(
			Path.GetDirectoryName(sourceFile) ?? string.Empty,
			"..",
			"..",
			"app",
			"src",
			folder,
			fileName));
	}

	/// <param name="Service">The production avatar batch service.</param>
	/// <param name="Context">The published context.</param>
	/// <param name="Reader">The factory reader.</param>
	private sealed record Harness(WorkspaceAvatarBatchService Service, WorkspaceContext Context, FactoryAvatarReader Reader);

	/// <summary>Authorizes every member on the page. It does not open a connection.</summary>
	private sealed class AuthorizingGateway(Guid[] memberIds) : IWorkspaceProfileGateway
	{
		/// <inheritdoc />
		public Task<AccessContextHelper.Account?> ResolveAsync(System.Security.Claims.ClaimsPrincipal user, CancellationToken cancellationToken)
		{
			return Task.FromResult<AccessContextHelper.Account?>(new AccessContextHelper.Account(
				AvatarTestDataFactory.AccountId,
				"owner@example.com",
				"Owner",
				1,
				new AccessContextHelper.AccountStatus(1, "Active", "Active", 1, true),
				DateTime.UtcNow,
				null));
		}

		/// <inheritdoc />
		public Task<AnchorProfileBundle> ReadProfilesAsync(
			string email,
			string objectType,
			string anchorName,
			string branch,
			CancellationToken cancellationToken)
		{
			var profiles = memberIds
				.Select(id => new AnchorMemberProfile(id, "Member", null, null, null, null, null, null, false))
				.ToArray();
			return Task.FromResult(new AnchorProfileBundle(profiles, true, [], [], [], []));
		}
	}

	/// <summary>Loads images through the avatar factory.</summary>
	private sealed class FactoryAvatarReader : IWorkspaceAvatarReader
	{
		/// <summary>Members the batch asked the loader to read.</summary>
		internal List<Guid> Members { get; } = [];

		/// <summary>True when the next read is cancelled.</summary>
		internal bool Cancel { get; set; }

		/// <inheritdoc />
		public Task<(byte[] Bytes, string ContentType)?> ReadAsync(
			Guid accountId,
			Guid memberId,
			CancellationToken cancellationToken)
		{
			if (Cancel)
			{
				throw new OperationCanceledException();
			}

			Members.Add(memberId);
			return AnchorMemberProfileSave.LoadAvatarAsync(
				useDatabase: false,
				accountId,
				memberId,
				cancellationToken,
				testDataFactory: AvatarTestDataFactory.Read);
		}
	}

	/// <summary>Discards store messages. The profile test reads its own logger.</summary>
	private sealed class SilentLogger : ILogger<WorkspaceContextStore>
	{
		/// <inheritdoc />
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull
		{
			return null;
		}

		/// <inheritdoc />
		public bool IsEnabled(LogLevel logLevel)
		{
			return false;
		}

		/// <inheritdoc />
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
		}
	}

	/// <summary>Collects formatted log lines.</summary>
	private sealed class CapturingLogger : ILogger
	{
		/// <summary>The formatted messages.</summary>
		internal List<string> Messages { get; } = [];

		/// <inheritdoc />
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull
		{
			return null;
		}

		/// <inheritdoc />
		public bool IsEnabled(LogLevel logLevel)
		{
			return true;
		}

		/// <inheritdoc />
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			Messages.Add(formatter(state, exception));
		}
	}

	/// <summary>A logger whose write fails. The profile writer must absorb that failure.</summary>
	private sealed class ThrowingLogger : ILogger
	{
		/// <inheritdoc />
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull
		{
			return null;
		}

		/// <inheritdoc />
		public bool IsEnabled(LogLevel logLevel)
		{
			return true;
		}

		/// <inheritdoc />
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			throw new InvalidOperationException("The profiling log failed.");
		}
	}
}
