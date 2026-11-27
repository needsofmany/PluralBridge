// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// The first member-card page accepts its avatar response through the workspace view session.
/// These calls exercise that session. They do not execute the browser script or open a database connection.
/// </summary>
public sealed class WorkspaceAvatarCardTests
{
	/// <summary>
	/// The first page requests avatars and applies each image to its card. A null image leaves the placeholder.
	/// </summary>
	[Fact]
	public void FirstBatch_MatchesImagesAndLeavesNullAvatarsBlank()
	{
		var view = Open("anchor", 3);
		var generation = view.DetailGeneration;
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(1, 3)));
		Assert.Contains("GET member-avatars context-a anchor 0", view.Requests);

		view.MemberScrollTop = 18;
		view.MemberCards[0].State = "name";
		view.MemberCards[0].Expanded = true;
		var applied = view.AcceptMemberAvatars(generation, "anchor",
		[
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(1, 1)[0], "red"),
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(2, 1)[0], null),
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(3, 1)[0], "blue")
		]);

		Assert.True(applied);
		Assert.Equal("red", view.MemberCards[0].Avatar);
		Assert.Null(view.MemberCards[1].Avatar);
		Assert.Equal("blue", view.MemberCards[2].Avatar);
		Assert.Equal("name", view.MemberCards[0].State);
		Assert.True(view.MemberCards[0].Expanded);
		Assert.Equal(18, view.MemberScrollTop);
	}

	/// <summary>
	/// A pending avatar edit keeps its preview. A response for another selection changes nothing.
	/// </summary>
	[Fact]
	public void FirstBatch_KeepsPendingEditsAndIgnoresAStaleResponse()
	{
		var view = Open("anchor", 2);
		var generation = view.DetailGeneration;
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(1, 2)));
		view.MemberCards[0].PendingAvatar = true;
		view.MemberCards[0].Avatar = "local";
		view.MemberCards[0].State = "draft";
		view.MemberCards[1].Expanded = true;
		view.MemberScrollTop = 7;

		Assert.True(view.AcceptMemberAvatars(generation, "anchor",
		[
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(1, 1)[0], "red"),
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(2, 1)[0], "blue")
		]));
		Assert.Equal("local", view.MemberCards[0].Avatar);
		Assert.Equal("draft", view.MemberCards[0].State);
		Assert.Equal("blue", view.MemberCards[1].Avatar);
		Assert.True(view.MemberCards[1].Expanded);
		Assert.Equal(7, view.MemberScrollTop);

		view.Select("other");
		Assert.False(view.AcceptMemberAvatars(generation, "anchor",
		[
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(1, 1)[0], "replaced")
		]));
		Assert.Empty(view.MemberCards);
	}

	/// <summary>
	/// A later page does not request avatars. A failed first-page read keeps the cards and does not request again.
	/// </summary>
	[Fact]
	public void LaterPage_DoesNotRequestAvatarsAndFailureDoesNotRetry()
	{
		var view = Open("anchor", 25);
		var generation = view.DetailGeneration;
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(1, 20)));
		Assert.Equal(1, view.Requests.Count(request => request.StartsWith("GET member-avatars ", StringComparison.Ordinal)));
		Assert.True(view.RequestNextPage());
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(21, 5)));
		Assert.Equal(1, view.Requests.Count(request => request.StartsWith("GET member-avatars ", StringComparison.Ordinal)));

		var cards = view.MemberCards.ToArray();
		var requests = view.Requests.Count;
		Assert.True(view.FailMemberAvatars(generation, "anchor"));
		Assert.False(view.FailMemberAvatars(generation, "anchor"));
		Assert.Equal(cards, view.MemberCards);
		Assert.Equal(requests, view.Requests.Count);
		Assert.All(view.MemberCards, card => Assert.Null(card.Avatar));
	}

	/// <summary>
	/// The browser script requests avatars only after the first card page and applies them through the card.
	/// </summary>
	[Fact]
	public void Navigator_RequestsAvatarsAfterTheFirstCardPage()
	{
		var script = File.ReadAllText(SourcePath("js", "navigator.js"));
		var cards = File.ReadAllText(SourcePath("js", "member-card.js"));
		var client = File.ReadAllText(SourcePath("js", "api-client.js"));
		var detail = FunctionBody(script, "function startMemberDetailRequest(row, skip, request, signal, progress)");
		var avatars = FunctionBody(script, "function requestFirstBatchAvatars(row, request, serial)");
		var apply = FunctionBody(script, "function applyFirstBatchAvatars(images)");
		var card = FunctionBody(cards, "cardDetails.applyWorkspaceAvatar = function (objectUrl)");
		var thenStart = detail.IndexOf("paintWorkspaceMemberPage(row, profiles, skip);", StringComparison.Ordinal);
		var avatarCall = detail.IndexOf("requestFirstBatchAvatars(row, request, serial);", StringComparison.Ordinal);
		var thenStale = avatars.IndexOf("serial !== memberDetailSerial", StringComparison.Ordinal);
		var thenApply = avatars.IndexOf("applyFirstBatchAvatars(images);", StringComparison.Ordinal);

		Assert.Contains("skip === 0", detail, StringComparison.Ordinal);
		Assert.True(thenStart >= 0 && thenStart < avatarCall);
		Assert.True(thenStale >= 0 && thenStale < thenApply);
		Assert.Contains("readWorkspaceMemberAvatars(capturedContext, pathKey, openMemberAvatarSignal())", avatars, StringComparison.Ordinal);
		Assert.DoesNotContain("requestNextWorkspaceMemberDetails()", avatars, StringComparison.Ordinal);
		Assert.DoesNotContain("showWorkspaceDetailError(", avatars, StringComparison.Ordinal);
		Assert.DoesNotContain("scrollTop", avatars, StringComparison.Ordinal);
		Assert.DoesNotContain("scrollTop", apply, StringComparison.Ordinal);
		Assert.Contains("applyWorkspaceAvatar", apply, StringComparison.Ordinal);
		Assert.Contains("revokeAvatarObjectUrl(objectUrl)", apply, StringComparison.Ordinal);
		Assert.Contains("surfaceIsModified(avatarWrap)", card, StringComparison.Ordinal);
		Assert.Contains("/member-avatars?", client, StringComparison.Ordinal);
		Assert.Contains("query.set(\"skip\", \"0\")", client, StringComparison.Ordinal);
	}

	/// <summary>Selects an anchor whose preview has the requested number of direct members.</summary>
	/// <param name="path">The anchor path.</param>
	/// <param name="count">How many direct members to retain.</param>
	/// <returns>The view, with the first member-detail request in flight.</returns>
	private static WorkspaceTreeViewSession Open(string path, int count)
	{
		var view = new WorkspaceTreeViewSession();
		view.SetWorkspaceTreeEnabled(true);
		var generation = view.BeginLoad();
		Assert.True(view.CompleteWorkspaceLoad(generation, true, "context-a", [path]));
		view.RetainMemberPreview(Enumerable.Range(1, count)
			.Select(index => new WorkspaceTreeViewSession.MemberPreviewRow(path, "member", Ids(index, 1)[0]))
			.ToArray());
		view.Select(path);
		return view;
	}

	/// <summary>Builds canonical member ids. Index 1 is the first member.</summary>
	/// <param name="start">The first member number.</param>
	/// <param name="count">How many ids to build.</param>
	/// <returns>The ids.</returns>
	private static string[] Ids(int start, int count)
	{
		return Enumerable.Range(start, count)
			.Select(index => Guid.Parse("00000000-0000-0000-0000-" + index.ToString("D12")).ToString("D"))
			.ToArray();
	}

	/// <summary>Reads one function, starting at its declaration, through the matching closing brace.</summary>
	/// <param name="script">The source file.</param>
	/// <param name="signature">The declaration that starts the function.</param>
	/// <returns>The declaration and its body.</returns>
	private static string FunctionBody(string script, string signature)
	{
		var start = script.IndexOf(signature, StringComparison.Ordinal);
		Assert.True(start >= 0);
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
}
