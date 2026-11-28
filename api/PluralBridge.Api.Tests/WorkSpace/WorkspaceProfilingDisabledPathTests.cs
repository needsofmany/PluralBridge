// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PluralBridge.Api.Controllers;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// A disabled workspace open keeps the tree, member, and avatar results and creates no profiling records.
/// An enabled open correlates the first three requests and does not profile a later page.
/// These calls use the production controller and substitutes. They do not open a database connection.
/// </summary>
public sealed class WorkspaceProfilingDisabledPathTests
{
	/// <summary>No run id means no timing profile, response header, or profiling log, and the same bodies as an enabled open.</summary>
	[Fact]
	public async Task DisabledOpen_OmitsProfilingAndMatchesTheEnabledResults()
	{
		var harness = NewHarness();
		var disabledTree = await harness.Get(null);
		var disabledMembers = await harness.Members(disabledTree.ContextId, "0", null);
		var disabledAvatars = await harness.Avatars(disabledTree.ContextId, "0", null);
		var disabledLaterMembers = await harness.Members(disabledTree.ContextId, "20", null);
		var disabledLaterAvatars = await harness.Avatars(disabledTree.ContextId, "20", null);
		Assert.DoesNotContain(harness.Logs, IsProfilingLog);

		var runId = Guid.NewGuid().ToString("D");
		var enabledTree = await harness.Get(runId);
		var enabledMembers = await harness.Members(enabledTree.ContextId, "0", runId);
		var enabledAvatars = await harness.Avatars(enabledTree.ContextId, "0", runId);
		var enabledLaterMembers = await harness.Members(enabledTree.ContextId, "20", runId);
		var enabledLaterAvatars = await harness.Avatars(enabledTree.ContextId, "20", runId);

		Assert.Equal(enabledTree.AccountId, disabledTree.AccountId);
		Assert.Equal(PreviewKeys(enabledTree), PreviewKeys(disabledTree));
		Assert.Equal(JsonSerializer.Serialize(enabledMembers), JsonSerializer.Serialize(disabledMembers));
		Assert.Equal(JsonSerializer.Serialize(enabledAvatars), JsonSerializer.Serialize(disabledAvatars));
		Assert.Equal(JsonSerializer.Serialize(enabledLaterMembers), JsonSerializer.Serialize(disabledLaterMembers));
		Assert.Equal(JsonSerializer.Serialize(enabledLaterAvatars), JsonSerializer.Serialize(disabledLaterAvatars));
		Assert.Contains(AvatarTestDataFactory.RedMemberId, MemberIds(disabledMembers));
		Assert.Contains(AvatarTestDataFactory.BlueMemberId, MemberIds(disabledMembers));
		Assert.Equal(2, AvatarIds(disabledAvatars).Count);

		Assert.Equal(2, harness.Session.Profiles.Count);
		Assert.Null(harness.Session.Profiles[0]);
		Assert.NotNull(harness.Session.Profiles[1]);
		Assert.Equal(
			[true, true, true, true, false, false, true, true],
			harness.Gateway.ResolveProfiles.Select(profile => profile == null).ToArray());
		Assert.All(harness.Reader.BatchProfiles.Take(1), Assert.Null);
		Assert.All(harness.Reader.BatchProfiles.Skip(1).Take(1), profile => Assert.NotNull(profile));
		Assert.Equal(2, harness.Reader.BatchProfiles.Count);
		Assert.Equal(3, harness.Logs.Count(message => message.Contains("RunId=" + runId, StringComparison.Ordinal)));
		Assert.Equal(3, harness.Logs.Count(IsProfilingLog));
		Assert.All(harness.DisabledHeaders, header => Assert.False(header.ContainsKey(WorkspaceTreeLoadProfile.ResponseHeaderName)));
		Assert.Equal(3, harness.EnabledHeaders.Count(header => RunId(header) == runId));
		Assert.Equal(2, harness.LaterHeaders.Count);
		Assert.All(harness.LaterHeaders, header => Assert.False(header.ContainsKey(WorkspaceTreeLoadProfile.ResponseHeaderName)));
	}

