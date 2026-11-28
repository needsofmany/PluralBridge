// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// Workspace-tree profiling records durations only for a valid run id.
/// These calls use the production loader and the existing factory. They do not open a database connection.
/// </summary>
public sealed class WorkspaceTreeLoadProfileTests
{
	/// <summary>A valid id correlates the header and the log. The tree JSON stays free of those durations.</summary>
	[Fact]
	public async Task EnabledFactoryLoad_CorrelatesDurationsOutsideTheTreeJson()
	{
		var runId = Guid.NewGuid();
		var profile = WorkspaceTreeLoadProfile.TryStart(runId.ToString("D"));
		Assert.NotNull(profile);
		var store = NewStore();
		var service = new WorkspaceTreeService(new IdleSession(), store);
		var response = await service.LoadTrackedAsync(false, new System.Security.Claims.ClaimsPrincipal(), CancellationToken.None, profile, null);
		Assert.NotNull(response);
		profile.SetOutcome("completed");

		Assert.Equal(runId.ToString("D"), profile.RunId);
		Assert.Equal("completed", profile.Outcome);
		Assert.Null(profile.ConnectionOpenMs);
		Assert.Null(profile.AccountResolutionMs);
		Assert.Null(profile.OpeningBuildMs);
		Assert.NotNull(profile.CreateContextMs);
		Assert.NotNull(profile.PublishMs);
		Assert.NotNull(profile.ActionTotalMs);
		Assert.InRange(profile.CreateContextMs.Value, 0, long.MaxValue);
		Assert.InRange(profile.PublishMs.Value, 0, long.MaxValue);
		Assert.InRange(profile.ActionTotalMs.Value, 0, long.MaxValue);

		using var header = JsonDocument.Parse(profile.HeaderJson());
		Assert.Equal(profile.RunId, header.RootElement.GetProperty("runId").GetString());
		Assert.Equal(profile.CreateContextMs, header.RootElement.GetProperty("serverMs").GetProperty("createContext").GetInt64());
		Assert.Equal(profile.PublishMs, header.RootElement.GetProperty("serverMs").GetProperty("publish").GetInt64());
		Assert.Equal(JsonValueKind.Null, header.RootElement.GetProperty("serverMs").GetProperty("connectionOpen").ValueKind);

		var logger = new CapturingLogger();
		profile.Log(logger);
		var message = Assert.Single(logger.Messages);
		Assert.Contains("RunId=" + profile.RunId, message, StringComparison.Ordinal);
		Assert.Contains("CreateContextMs=" + profile.CreateContextMs.Value.ToString(), message, StringComparison.Ordinal);
		Assert.Contains("PublishMs=" + profile.PublishMs.Value.ToString(), message, StringComparison.Ordinal);
		Assert.Contains("ActionTotalMs=" + profile.ActionTotalMs.Value.ToString(), message, StringComparison.Ordinal);

		var json = JsonSerializer.Serialize(response);
		Assert.DoesNotContain(profile.RunId, json, StringComparison.Ordinal);
		Assert.DoesNotContain("serverMs", json, StringComparison.Ordinal);
		Assert.DoesNotContain("actionTotal", json, StringComparison.Ordinal);
	}

	/// <summary>Missing and non-guid ids do not start a profile, and a factory load then writes no profiling log.</summary>
	[Fact]
	public async Task Disabled_CreatesNoProfileOrProfilingLog()
	{
		Assert.Null(WorkspaceTreeLoadProfile.TryStart(null));
		Assert.Null(WorkspaceTreeLoadProfile.TryStart(""));
		Assert.Null(WorkspaceTreeLoadProfile.TryStart("true"));
		Assert.Null(WorkspaceTreeLoadProfile.TryStart("1"));
		Assert.Null(WorkspaceTreeLoadProfile.TryStart(Guid.Empty.ToString("D")));

		var storeLogger = new CapturingLogger();
		var service = new WorkspaceTreeService(new IdleSession(), NewStore(storeLogger));
		var response = await service.LoadTrackedAsync(false, new System.Security.Claims.ClaimsPrincipal(), CancellationToken.None, null, null);
		Assert.NotNull(response);
		Assert.DoesNotContain(storeLogger.Messages, message => message.Contains("WorkspaceTreeProfile", StringComparison.Ordinal));
	}

	/// <summary>Cancellation and a failed factory leave the load incomplete and keep their own run id.</summary>
	[Fact]
	public async Task CancelledAndFailedLoads_StayIncompleteAndCorrelated()
	{
		var cancelledId = Guid.NewGuid();
		var failedId = Guid.NewGuid();
		var store = NewStore();
		var service = new WorkspaceTreeService(new IdleSession(), store);
		var cancelled = WorkspaceTreeLoadProfile.TryStart(cancelledId.ToString("D"));
		Assert.NotNull(cancelled);
		using var source = new CancellationTokenSource();
		source.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.LoadTrackedAsync(
			false,
			new System.Security.Claims.ClaimsPrincipal(),
			source.Token,
			cancelled,
			null));
		cancelled.SetOutcome("cancelled");
		Assert.Equal("cancelled", cancelled.Outcome);
		Assert.Null(cancelled.CreateContextMs);
		Assert.Null(cancelled.PublishMs);
		Assert.NotNull(cancelled.ActionTotalMs);
		Assert.Equal(0, store.Count);

