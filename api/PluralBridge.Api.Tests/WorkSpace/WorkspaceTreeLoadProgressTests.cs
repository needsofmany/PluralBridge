// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;

namespace PluralBridge.Api.Tests;

/// <summary>
/// The workspace loading line owns the indeterminate bar.
/// These tests read the navigator source. They do not start the application or open a database connection.
/// </summary>
public sealed class WorkspaceTreeLoadProgressTests
{
	/// <summary>
	/// Only the workspace loading status shows the bar. Success and failure replace that status.
	/// An aborted or stale load returns before it can change the tree.
	/// </summary>
	[Fact]
	public void WorkspaceLoad_ShowsTheProgressBarUntilThatRequestFinishes()
	{
		var script = File.ReadAllText(SourcePath("js", "navigator.js"));
		var style = File.ReadAllText(SourcePath("css", "navigator.css"));
		var load = FunctionBody(script, "function startLoad(replacing)");
		var status = FunctionBody(script, "function renderStatus(text, isError, showProgress)");
		var catchStart = load.IndexOf(".catch(function (error)", StringComparison.Ordinal);
		var catchBody = load[catchStart..];
		var abortReturn = catchBody.IndexOf("error.name === \"AbortError\"", StringComparison.Ordinal);
		var staleReturn = catchBody.IndexOf("if (!loadIsCurrent(request))", StringComparison.Ordinal);
		var failureStatus = catchBody.IndexOf("renderStatus(request.workspaceMode", StringComparison.Ordinal);
		var successReturn = load.IndexOf("if (!loadIsCurrent(request))", StringComparison.Ordinal);
		var successPaint = load.IndexOf("renderTree();", StringComparison.Ordinal);

		Assert.Contains("request.workspaceMode ? \"Loading workspace tree.\" : \"Loading plural map.\"", load, StringComparison.Ordinal);
		Assert.Contains("request.workspaceMode === true", load, StringComparison.Ordinal);
		Assert.Contains("progress.className = \"navigator-load-progress\"", status, StringComparison.Ordinal);
		Assert.Contains("tree.replaceChildren();", status, StringComparison.Ordinal);
		Assert.True(abortReturn >= 0 && abortReturn < failureStatus);
		Assert.True(staleReturn >= 0 && staleReturn < failureStatus);
		Assert.True(successReturn >= 0 && successReturn < successPaint);
		Assert.Contains("animation: navigator-load-progress 1s ease-in-out infinite;", style, StringComparison.Ordinal);
		Assert.Contains("transform: translateX(-120%);", style, StringComparison.Ordinal);
		Assert.Contains("transform: translateX(280%);", style, StringComparison.Ordinal);
		Assert.Contains("background: #8eb6ff;", style, StringComparison.Ordinal);
		Assert.Contains("background: #2041b8;", style, StringComparison.Ordinal);
		Assert.Contains("animation: none;", style, StringComparison.Ordinal);
	}

	/// <summary>
	/// The initial member-profile load puts the bar under the loading line.
	/// A later page puts that same bar after the cards and removes only its own element.
	/// A stale response removes that element before it can paint or report an error.
	/// </summary>
	[Fact]
	public void MemberProfileLoad_OwnsItsProgressBar()
	{
		var script = File.ReadAllText(SourcePath("js", "navigator.js"));
		var initial = FunctionBody(script, "function loadWorkspaceMemberDetails(row, profileId, profileRequest)");
		var fallback = FunctionBody(script, "function loadAnchorProfiles(row)");
		var next = FunctionBody(script, "function requestNextWorkspaceMemberDetails()");
		var page = FunctionBody(script, "function startMemberDetailRequest(row, skip, request, signal, progress)");
		var paged = FunctionBody(script, "function showPagedMemberProgress()");
		var paint = FunctionBody(script, "function paintWorkspaceMemberPage(row, profiles, skip)");
		var thenStart = page.IndexOf(".then(function (profiles)", StringComparison.Ordinal);
		var thenBody = page[thenStart..page.IndexOf(".catch(function (error)", StringComparison.Ordinal)];
		var catchBody = page[page.IndexOf(".catch(function (error)", StringComparison.Ordinal)..];
		var thenRemove = thenBody.IndexOf("removeMemberLoadProgress(progress);", StringComparison.Ordinal);
		var thenStale = thenBody.IndexOf("serial !== memberDetailSerial", StringComparison.Ordinal);
		var catchRemove = catchBody.IndexOf("removeMemberLoadProgress(progress);", StringComparison.Ordinal);
		var catchStale = catchBody.IndexOf("serial !== memberDetailSerial", StringComparison.Ordinal);
		var catchError = catchBody.IndexOf("showWorkspaceDetailError(", StringComparison.Ordinal);
		var reuseList = paint.IndexOf("if (!section || !list)", StringComparison.Ordinal);
		var replacePane = paint.IndexOf("detail.replaceChildren();", StringComparison.Ordinal);

		Assert.Contains("renderDetailStatus(row, \"Loading member profiles.\", false, true)", initial, StringComparison.Ordinal);
		Assert.Contains("renderDetailStatus(row, \"Loading member profiles.\", false, true)", fallback, StringComparison.Ordinal);
		Assert.Contains("showPagedMemberProgress()", next, StringComparison.Ordinal);
		Assert.Contains("list.after(progress);", paged, StringComparison.Ordinal);
		Assert.DoesNotContain("querySelector(\".navigator-load-progress\").remove", page, StringComparison.Ordinal);
		Assert.DoesNotContain("scrollTop", page, StringComparison.Ordinal);
		Assert.True(thenRemove >= 0 && thenRemove < thenStale);
		Assert.True(catchRemove >= 0 && catchRemove < catchStale && catchStale < catchError);
		Assert.True(replacePane > reuseList);
	}

	/// <summary>Reads one function, starting at its declaration, through the matching closing brace.</summary>
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

	/// <summary>Finds a browser source file from this test file. The output folder may sit outside the repository.</summary>
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
