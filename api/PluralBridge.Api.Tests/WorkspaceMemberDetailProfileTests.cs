// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PluralBridge.Api.Controllers;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// The first member-detail page records durations only for a valid workspace-open run id.
/// These calls use the production loader and a fake reader. They do not open a database connection.
/// </summary>
public sealed class WorkspaceMemberDetailProfileTests
{
	/// <summary>A valid first-page id correlates the header and the log. The member JSON stays free of those durations.</summary>
	[Fact]
	public async Task EnabledFirstPage_CorrelatesDurationsOutsideTheMemberJson()
	{
		var runId = Guid.NewGuid();
		var profile = WorkspaceMemberDetailProfile.ForInitialPage(runId.ToString("D"), 0);
		Assert.NotNull(profile);
		var harness = NewHarness();
		var result = await harness.Service.LoadAsync(
			new System.Security.Claims.ClaimsPrincipal(),
			harness.Context.ContextId,
			"system-a",
			CancellationToken.None,
			0,
			profile);
		var ready = Assert.IsType<WorkspaceMemberDetailResult.Ready>(result);
		profile.SetOutcome("completed");

		Assert.Equal(runId.ToString("D"), profile.RunId);
		Assert.Equal("completed", profile.Outcome);
		Assert.Null(profile.ResolveConnectionOpenMs);
		Assert.Null(profile.AccountResolutionMs);
		Assert.Null(profile.ProfileConnectionOpenMs);
		Assert.Null(profile.ProfileAuthorizationMs);
		Assert.NotNull(profile.PrepareOccurrenceMs);
		Assert.NotNull(profile.ResponsePreparationMs);
		Assert.NotNull(profile.ActionTotalMs);
		Assert.InRange(profile.PrepareOccurrenceMs.Value, 0, long.MaxValue);
		Assert.InRange(profile.ResponsePreparationMs.Value, 0, long.MaxValue);
		Assert.InRange(profile.ActionTotalMs.Value, 0, long.MaxValue);

		using var header = JsonDocument.Parse(profile.HeaderJson());
		Assert.Equal("members", header.RootElement.GetProperty("phase").GetString());
		Assert.Equal(profile.RunId, header.RootElement.GetProperty("runId").GetString());
		Assert.Equal(profile.PrepareOccurrenceMs, header.RootElement.GetProperty("serverMs").GetProperty("prepareOccurrence").GetInt64());
		Assert.Equal(profile.ResponsePreparationMs, header.RootElement.GetProperty("serverMs").GetProperty("responsePreparation").GetInt64());
		Assert.Equal(JsonValueKind.Null, header.RootElement.GetProperty("serverMs").GetProperty("profileConnectionOpen").ValueKind);

		var logger = new CapturingLogger();
		profile.Log(logger);
		var message = Assert.Single(logger.Messages);
		Assert.Contains("RunId=" + profile.RunId, message, StringComparison.Ordinal);
		Assert.Contains("PrepareOccurrenceMs=" + profile.PrepareOccurrenceMs.Value.ToString(), message, StringComparison.Ordinal);
		Assert.Contains("ResponsePreparationMs=" + profile.ResponsePreparationMs.Value.ToString(), message, StringComparison.Ordinal);
		Assert.Contains("ActionTotalMs=" + profile.ActionTotalMs.Value.ToString(), message, StringComparison.Ordinal);

		var json = JsonSerializer.Serialize(ready.Cards);
		Assert.DoesNotContain(profile.RunId, json, StringComparison.Ordinal);
		Assert.DoesNotContain("serverMs", json, StringComparison.Ordinal);
		Assert.DoesNotContain("actionTotal", json, StringComparison.Ordinal);
	}

