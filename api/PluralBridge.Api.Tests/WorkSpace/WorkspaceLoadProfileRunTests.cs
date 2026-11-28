// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// One workspace open shares a profiling run id across its tree, first member page, and first avatar page.
/// These calls exercise the workspace view session. They do not execute the browser script or open a database connection.
/// </summary>
public sealed class WorkspaceLoadProfileRunTests
{
	/// <summary>The enabled open sends one id on the tree, the first member page, and the first avatar page.</summary>
	[Fact]
	public void EnabledOpen_SharesOneId()
	{
		var view = Open(profile: true, members: 1);
		var generation = view.BeginLoad();
		Assert.True(view.CompleteWorkspaceLoad(generation, true, "context-a", ["anchor"]));
		Assert.True(view.ContinueWorkspaceOpen(generation, "anchor"));
		Assert.True(view.AcceptMemberDetails(view.DetailGeneration, "anchor", [Member(1)]));

		var id = view.OpenProfileId(generation);
		Assert.False(string.IsNullOrWhiteSpace(id));
		Assert.Equal(id, Header(view, "GET workspace"));
		Assert.Equal(id, Header(view, "GET member-details context-a anchor 0"));
		Assert.Equal(id, Header(view, "GET member-avatars context-a anchor 0"));
	}

	/// <summary>A second workspace open creates a different id.</summary>
	[Fact]
	public void NewOpen_GetsADifferentId()
	{
		var view = Open(profile: true, members: 1);
		var first = view.BeginLoad();
		var second = view.BeginLoad();

		Assert.NotEqual(view.OpenProfileId(first), view.OpenProfileId(second));
		Assert.False(string.IsNullOrWhiteSpace(view.OpenProfileId(first)));
		Assert.False(string.IsNullOrWhiteSpace(view.OpenProfileId(second)));
	}

	/// <summary>Profiling left off sends no run id.</summary>
	[Fact]
	public void Disabled_SendsNoHeader()
	{
		var view = Open(profile: false, members: 1);
		var generation = view.BeginLoad();
		Assert.True(view.CompleteWorkspaceLoad(generation, true, "context-a", ["anchor"]));
		Assert.True(view.ContinueWorkspaceOpen(generation, "anchor"));
		Assert.True(view.AcceptMemberDetails(view.DetailGeneration, "anchor", [Member(1)]));

		Assert.Null(view.OpenProfileId(generation));
		Assert.All(Enumerable.Range(0, view.Requests.Count), index => Assert.Null(view.RequestProfileId(index)));
	}

	/// <summary>A later member page and a later anchor selection do not carry the open's id.</summary>
	[Fact]
	public void LaterPageAndLaterSelection_HaveNoHeader()
	{
		var view = Open(profile: true, members: 25);
		var generation = view.BeginLoad();
		Assert.True(view.CompleteWorkspaceLoad(generation, true, "context-a", ["anchor"]));
		Assert.True(view.ContinueWorkspaceOpen(generation, "anchor"));
		Assert.True(view.AcceptMemberDetails(view.DetailGeneration, "anchor", Members(1, 20)));
		Assert.True(view.RequestNextPage());
		view.Select("anchor");

		var id = view.OpenProfileId(generation);
		Assert.Equal(id, Header(view, "GET member-avatars context-a anchor 0"));
		Assert.Null(Header(view, "GET member-details context-a anchor 20"));
		Assert.Null(view.RequestProfileId(view.Requests.Count - 1));
		Assert.StartsWith("GET member-details ", view.Requests[^1], StringComparison.Ordinal);
	}

	/// <summary>A stale open does not select, and it does not attach the newer open's id to its own request.</summary>
	[Fact]
	public void StaleOpen_DoesNotTakeTheNewerId()
	{
		var view = Open(profile: true, members: 1);
		var first = view.BeginLoad();
		Assert.True(view.CompleteWorkspaceLoad(first, true, "context-a", ["anchor"]));
		Assert.True(view.ContinueWorkspaceOpen(first, "anchor"));
		var detailGeneration = view.DetailGeneration;
		var firstId = view.OpenProfileId(first);

		var second = view.BeginLoad();
		Assert.False(view.ContinueWorkspaceOpen(first, "anchor"));
		Assert.False(view.AcceptMemberDetails(detailGeneration, "anchor", [Member(1)]));
		Assert.DoesNotContain(view.Requests, request => request.StartsWith("GET member-avatars ", StringComparison.Ordinal));
		Assert.Equal(firstId, Header(view, "GET member-details context-a anchor 0"));
		Assert.NotEqual(firstId, view.OpenProfileId(second));
		Assert.Equal(view.OpenProfileId(second), Header(view, "GET workspace", occurrence: 1));
	}