	/// <summary>The disabled and enabled view sessions record the same requests. Only the enabled open's first three carry an id.</summary>
	[Fact]
	public void ViewSession_PreservesRequestOrderAndLimitsTheRunId()
	{
		var disabled = Walk(false);
		var enabled = Walk(true);

		Assert.Equal(disabled.View.Requests, enabled.View.Requests);
		Assert.Equal(
			[
				"GET workspace",
				"GET member-details context-a anchor 0",
				"GET member-avatars context-a anchor 0",
				"GET member-details context-a anchor 20",
				"GET member-details context-a anchor 0"
			],
			enabled.View.Requests);
		Assert.All(Enumerable.Range(0, disabled.View.Requests.Count), index => Assert.Null(disabled.View.RequestProfileId(index)));

		var id = enabled.View.OpenProfileId(enabled.Generation);
		Assert.False(string.IsNullOrWhiteSpace(id));
		Assert.Equal(id, enabled.View.RequestProfileId(0));
		Assert.Equal(id, enabled.View.RequestProfileId(1));
		Assert.Equal(id, enabled.View.RequestProfileId(2));
		Assert.Null(enabled.View.RequestProfileId(3));
		Assert.Null(enabled.View.RequestProfileId(4));
	}

	/// <summary>
	/// The browser script gates the run id, request header, timing record, and console report on the profiling flag.
	/// The navigator script uses syntax this host cannot execute, so this check reads that source.
	/// </summary>
	[Fact]
	public void BrowserScript_DisabledOpenCreatesNoProfile()
	{
		var navigator = File.ReadAllText(SourcePath("js", "navigator.js"));
		var load = FunctionBody(navigator, "function startLoad(replacing)");
		var publish = FunctionBody(navigator, "function publishWorkspaceLoadReport(request)");
		var finish = FunctionBody(navigator, "function finishTreeProfile(request, outcome, openContinues)");
		var next = FunctionBody(navigator, "function requestNextWorkspaceMemberDetails()");

		Assert.Contains("request.profileId = request.workspaceMode && workspaceLoadProfileEnabled()", load, StringComparison.Ordinal);
		Assert.Contains("if (request.profileId)", load, StringComparison.Ordinal);
		Assert.Contains("headers[\"X-PB-Workspace-Profile-Id\"] = request.profileId;", load, StringComparison.Ordinal);
		Assert.Contains("if (request.profileId && window.performance && typeof window.performance.now === \"function\")", load, StringComparison.Ordinal);
		Assert.Contains("if (!request || !request.treeProfile || request.treeProfile.reported)", publish, StringComparison.Ordinal);
		Assert.Contains("if (!request || !request.treeProfile || request.treeProfile.finished)", finish, StringComparison.Ordinal);
		Assert.Contains("selectAnchor(row);", navigator, StringComparison.Ordinal);
		Assert.DoesNotContain("profileId", next, StringComparison.Ordinal);
		Assert.DoesNotContain("console.info", finish, StringComparison.Ordinal);
	}

	/// <summary>Walks one workspace open, its later page, and a following anchor selection.</summary>
	/// <param name="profile">True creates a run id for the open.</param>
	/// <returns>The view and the open generation.</returns>
	private static Walked Walk(bool profile)
	{
		var view = new WorkspaceTreeViewSession();
		view.SetWorkspaceTreeEnabled(true);
		view.SetWorkspaceLoadProfile(profile);
		view.RetainMemberPreview(Enumerable.Range(1, 25)
			.Select(index => new WorkspaceTreeViewSession.MemberPreviewRow(
				"anchor",
				"member",
				Guid.Parse("00000000-0000-0000-0000-" + index.ToString("D12")).ToString("D")))
			.ToArray());
		var generation = view.BeginLoad();
		Assert.True(view.CompleteWorkspaceLoad(generation, true, "context-a", ["anchor"]));
		Assert.True(view.ContinueWorkspaceOpen(generation, "anchor"));
		Assert.True(view.AcceptMemberDetails(view.DetailGeneration, "anchor", Enumerable.Range(1, 20)
			.Select(index => Guid.Parse("00000000-0000-0000-0000-" + index.ToString("D12")).ToString("D"))
			.ToArray()));
		Assert.True(view.RequestNextPage());
		view.Select("anchor");
		return new Walked(view, generation);
	}

	/// <param name="View">The view after the walk.</param>
	/// <param name="Generation">The workspace-open generation.</param>
	private sealed record Walked(WorkspaceTreeViewSession View, int Generation);

