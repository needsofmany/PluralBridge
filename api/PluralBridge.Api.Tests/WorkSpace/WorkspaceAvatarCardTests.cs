// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// Member-card pages accept avatar responses through the workspace view session.
/// These calls exercise that session. They do not execute the browser script or open a database connection.
/// Tests whose names start with Navigator read the production script and do not run it.
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
	/// A later page requests its own avatars. A failed read clears only that page, keeps the cards, and does not request again.
	/// </summary>
	[Fact]
	public void LaterPage_RequestsAvatarsAndFailureStaysOnThatPage()
	{
		var view = Open("anchor", 25);
		var generation = view.DetailGeneration;
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(1, 20)));
		Assert.Contains("GET member-avatars context-a anchor 0", view.Requests);
		view.MemberCards[0].Avatar = "kept";
		view.MemberCards[0].State = "draft";
		view.MemberCards[0].Expanded = true;
		view.MemberScrollTop = 30;

		Assert.True(view.RequestNextPage());
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(21, 5)));
		Assert.Contains("GET member-avatars context-a anchor 20", view.Requests);
		Assert.Equal(2, view.Requests.Count(request => request.StartsWith("GET member-avatars ", StringComparison.Ordinal)));

		var cards = view.MemberCards.ToArray();
		var requests = view.Requests.Count;
		Assert.True(view.FailMemberAvatars(generation, "anchor", 20));
		Assert.False(view.FailMemberAvatars(generation, "anchor", 20));
		Assert.Equal(cards, view.MemberCards);
		Assert.Equal(requests, view.Requests.Count);
		Assert.All(view.MemberCards.Skip(20), card => Assert.False(card.AvatarLoading));
		Assert.All(view.MemberCards.Skip(20), card => Assert.Null(card.Avatar));
		Assert.All(view.MemberCards.Take(20), card => Assert.True(card.AvatarLoading));
		Assert.Equal("kept", view.MemberCards[0].Avatar);
		Assert.Equal("draft", view.MemberCards[0].State);
		Assert.True(view.MemberCards[0].Expanded);
		Assert.Equal(30, view.MemberScrollTop);
	}

	/// <summary>
	/// A later-page response updates only its cards while the first page is still waiting. A new selection drops both responses.
	/// </summary>
	[Fact]
	public void LaterPage_AppliesItsImagesWhileTheFirstRequestIsOpen()
	{
		var view = Open("anchor", 25);
		var generation = view.DetailGeneration;
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(1, 20)));
		view.MemberCards[0].PendingAvatar = true;
		view.MemberCards[0].Avatar = "local";
		view.MemberCards[0].State = "draft";
		view.MemberCards[0].Expanded = true;
		view.MemberScrollTop = 11;
		Assert.True(view.RequestNextPage());
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(21, 5)));
		view.MemberCards[20].PendingAvatar = true;
		view.MemberCards[20].Avatar = "local-later";
		view.MemberCards[20].State = "note";
		view.MemberCards[20].Expanded = true;

		Assert.True(view.AcceptMemberAvatars(generation, "anchor",
		[
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(22, 1)[0], "green"),
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(23, 1)[0], null),
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(1, 1)[0], "should-not-apply")
		], 20));

		Assert.Equal("local", view.MemberCards[0].Avatar);
		Assert.Equal("draft", view.MemberCards[0].State);
		Assert.True(view.MemberCards[0].Expanded);
		Assert.True(view.MemberCards[0].AvatarLoading);
		Assert.Equal("local-later", view.MemberCards[20].Avatar);
		Assert.Equal("note", view.MemberCards[20].State);
		Assert.True(view.MemberCards[20].Expanded);
		Assert.False(view.MemberCards[20].AvatarLoading);
		Assert.Equal("green", view.MemberCards[21].Avatar);
		Assert.True(view.MemberCards[21].AvatarLoading);
		Assert.Null(view.MemberCards[22].Avatar);
		Assert.False(view.MemberCards[22].AvatarLoading);
		Assert.Equal(11, view.MemberScrollTop);

		Assert.True(view.AcceptMemberAvatars(generation, "anchor",
		[
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(1, 1)[0], "red"),
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(21, 1)[0], "late")
		]));
		Assert.Equal("local", view.MemberCards[0].Avatar);
		Assert.Equal("green", view.MemberCards[21].Avatar);

		view.Select("other");
		Assert.False(view.AcceptMemberAvatars(generation, "anchor",
		[
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(2, 1)[0], "replaced")
		]));
		Assert.False(view.AcceptMemberAvatars(generation, "anchor",
		[
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(21, 1)[0], "replaced")
		], 20));
		Assert.Empty(view.MemberCards);
	}

	/// <summary>
	/// Every card on a requested page shows its own indicator. The first page stays marked when the next page starts.
	/// </summary>
	[Fact]
	public void AvatarLoading_MarksEveryRequestedCardOnEachPage()
	{
		var view = Open("anchor", 25);
		var generation = view.DetailGeneration;
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(1, 20)));
		Assert.Equal(20, view.MemberCards.Count);
		Assert.All(view.MemberCards, card => Assert.True(card.AvatarLoading));

		Assert.True(view.RequestNextPage());
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(21, 5)));
		Assert.All(view.MemberCards, card => Assert.True(card.AvatarLoading));
	}

	/// <summary>
	/// A null image removes that card's bar. An image stays applied and keeps its bar until that image settles.
	/// </summary>
	[Fact]
	public void AvatarLoading_ClearsWhenTheImageIsAbsentOrSettled()
	{
		var view = Open("anchor", 3);
		var generation = view.DetailGeneration;
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(1, 3)));
		view.MemberScrollTop = 12;
		view.MemberCards[0].Expanded = true;
		view.MemberCards[2].State = "note";

		Assert.True(view.AcceptMemberAvatars(generation, "anchor",
		[
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(1, 1)[0], null),
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(2, 1)[0], "red"),
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(3, 1)[0], "blue")
		]));

		Assert.Null(view.MemberCards[0].Avatar);
		Assert.False(view.MemberCards[0].AvatarLoading);
		Assert.Equal("red", view.MemberCards[1].Avatar);
		Assert.True(view.MemberCards[1].AvatarLoading);
		Assert.Equal("blue", view.MemberCards[2].Avatar);
		Assert.True(view.MemberCards[2].AvatarLoading);
		Assert.True(view.MemberCards[0].Expanded);
		Assert.Equal("note", view.MemberCards[2].State);
		Assert.Equal(12, view.MemberScrollTop);

		Assert.True(view.SettleMemberAvatar(generation, "anchor", Ids(2, 1)[0]));
		Assert.False(view.MemberCards[1].AvatarLoading);
		Assert.True(view.MemberCards[2].AvatarLoading);
		Assert.Equal("blue", view.MemberCards[2].Avatar);
	}

	/// <summary>
	/// Failure, a pending edit, and a replaced selection remove only the indicators that request owned.
	/// </summary>
	[Fact]
	public void AvatarLoading_ClearsOnFailureEditAndIgnoresAStaleResponse()
	{
		var failed = Open("anchor", 2);
		var failedGeneration = failed.DetailGeneration;
		Assert.True(failed.AcceptMemberDetails(failedGeneration, "anchor", Ids(1, 2)));
		failed.MemberCards[0].State = "draft";
		failed.MemberCards[1].Expanded = true;
		failed.MemberScrollTop = 4;
		Assert.True(failed.FailMemberAvatars(failedGeneration, "anchor"));
		Assert.All(failed.MemberCards, card => Assert.False(card.AvatarLoading));
		Assert.All(failed.MemberCards, card => Assert.Null(card.Avatar));
		Assert.Equal("draft", failed.MemberCards[0].State);
		Assert.True(failed.MemberCards[1].Expanded);
		Assert.Equal(4, failed.MemberScrollTop);

		var view = Open("anchor", 2);
		var generation = view.DetailGeneration;
		Assert.True(view.AcceptMemberDetails(generation, "anchor", Ids(1, 2)));
		view.MemberCards[0].PendingAvatar = true;
		view.MemberCards[0].Avatar = "local";
		Assert.True(view.AcceptMemberAvatars(generation, "anchor",
		[
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(1, 1)[0], "red"),
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(2, 1)[0], "blue")
		]));
		Assert.Equal("local", view.MemberCards[0].Avatar);
		Assert.False(view.MemberCards[0].AvatarLoading);
		Assert.Equal("blue", view.MemberCards[1].Avatar);
		Assert.True(view.MemberCards[1].AvatarLoading);

		view.RetainMemberPreview(
		[
			new WorkspaceTreeViewSession.MemberPreviewRow("anchor", "member", Ids(1, 1)[0]),
			new WorkspaceTreeViewSession.MemberPreviewRow("other", "member", Ids(3, 1)[0])
		]);
		view.Select("other");
		Assert.Empty(view.MemberCards);
		Assert.True(view.AcceptMemberDetails(view.DetailGeneration, "other", Ids(3, 1)));
		Assert.True(view.MemberCards[0].AvatarLoading);
		Assert.False(view.AcceptMemberAvatars(generation, "anchor",
		[
			new WorkspaceTreeViewSession.MemberAvatarImage(Ids(3, 1)[0], "replaced")
		]));
		Assert.True(view.MemberCards[0].AvatarLoading);
		Assert.Null(view.MemberCards[0].Avatar);
	}

	/// <summary>
	/// The production script gives each requested card the existing bar and does not add a shared bar under the list.
	/// This reads the source. It does not execute the script.
	/// </summary>
	[Fact]
	public void Navigator_UsesOneExistingBarPerLoadingAvatar()
	{
		var script = File.ReadAllText(SourcePath("js", "navigator.js"));
		var cards = File.ReadAllText(SourcePath("js", "member-card.js"));
		var style = File.ReadAllText(SourcePath("css", "navigator.css"));
		var request = FunctionBody(script, "function requestFirstBatchAvatars(row, request, serial, skip, pageCards)");
		var cardsIn = FunctionBody(script, "function avatarCardsIn(root)");
		var apply = FunctionBody(script, "function applyFirstBatchAvatars(images, profileRequest");
		var card = FunctionBody(cards, "cardDetails.setWorkspaceAvatarLoading = function (loading)");
		var settle = FunctionBody(cards, "function clearAvatarLoadWhenSettled(preview)");
		var applyCard = FunctionBody(cards, "cardDetails.applyWorkspaceAvatar = function (objectUrl)");
		var detail = FunctionBody(script, "function startMemberDetailRequest(row, skip, request, signal, progress)");

		var showed = request.IndexOf("setCardsAvatarLoading(loadingCards, true)", StringComparison.Ordinal);
		var fetch = request.IndexOf("readWorkspaceMemberAvatars(", StringComparison.Ordinal);
		Assert.True(showed >= 0 && showed < fetch);
		Assert.Contains("setCardsAvatarLoading(loadingCards, false)", request, StringComparison.Ordinal);
		Assert.DoesNotContain("showPagedMemberProgress", request, StringComparison.Ordinal);
		Assert.Contains("querySelectorAll(\".member-card\")", cardsIn, StringComparison.Ordinal);
		Assert.DoesNotContain("[0]", cardsIn, StringComparison.Ordinal);
		Assert.Equal(1, detail.Split("requestFirstBatchAvatars(", StringSplitOptions.None).Length - 1);
		Assert.Contains("requestFirstBatchAvatars(row, request, serial, skip, paintedCards);", detail, StringComparison.Ordinal);
		Assert.Contains("skip === 0", detail, StringComparison.Ordinal);
		Assert.Contains("setWorkspaceAvatarLoading(false)", apply, StringComparison.Ordinal);
		Assert.Contains("navigator-load-progress member-avatar-load", card, StringComparison.Ordinal);
		Assert.Contains("addEventListener(\"load\", settled)", settle, StringComparison.Ordinal);
		Assert.Contains("addEventListener(\"error\", settled)", settle, StringComparison.Ordinal);
		Assert.Contains("hideAvatarLoad()", applyCard, StringComparison.Ordinal);
		Assert.Contains("animation: navigator-load-progress", style, StringComparison.Ordinal);
		Assert.Contains(".member-avatar-load", style, StringComparison.Ordinal);
		Assert.DoesNotContain("scrollTop", request, StringComparison.Ordinal);
	}

	/// <summary>
	/// The browser script requests avatars after every card page and applies them through the card.
	/// This reads the source. It does not execute the script.
	/// </summary>
	[Fact]
	public void Navigator_RequestsAvatarsAfterEveryCardPage()
	{
		var script = File.ReadAllText(SourcePath("js", "navigator.js"));
		var cards = File.ReadAllText(SourcePath("js", "member-card.js"));
		var client = File.ReadAllText(SourcePath("js", "api-client.js"));
		var detail = FunctionBody(script, "function startMemberDetailRequest(row, skip, request, signal, progress)");
		var avatars = FunctionBody(script, "function requestFirstBatchAvatars(row, request, serial, skip, pageCards)");
		var apply = FunctionBody(script, "function applyFirstBatchAvatars(images, profileRequest, loadingCards)");
		var signal = FunctionBody(script, "function openMemberAvatarSignal()");
		var begin = FunctionBody(script, "function beginRequest()");
		var card = FunctionBody(cards, "cardDetails.applyWorkspaceAvatar = function (objectUrl)");
		var thenStart = detail.IndexOf("paintWorkspaceMemberPage(row, profiles, skip);", StringComparison.Ordinal);
		var avatarCall = detail.IndexOf("requestFirstBatchAvatars(row, request, serial, skip, paintedCards);", StringComparison.Ordinal);
		var thenStale = avatars.IndexOf("serial !== memberDetailSerial", StringComparison.Ordinal);
		var thenApply = avatars.IndexOf("applyFirstBatchAvatars(images, request", StringComparison.Ordinal);

		Assert.Contains("skip === 0", detail, StringComparison.Ordinal);
		Assert.True(thenStart >= 0 && thenStart < avatarCall);
		Assert.True(thenStale >= 0 && thenStale < thenApply);
		var avatarRead = avatars.IndexOf("readWorkspaceMemberAvatars(", StringComparison.Ordinal);
		var avatarContext = avatars.IndexOf("capturedContext,", avatarRead, StringComparison.Ordinal);
		var avatarSignal = avatars.IndexOf("openMemberAvatarSignal(),", avatarContext, StringComparison.Ordinal);
		var avatarSkipArg = avatars.IndexOf("avatarSkip", avatarSignal, StringComparison.Ordinal);
		Assert.True(avatarRead >= 0 && avatarRead < avatarContext && avatarContext < avatarSignal && avatarSignal < avatarSkipArg);
		Assert.Contains("avatarSkip === 0 ? request.profileId : null", avatars, StringComparison.Ordinal);
		Assert.DoesNotContain(".abort()", signal, StringComparison.Ordinal);
		Assert.Contains("memberAvatarControllers.push(controller)", signal, StringComparison.Ordinal);
		Assert.Contains("controller.abort()", begin, StringComparison.Ordinal);
		Assert.DoesNotContain("requestNextWorkspaceMemberDetails()", avatars, StringComparison.Ordinal);
		Assert.DoesNotContain("showWorkspaceDetailError(", avatars, StringComparison.Ordinal);
		Assert.DoesNotContain("scrollTop", avatars, StringComparison.Ordinal);
		Assert.DoesNotContain("scrollTop", apply, StringComparison.Ordinal);
		Assert.Contains("applyWorkspaceAvatar", apply, StringComparison.Ordinal);
		Assert.Contains("revokeAvatarObjectUrl(objectUrl)", apply, StringComparison.Ordinal);
		Assert.Contains("surfaceIsModified(avatarWrap)", card, StringComparison.Ordinal);
		Assert.Contains("/member-avatars?", client, StringComparison.Ordinal);
		Assert.Contains("const avatarSkip = skip > 0 ? skip : 0;", client, StringComparison.Ordinal);
		Assert.Contains("query.set(\"skip\", String(avatarSkip));", client, StringComparison.Ordinal);
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
			"..",
			"app",
			"src",
			folder,
			fileName));
	}
}