		var failed = WorkspaceTreeLoadProfile.TryStart(failedId.ToString("D"));
		Assert.NotNull(failed);
		await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoadTrackedAsync(
			false,
			new System.Security.Claims.ClaimsPrincipal(),
			CancellationToken.None,
			failed,
			() => throw new InvalidOperationException("The factory failed.")));
		failed.SetOutcome("failed");
		Assert.Equal("failed", failed.Outcome);
		Assert.Null(failed.CreateContextMs);
		Assert.Null(failed.PublishMs);
		Assert.NotEqual(cancelled.RunId, failed.RunId);

		var logger = new CapturingLogger();
		cancelled.Log(logger);
		failed.Log(logger);
		Assert.Contains(logger.Messages, message => message.Contains("RunId=" + cancelled.RunId, StringComparison.Ordinal) && message.Contains("Outcome=cancelled", StringComparison.Ordinal));
		Assert.Contains(logger.Messages, message => message.Contains("RunId=" + failed.RunId, StringComparison.Ordinal) && message.Contains("Outcome=failed", StringComparison.Ordinal));
		Assert.DoesNotContain(logger.Messages, message => message.Contains(cancelled.RunId, StringComparison.Ordinal) && message.Contains(failed.RunId, StringComparison.Ordinal));
	}

	/// <summary>The browser records tree marks on this run's clock and copies server durations from the response header.</summary>
	[Fact]
	public void Navigator_MeasuresTreeDomCompletionOnTheRunClock()
	{
		var script = File.ReadAllText(SourcePath("js", "navigator.js"));
		var load = FunctionBody(script, "function startLoad(replacing)");
		var select = FunctionBody(script, "function selectAnchor(row, profileId, profileRequest)");
		var mark = FunctionBody(script, "function markTreeProfile(request, name)");
		var finish = FunctionBody(script, "function finishTreeProfile(request, outcome, openContinues)");
		var origin = load.IndexOf("origin: window.performance.now()", StringComparison.Ordinal);
		var fetch = load.IndexOf("fetch(request.workspaceMode ? workspaceTreeEndpoint : pluralMapEndpoint", StringComparison.Ordinal);
		var received = load.IndexOf("markTreeProfile(request, \"responseReceived\")", StringComparison.Ordinal);
		var parsed = load.IndexOf("markTreeProfile(request, \"jsonParsed\")", StringComparison.Ordinal);
		var body = load.IndexOf("return response.json()", StringComparison.Ordinal);
		var rendered = load.IndexOf("renderTree();", StringComparison.Ordinal);
		var dom = load.IndexOf("markTreeProfile(request, \"domComplete\")", StringComparison.Ordinal);
		var stale = load.IndexOf("if (!loadIsCurrent(request))", StringComparison.Ordinal);

		Assert.True(origin >= 0 && origin < fetch);
		Assert.True(received >= 0 && received < body);
		Assert.True(parsed >= 0 && parsed < rendered);
		Assert.True(rendered < dom);
		Assert.True(stale >= 0 && stale < rendered);
		Assert.Contains("finishTreeProfile(request, \"incomplete\")", load, StringComparison.Ordinal);
		Assert.Contains("markTreeProfile(profileRequest, \"selectionDom\")", select, StringComparison.Ordinal);
		Assert.Contains("renderTree();", select, StringComparison.Ordinal);
		Assert.Contains("window.performance.now() - request.treeProfile.origin", mark, StringComparison.Ordinal);
		Assert.Contains("They do not mean the screen has painted.", script, StringComparison.Ordinal);
		Assert.Contains("publishWorkspaceLoadReport(request)", finish, StringComparison.Ordinal);
		Assert.Contains("assembleWorkspaceLoadReport(request.treeProfile)", script, StringComparison.Ordinal);
		Assert.DoesNotContain("phase: \"tree\"", finish, StringComparison.Ordinal);
		Assert.Contains("selectAnchor(row);", script, StringComparison.Ordinal);
	}

	/// <summary>Creates a store whose own log is separate from the profiling log.</summary>
	/// <param name="logger">The store logger. Null uses a silent logger.</param>
	/// <returns>The store.</returns>
	private static WorkspaceContextStore NewStore(CapturingLogger? logger = null)
	{
		return new WorkspaceContextStore(
			new ManualWorkspaceClock(),
			new MutableWorkspaceContextSettings(),
			logger ?? new CapturingLogger());
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
			"..",
			"app",
			"src",
			folder,
			fileName));
	}

	/// <summary>An opening session that fails the test if the factory path calls it.</summary>
	private sealed class IdleSession : IWorkspaceOpeningSession
	{
		/// <summary>The factory path does not call this method.</summary>
		/// <param name="user">The authenticated session.</param>
		/// <param name="cancellationToken">Cancels the build.</param>
		/// <returns>This method does not return.</returns>
		public Task<WorkspaceOpeningResult> ExecuteAsync(System.Security.Claims.ClaimsPrincipal user, CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("The factory load opened a database session.");
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