	/// <summary>Builds the production controller around substitutes that do not connect.</summary>
	/// <returns>The controller harness.</returns>
	private static Harness NewHarness()
	{
		var systemId = Guid.Parse("11111111-1111-4111-8111-111111111111");
		var accountId = AvatarTestDataFactory.AccountId;
		var build = new WorkspaceCapturedBuild(
			accountId,
			[],
			[new WorkspaceOccurrence("account", accountId, "system", systemId, 0, "system-a")],
			[new WorkspaceNodeLabel("system", systemId, "Harbor")],
			[new WorkspaceSystemEdgeLabel(systemId, null)],
			[
				new PluralMapPreviewRow("preview", "system-a", "system-a", "Harbor", "system", systemId, null, false, "Harbor"),
				new PluralMapPreviewRow("preview", "member-red", "member-red", "Member", "member", AvatarTestDataFactory.RedMemberId, "system-a", false, null),
				new PluralMapPreviewRow("preview", "member-blue", "member-blue", "Member", "member", AvatarTestDataFactory.BlueMemberId, "system-a", false, null)
			],
			[new WorkspaceAccessFact(Guid.NewGuid(), "system", systemId, 1, "Active", true)]);
		var session = new OpeningSession(build);
		var logs = new List<string>();
		var store = new WorkspaceContextStore(new ManualWorkspaceClock(), new MutableWorkspaceContextSettings(), new DiscardLogger());
		var gateway = new ProfileGateway(accountId);
		var reader = new FactoryReader();
		var controller = new WorkspaceTreeController(
			new ConfigurationBuilder().Build(),
			new WorkspaceTreeService(session, store),
			store,
			new ListLogger(logs));
		return new Harness(controller, new ServiceBox(
			new WorkspaceMemberDetailService(store, gateway),
			new WorkspaceAvatarBatchService(store, gateway, reader)), session, gateway, reader, logs);
	}

	/// <summary>Reads the preview path keys in order.</summary>
	/// <param name="response">The tree response.</param>
	/// <returns>The path keys.</returns>
	private static string[] PreviewKeys(WorkspaceTreeResponse response)
	{
		return response.HierarchyPreview.Select(row => row.PathKey).ToArray();
	}

	/// <summary>Reads member ids from a member-detail body.</summary>
	/// <param name="body">The action body.</param>
	/// <returns>The ids.</returns>
	private static List<Guid> MemberIds(object body)
	{
		using var document = JsonDocument.Parse(JsonSerializer.Serialize(body));
		return document.RootElement.EnumerateArray().Select(row => row.GetProperty("memberId").GetGuid()).ToList();
	}

	/// <summary>Reads member ids from an avatar body.</summary>
	/// <param name="body">The action body.</param>
	/// <returns>The ids.</returns>
	private static List<Guid> AvatarIds(object body)
	{
		using var document = JsonDocument.Parse(JsonSerializer.Serialize(body));
		return document.RootElement.EnumerateArray().Select(row => row.GetProperty("memberId").GetGuid()).ToList();
	}

	/// <summary>Reads the run id from a profiling response header.</summary>
	/// <param name="headers">The response headers.</param>
	/// <returns>The run id, or null when the header is absent.</returns>
	private static string? RunId(IHeaderDictionary headers)
	{
		if (!headers.ContainsKey(WorkspaceTreeLoadProfile.ResponseHeaderName))
		{
			return null;
		}

		using var document = JsonDocument.Parse(headers[WorkspaceTreeLoadProfile.ResponseHeaderName].ToString());
		return document.RootElement.GetProperty("runId").GetString();
	}