	/// <summary>An invalid id does not start a profile, and a later page does not record durations.</summary>
	[Fact]
	public async Task DisabledAndLaterPage_CreateNoProfile()
	{
		Assert.Null(WorkspaceMemberDetailProfile.ForInitialPage(null, 0));
		Assert.Null(WorkspaceMemberDetailProfile.ForInitialPage("", 0));
		Assert.Null(WorkspaceMemberDetailProfile.ForInitialPage("true", 0));
		Assert.Null(WorkspaceMemberDetailProfile.ForInitialPage("1", 0));
		Assert.Null(WorkspaceMemberDetailProfile.ForInitialPage(Guid.Empty.ToString("D"), 0));
		var laterId = Guid.NewGuid();
		Assert.Null(WorkspaceMemberDetailProfile.ForInitialPage(laterId.ToString("D"), 20));

		var harness = NewHarness();
		var unprofiled = await harness.Service.LoadAsync(
			new System.Security.Claims.ClaimsPrincipal(),
			harness.Context.ContextId,
			"system-a",
			CancellationToken.None);
		Assert.IsType<WorkspaceMemberDetailResult.Ready>(unprofiled);
		Assert.DoesNotContain(harness.Messages, message => message.Contains("WorkspaceMemberDetailProfile", StringComparison.Ordinal));

		var ignored = WorkspaceMemberDetailProfile.ForInitialPage(laterId.ToString("D"), 0);
		Assert.NotNull(ignored);
		var page = await harness.Service.LoadAsync(
			new System.Security.Claims.ClaimsPrincipal(),
			harness.Context.ContextId,
			"system-a",
			CancellationToken.None,
			20,
			ignored);
		Assert.IsType<WorkspaceMemberDetailResult.Ready>(page);
		Assert.Null(ignored.PrepareOccurrenceMs);
		Assert.Null(ignored.ResponsePreparationMs);
		Assert.Null(ignored.ActionTotalMs);
	}

	/// <summary>A cancelled or failed first page stays incomplete and keeps its own run id.</summary>
	[Fact]
	public async Task CancelledAndFailedReads_StayIncompleteAndCorrelated()
	{
		var cancelledId = Guid.NewGuid();
		var failedId = Guid.NewGuid();
		var cancelledHarness = NewHarness();
		cancelledHarness.Gateway.Error = new OperationCanceledException();
		var cancelled = WorkspaceMemberDetailProfile.ForInitialPage(cancelledId.ToString("D"), 0);
		Assert.NotNull(cancelled);
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledHarness.Service.LoadAsync(
			new System.Security.Claims.ClaimsPrincipal(),
			cancelledHarness.Context.ContextId,
			"system-a",
			CancellationToken.None,
			0,
			cancelled));
		cancelled.SetOutcome("cancelled");
		Assert.Equal("cancelled", cancelled.Outcome);
		Assert.NotNull(cancelled.PrepareOccurrenceMs);
		Assert.Null(cancelled.ResponsePreparationMs);
		Assert.NotNull(cancelled.ActionTotalMs);

		var failedHarness = NewHarness();
		failedHarness.Gateway.Error = new InvalidOperationException("The profile read failed.");
		var failed = WorkspaceMemberDetailProfile.ForInitialPage(failedId.ToString("D"), 0);
		Assert.NotNull(failed);
		await Assert.ThrowsAsync<InvalidOperationException>(() => failedHarness.Service.LoadAsync(
			new System.Security.Claims.ClaimsPrincipal(),
			failedHarness.Context.ContextId,
			"system-a",
			CancellationToken.None,
			0,
			failed));
		failed.SetOutcome("failed");
		Assert.Equal("failed", failed.Outcome);
		Assert.Null(failed.ResponsePreparationMs);
		Assert.NotEqual(cancelled.RunId, failed.RunId);