	/// <summary>The browser script puts the captured run id on the three opening requests and not on a later page or click.</summary>
	[Fact]
	public void Navigator_BuildsTheHeaderFromTheCapturedOpen()
	{
		var navigator = File.ReadAllText(SourcePath("js", "navigator.js"));
		var client = File.ReadAllText(SourcePath("js", "api-client.js"));
		var load = FunctionBody(navigator, "function startLoad(replacing)");
		var selectAnchor = FunctionBody(navigator, "function selectAnchor(row, profileId, profileRequest)");
		var openDetails = FunctionBody(navigator, "function loadWorkspaceMemberDetails(row, profileId, profileRequest)");
		var details = FunctionBody(navigator, "function startMemberDetailRequest(row, skip, request, signal, progress)");
		var avatars = FunctionBody(navigator, "function requestFirstBatchAvatars(row, request, serial, skip, pageCards)");
		var next = FunctionBody(navigator, "function requestNextWorkspaceMemberDetails()");
		var anchorProfiles = FunctionBody(navigator, "function loadAnchorProfiles(row)");
		var current = load.IndexOf("if (!loadIsCurrent(request))", StringComparison.Ordinal);
		var select = load.IndexOf("selectAnchor(anchor, request.profileId, request);", StringComparison.Ordinal);

		Assert.Contains("request.profileId = request.workspaceMode && workspaceLoadProfileEnabled()", load, StringComparison.Ordinal);
		Assert.Contains("headers[\"X-PB-Workspace-Profile-Id\"] = request.profileId;", load, StringComparison.Ordinal);
		Assert.True(current >= 0 && current < select);
		Assert.Contains("loadWorkspaceMemberDetails(row, profileId, profileRequest);", selectAnchor, StringComparison.Ordinal);
		Assert.Contains("request.profileId === profileRequest.treeProfile.runId", openDetails, StringComparison.Ordinal);
		Assert.Contains("selectAnchor(row);", navigator, StringComparison.Ordinal);
		Assert.DoesNotContain("selectAnchor(row, profileId, profileRequest);", navigator, StringComparison.Ordinal);
		Assert.Contains("request.profileId = profileId || null;", navigator, StringComparison.Ordinal);
		Assert.Contains("skip === 0 ? request.profileId : null", details, StringComparison.Ordinal);
		var avatarCall = avatars.IndexOf("readWorkspaceMemberAvatars(", StringComparison.Ordinal);
		var avatarContext = avatars.IndexOf("capturedContext,", avatarCall, StringComparison.Ordinal);
		var avatarPath = avatars.IndexOf("pathKey,", avatarContext, StringComparison.Ordinal);
		var avatarSignal = avatars.IndexOf("openMemberAvatarSignal(),", avatarPath, StringComparison.Ordinal);
		var avatarId = avatars.IndexOf("avatarSkip === 0 ? request.profileId : null", avatarSignal, StringComparison.Ordinal);
		Assert.True(avatarCall >= 0 && avatarId > avatarSignal && avatarSignal > avatarPath && avatarPath > avatarContext && avatarContext > avatarCall);
		Assert.DoesNotContain("profileId", next, StringComparison.Ordinal);
		Assert.DoesNotContain("X-PB-Workspace-Profile-Id", anchorProfiles, StringComparison.Ordinal);
		Assert.Contains("headers[\"X-PB-Workspace-Profile-Id\"] = profileId;", client, StringComparison.Ordinal);
		Assert.Contains("memberSkip === 0 && profileId", client, StringComparison.Ordinal);
	}

	/// <summary>Prepares a workspace view with direct members under anchor.</summary>
	/// <param name="profile">True enables the profiling switch.</param>
	/// <param name="members">How many direct members to retain.</param>
	/// <returns>The view, before the tree request starts.</returns>
	private static WorkspaceTreeViewSession Open(bool profile, int members)
	{
		var view = new WorkspaceTreeViewSession();
		view.SetWorkspaceTreeEnabled(true);
		view.SetWorkspaceLoadProfile(profile);
		view.RetainMemberPreview(Enumerable.Range(1, members)
			.Select(index => new WorkspaceTreeViewSession.MemberPreviewRow("anchor", "member", Member(index)))
			.ToArray());
		return view;
	}

	/// <summary>Finds the profiling id recorded for one request.</summary>
	/// <param name="view">The view.</param>
	/// <param name="request">The request text.</param>
	/// <param name="occurrence">Which match to read. Zero is the first.</param>
	/// <returns>The run id, or null when that request sent no profiling header.</returns>
	private static string? Header(WorkspaceTreeViewSession view, string request, int occurrence = 0)
	{
		var seen = 0;
		for (var index = 0; index < view.Requests.Count; index++)
		{
			if (!string.Equals(view.Requests[index], request, StringComparison.Ordinal))
			{
				continue;
			}

			if (seen == occurrence)
			{
				return view.RequestProfileId(index);
			}

			seen++;
		}

		throw new InvalidOperationException("The request was not recorded.");
	}

	/// <summary>Builds one canonical member id.</summary>
	/// <param name="index">The member number. One is the first member.</param>
	/// <returns>The id.</returns>
	private static string Member(int index)
	{
		return Guid.Parse("00000000-0000-0000-0000-" + index.ToString("D12")).ToString("D");
	}

	/// <summary>Builds consecutive member ids.</summary>
	/// <param name="start">The first member number.</param>
	/// <param name="count">How many ids to build.</param>
	/// <returns>The ids.</returns>
	private static string[] Members(int start, int count)
	{
		return Enumerable.Range(start, count).Select(Member).ToArray();
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
}