	/// <summary>Reports whether a log line is a profiling diagnostic.</summary>
	/// <param name="message">The formatted log line.</param>
	/// <returns>True for a tree, member, or avatar profiling line.</returns>
	private static bool IsProfilingLog(string message)
	{
		return message.Contains("WorkspaceTreeProfile ", StringComparison.Ordinal)
			|| message.Contains("WorkspaceMemberDetailProfile ", StringComparison.Ordinal)
			|| message.Contains("WorkspaceAvatarBatchProfile ", StringComparison.Ordinal);
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
	private static string SourcePath(string folder, string fileName, [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
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

	/// <param name="Controller">The production workspace controller.</param>
	/// <param name="Services">Member and avatar services for the controller.</param>
	/// <param name="Session">The opening substitute.</param>
	/// <param name="Gateway">The profile substitute.</param>
	/// <param name="Reader">The avatar substitute.</param>
	/// <param name="Logs">Lines written by the controller logger.</param>
	private sealed class Harness(
		WorkspaceTreeController Controller,
		ServiceBox Services,
		OpeningSession Session,
		ProfileGateway Gateway,
		FactoryReader Reader,
		List<string> Logs)
	{
		/// <summary>The opening substitute.</summary>
		internal OpeningSession Session { get; } = Session;

		/// <summary>The profile substitute.</summary>
		internal ProfileGateway Gateway { get; } = Gateway;

		/// <summary>The avatar substitute.</summary>
		internal FactoryReader Reader { get; } = Reader;

		/// <summary>Controller log lines.</summary>
		internal List<string> Logs { get; } = Logs;

		/// <summary>Response headers from the disabled calls.</summary>
		internal List<IHeaderDictionary> DisabledHeaders { get; } = [];

		/// <summary>Response headers from the enabled first-page calls.</summary>
		internal List<IHeaderDictionary> EnabledHeaders { get; } = [];

		/// <summary>Response headers from the later-page calls.</summary>
		internal List<IHeaderDictionary> LaterHeaders { get; } = [];

		/// <summary>Loads the tree.</summary>
		/// <param name="runId">The profiling id. Null sends no request header.</param>
		/// <returns>The tree body.</returns>
		internal async Task<WorkspaceTreeResponse> Get(string? runId)
		{
			Bind(runId, false);
			var result = await Controller.Get(CancellationToken.None);
			var ok = Assert.IsType<OkObjectResult>(result);
			return Assert.IsType<WorkspaceTreeResponse>(ok.Value);
		}

		/// <summary>Loads one member page.</summary>
		/// <param name="contextId">The context from the tree response.</param>
		/// <param name="skip">The page skip.</param>
		/// <param name="runId">The profiling id. Null sends no request header.</param>
		/// <returns>The member body.</returns>
		internal async Task<object> Members(string contextId, string skip, string? runId)
		{
			Bind(runId, skip != "0");
			var result = await Controller.MemberDetails(contextId, "system-a", CancellationToken.None, skip);
			var ok = Assert.IsType<OkObjectResult>(result);
			return ok.Value ?? throw new InvalidOperationException("The member body was empty.");
		}

		/// <summary>Loads one avatar page.</summary>
		/// <param name="contextId">The context from the tree response.</param>
		/// <param name="skip">The page skip.</param>
		/// <param name="runId">The profiling id. Null sends no request header.</param>
		/// <returns>The avatar body.</returns>
		internal async Task<object> Avatars(string contextId, string skip, string? runId)
		{
			Bind(runId, skip != "0");
			var result = await Controller.MemberAvatars(contextId, "system-a", CancellationToken.None, skip);
			var ok = Assert.IsType<OkObjectResult>(result);
			return ok.Value ?? throw new InvalidOperationException("The avatar body was empty.");
		}

		/// <summary>Binds a new request and keeps its response headers.</summary>
		/// <param name="runId">The profiling id. Null sends no request header.</param>
		/// <param name="later">True stores the headers with the later-page calls.</param>
		private void Bind(string? runId, bool later)
		{
			var http = new DefaultHttpContext
			{
				RequestServices = Services
			};
			if (runId != null)
			{
				http.Request.Headers[WorkspaceTreeLoadProfile.RequestHeaderName] = runId;
			}

			Controller.ControllerContext = new ControllerContext
			{
				HttpContext = http
			};
			if (runId == null)
			{
				DisabledHeaders.Add(http.Response.Headers);
			}
			else if (later)
			{
				LaterHeaders.Add(http.Response.Headers);
			}
			else
			{
				EnabledHeaders.Add(http.Response.Headers);
			}
		}
	}

	/// <summary>Returns the captured build and records the profile passed by the production service.</summary>
	private sealed class OpeningSession(WorkspaceCapturedBuild build) : IWorkspaceOpeningSession
	{
		/// <summary>Profiles passed to the profiled execute, in call order. Null means that open was not profiled.</summary>
		internal List<WorkspaceTreeLoadProfile?> Profiles { get; } = [];

		/// <inheritdoc />
		public Task<WorkspaceOpeningResult> ExecuteAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
		{
			return Task.FromResult<WorkspaceOpeningResult>(new WorkspaceOpeningResult.Ready(build));
		}

		/// <inheritdoc />
		public Task<WorkspaceOpeningResult> ExecuteProfiledAsync(ClaimsPrincipal user, CancellationToken cancellationToken, WorkspaceTreeLoadProfile? profile)
		{
			Profiles.Add(profile);
			return ExecuteAsync(user, cancellationToken);
		}
	}

	/// <summary>Resolves one account and authorizes the two page members. It records each profiling argument.</summary>
	private sealed class ProfileGateway(Guid accountId) : IWorkspaceProfileGateway
	{
		/// <summary>Profiles passed to account resolution, in call order.</summary>
		internal List<IWorkspaceGatewayTiming?> ResolveProfiles { get; } = [];

		/// <inheritdoc />
		public Task<AccessContextHelper.Account?> ResolveAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
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
		public Task<AccessContextHelper.Account?> ResolveProfiledAsync(ClaimsPrincipal user, CancellationToken cancellationToken, IWorkspaceGatewayTiming? profile)
		{
			ResolveProfiles.Add(profile);
			return ResolveAsync(user, cancellationToken);
		}

		/// <inheritdoc />
		public Task<AnchorProfileBundle> ReadProfilesAsync(
			string email,
			string objectType,
			string anchorName,
			string branch,
			CancellationToken cancellationToken)
		{
			var profiles = new[]
			{
				AvatarTestDataFactory.RedMemberId,
				AvatarTestDataFactory.BlueMemberId
			}.Select(id => new AnchorMemberProfile(id, "Member", null, null, null, null, null, null, false)).ToArray();
			return Task.FromResult(new AnchorProfileBundle(profiles, true, [], [], [], []));
		}
	}

	/// <summary>Loads one miss list through the avatar factory and records the profiling argument once.</summary>
	private sealed class FactoryReader : IWorkspaceAvatarReader
	{
		/// <summary>Profiles passed to image reads, in call order. The single-member path still records here.</summary>
		internal List<WorkspaceAvatarBatchProfile?> ReadProfiles { get; } = [];

		/// <summary>Profiles passed to the batch load, one entry per call.</summary>
		internal List<WorkspaceAvatarBatchProfile?> BatchProfiles { get; } = [];

		/// <inheritdoc />
		public Task<(byte[] Bytes, string ContentType)?> ReadAsync(Guid accountId, Guid memberId, CancellationToken cancellationToken)
		{
			return AnchorMemberProfileSave.LoadAvatarAsync(
				useDatabase: false,
				accountId,
				memberId,
				cancellationToken,
				testDataFactory: AvatarTestDataFactory.Read);
		}

		/// <inheritdoc />
		public Task<(byte[] Bytes, string ContentType)?> ReadProfiledAsync(
			Guid accountId,
			Guid memberId,
			CancellationToken cancellationToken,
			WorkspaceAvatarBatchProfile? profile)
		{
			ReadProfiles.Add(profile);
			return ReadAsync(accountId, memberId, cancellationToken);
		}

		/// <inheritdoc />
		public Task<IReadOnlyDictionary<Guid, (byte[] Bytes, string ContentType)>> ReadManyAsync(
			Guid accountId,
			IReadOnlyList<Guid> memberIds,
			CancellationToken cancellationToken,
			WorkspaceAvatarBatchProfile? profile = null)
		{
			BatchProfiles.Add(profile);
			return AnchorMemberProfileSave.LoadAvatarsAsync(
				useDatabase: false,
				accountId,
				memberIds,
				cancellationToken,
				testDataFactory: AvatarTestDataFactory.Read);
		}
	}

	/// <summary>Supplies the member and avatar services to the controller.</summary>
	private sealed class ServiceBox(WorkspaceMemberDetailService details, WorkspaceAvatarBatchService avatars) : IServiceProvider
	{
		/// <inheritdoc />
		public object? GetService(Type serviceType)
		{
			if (serviceType == typeof(WorkspaceMemberDetailService))
			{
				return details;
			}

			if (serviceType == typeof(WorkspaceAvatarBatchService))
			{
				return avatars;
			}

			return null;
		}
	}

	/// <summary>Collects controller log lines.</summary>
	private sealed class ListLogger(List<string> messages) : ILogger<WorkspaceTreeController>
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
			messages.Add(formatter(state, exception));
		}
	}

	/// <summary>Discards store messages.</summary>
	private sealed class DiscardLogger : ILogger<WorkspaceContextStore>
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
}