		var logger = new CapturingLogger();
		cancelled.Log(logger);
		failed.Log(logger);
		Assert.Contains(logger.Messages, message => message.Contains("RunId=" + cancelled.RunId, StringComparison.Ordinal) && message.Contains("Outcome=cancelled", StringComparison.Ordinal));
		Assert.Contains(logger.Messages, message => message.Contains("RunId=" + failed.RunId, StringComparison.Ordinal) && message.Contains("Outcome=failed", StringComparison.Ordinal));
		Assert.DoesNotContain(logger.Messages, message => message.Contains(cancelled.RunId, StringComparison.Ordinal) && message.Contains(failed.RunId, StringComparison.Ordinal));
	}

	/// <summary>The browser marks the first member page from the workspace-open origin and does not mark a later page.</summary>
	[Fact]
	public void Navigator_MeasuresTheFirstMemberPageFromTheOpenOrigin()
	{
		var navigator = File.ReadAllText(SourcePath("js", "navigator.js"));
		var client = File.ReadAllText(SourcePath("js", "api-client.js"));
		var detail = FunctionBody(navigator, "function startMemberDetailRequest(row, skip, request, signal, progress)");
		var fetch = FunctionBody(client, "async function fetchJson(endpoint, options)");
		var reader = FunctionBody(client, "async function readWorkspaceMemberDetails(contextId, occurrence, signal, skip, profileId, profileMarks)");
		var mark = FunctionBody(navigator, "function markMemberProfile(request, name)");
		var body = fetch.IndexOf("requestOptions.onBody", StringComparison.Ordinal);
		var parse = fetch.IndexOf("JSON.parse(responseText)", StringComparison.Ordinal);
		var parsed = fetch.IndexOf("requestOptions.onParsed", StringComparison.Ordinal);
		var start = detail.IndexOf("markMemberProfile(request, \"memberRequestStart\")", StringComparison.Ordinal);
		var call = detail.IndexOf("readWorkspaceMemberDetails(", StringComparison.Ordinal);
		var paint = detail.IndexOf("paintWorkspaceMemberPage(row, profiles, skip);", StringComparison.Ordinal);
		var cards = detail.IndexOf("markMemberProfile(request, \"memberCardsDom\")", StringComparison.Ordinal);

		Assert.True(body >= 0 && body < parse && parse < parsed);
		Assert.True(start >= 0 && start < call);
		Assert.True(paint >= 0 && paint < cards);
		Assert.Contains("skip === 0 && request.treeProfile", detail, StringComparison.Ordinal);
		Assert.Contains("memberSkip === 0 ? profileMarks : null", reader, StringComparison.Ordinal);
		Assert.Contains("window.performance.now() - request.treeProfile.origin", mark, StringComparison.Ordinal);
		Assert.Contains("It does not mean the screen has painted.", navigator, StringComparison.Ordinal);
		Assert.DoesNotContain("console.info", mark, StringComparison.Ordinal);
		Assert.Contains("loadWorkspaceMemberDetails(row, profileId, profileRequest);", navigator, StringComparison.Ordinal);
	}

	/// <summary>Publishes one anchor whose fake reader can return its direct member.</summary>
	/// <returns>The service, context, gateway, and store log.</returns>
	private static Harness NewHarness()
	{
		var accountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
		var memberId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
		var systemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var logger = new CapturingLogger();
		var store = new WorkspaceContextStore(new ManualWorkspaceClock(), new MutableWorkspaceContextSettings(), logger);
		var context = WorkspaceContext.Create(
			accountId,
			[],
			[new WorkspaceOccurrence("account", accountId, "system", systemId, 0, "system-a")],
			[],
			[],
			[
				new PluralMapPreviewRow("preview", "system-a", "system-a", "Harbor", "system", systemId, null, false, "Harbor"),
				new PluralMapPreviewRow("preview", "member-a", "member-a", "Member: Jane", "member", memberId, "system-a", false, null)
			],
			[new WorkspaceAccessFact(Guid.NewGuid(), "system", systemId, 1, "Active", true)]);
		store.Publish(context);
		var gateway = new FakeGateway(accountId, memberId);
		return new Harness(new WorkspaceMemberDetailService(store, gateway), context, gateway, logger.Messages);
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

	/// <param name="Service">The production member-detail service.</param>
	/// <param name="Context">The published context.</param>
	/// <param name="Gateway">The fake reader.</param>
	/// <param name="Messages">Messages from the store logger.</param>
	private sealed record Harness(
		WorkspaceMemberDetailService Service,
		WorkspaceContext Context,
		FakeGateway Gateway,
		List<string> Messages);

	/// <summary>Returns one account and one profile bundle. It does not open a connection.</summary>
	private sealed class FakeGateway(Guid accountId, Guid memberId) : IWorkspaceProfileGateway
	{
		/// <summary>Thrown by the profile read when set.</summary>
		internal Exception? Error { get; set; }

		/// <inheritdoc />
		public Task<AccessContextHelper.Account?> ResolveAsync(System.Security.Claims.ClaimsPrincipal user, CancellationToken cancellationToken)
		{
			return Task.FromResult<AccessContextHelper.Account?>(new AccessContextHelper.Account(
				accountId,
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
			if (Error != null)
			{
				return Task.FromException<AnchorProfileBundle>(Error);
			}

			return Task.FromResult(new AnchorProfileBundle(
				[new AnchorMemberProfile(memberId, "Member", null, null, null, null, null, null, false)],
				true,
				[],
				[new AnchorMemberPlacement(memberId, "explicit", 1, "explicit", "viewer")],
				[],
				[]));
		}
	}

	/// <summary>Collects formatted log lines.</summary>
	private sealed class CapturingLogger : ILogger<WorkspaceContextStore>
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
}
