// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PluralBridge.Api.Tests;

/// <summary>
/// The workspace-load console report is assembled by the production browser function.
/// These tests pass known measurements into that function. They do not open a database.
/// </summary>
public sealed class WorkspaceLoadReportTests
{
	/// <summary>
	/// One run keeps its id, orders browser marks by elapsed time, and leaves server durations under their request.
	/// Missing marks stay absent. A zero duration stays zero. The snapshot does not follow later edits.
	/// </summary>
	[Fact]
	public void Assemble_CorrelatesOrdersAndKeepsTheSnapshot()
	{
		var lines = RunReport(
			"""
			var profile = {
			  runId: "run-1",
			  treeStatus: "complete",
			  membersStatus: "complete",
			  avatarStatus: "complete",
			  cardCount: 2,
			  browserElapsedMs: { selectionDom: 40, responseReceived: 10, domComplete: 30 },
			  memberBrowserElapsedMs: { memberCardsDom: 70, memberRequestStart: 50 },
			  avatarBrowser: {
			    elapsedMs: { displayComplete: 120, avatarRequestStart: 80, avatarApplyComplete: 100 },
			    images: [
			      { memberId: "member-a", decodeMs: 4, settle: "loaded" },
			      { memberId: "member-b", decodeMs: 0, settle: "placeholder" }
			    ]
			  },
			  treeHeader: { outcome: "completed", serverMs: { actionTotal: 15, connectionOpen: 0, accountResolution: null } },
			  memberHeader: { outcome: "completed", serverMs: { actionTotal: 9, prepareOccurrence: 2 } },
			  avatarHeader: {
			    outcome: "completed",
			    serverMs: { actionTotal: 21, imageLoop: 11, base64: 1, responsePreparation: 3 },
			    members: [
			      { memberId: "member-a", retention: "hit", image: "bytes", name: "Ada", imageBase64: "aaaa" },
			      { memberId: "member-b", retention: "miss", image: "null", connectionOpen: 6, byteLength: 8, graphTables: null, name: "Bea" }
			    ]
			  }
			};
			var report = assembleWorkspaceLoadReport(profile);
			profile.browserElapsedMs.responseReceived = 999;
			profile.treeHeader.serverMs.actionTotal = 999;
			profile.avatarHeader.members[0].name = "changed";
			echo("run=" + report.runId);
			echo("label=" + report.label);
			echo("milestones=" + names(report.browserMilestones));
			echo("treeStatus=" + report.tree.status);
			echo("treeBrowser=" + report.tree.browserElapsedMs.responseReceived);
			echo("treeServer=" + report.tree.serverMs.actionTotal);
			echo("treeOpen=" + report.tree.serverMs.connectionOpen);
			echo("treeAccount=" + kind(report.tree.serverMs.accountResolution));
			echo("treeParsed=" + kind(report.tree.browserElapsedMs.jsonParsed));
			echo("memberStatus=" + report.members.status);
			echo("memberServer=" + report.members.serverMs.actionTotal);
			echo("cards=" + report.members.cardCount);
			echo("avatarStatus=" + report.avatars.status);
			echo("hits=" + report.avatars.retentionHits);
			echo("misses=" + report.avatars.retentionMisses);
			echo("nulls=" + report.avatars.nullImages);
			echo("decode=" + report.avatars.members[0].browserDecodeMs);
			echo("zeroDecode=" + report.avatars.members[1].browserDecodeMs);
			echo("image=" + report.avatars.members[1].image);
			echo("byteLength=" + report.avatars.members[1].byteLength);
			echo("graph=" + kind(report.avatars.members[1].graphTablesMs));
			echo("name=" + kind(report.avatars.members[0].name));
			echo("base64Field=" + kind(report.avatars.members[0].imageBase64));
			echo("base64Duration=" + report.avatars.serverMs.base64);
			echo("display=" + report.avatars.browserElapsedMs.displayComplete);
			""");

		Assert.Equal("run=run-1", lines[0]);
		Assert.Equal("label=workspace load", lines[1]);
		Assert.Equal("milestones=responseReceived,domComplete,selectionDom,requestStart,cardsDomComplete,requestStart,scriptComplete,displayComplete", lines[2]);
		Assert.Equal("treeStatus=complete", lines[3]);
		Assert.Equal("treeBrowser=10", lines[4]);
		Assert.Equal("treeServer=15", lines[5]);
		Assert.Equal("treeOpen=0", lines[6]);
		Assert.Equal("treeAccount=absent", lines[7]);
		Assert.Equal("treeParsed=absent", lines[8]);
		Assert.Equal("memberStatus=complete", lines[9]);
		Assert.Equal("memberServer=9", lines[10]);
		Assert.Equal("cards=2", lines[11]);
		Assert.Equal("avatarStatus=complete", lines[12]);
		Assert.Equal("hits=1", lines[13]);
		Assert.Equal("misses=1", lines[14]);
		Assert.Equal("nulls=1", lines[15]);
		Assert.Equal("decode=4", lines[16]);
		Assert.Equal("zeroDecode=0", lines[17]);
		Assert.Equal("image=null", lines[18]);
		Assert.Equal("byteLength=8", lines[19]);
		Assert.Equal("graph=absent", lines[20]);
		Assert.Equal("name=absent", lines[21]);
		Assert.Equal("base64Field=absent", lines[22]);
		Assert.Equal("base64Duration=1", lines[23]);
		Assert.Equal("display=120", lines[24]);
	}

	/// <summary>A failed member page and a cancelled avatar batch keep those statuses and omit measurements that never ran.</summary>
	[Fact]
	public void Assemble_ReportsFailureCancellationAndDisabled()
	{
		var lines = RunReport(
			"""
			var failed = assembleWorkspaceLoadReport({
			  runId: "run-failed",
			  treeStatus: "complete",
			  membersStatus: "failed",
			  browserElapsedMs: { responseReceived: 3 },
			  treeHeader: { outcome: "completed", serverMs: { actionTotal: 4 } }
			});
			var cancelled = assembleWorkspaceLoadReport({
			  runId: "run-cancelled",
			  treeStatus: "complete",
			  membersStatus: "complete",
			  avatarStatus: "cancelled",
			  cardCount: 1,
			  avatarBrowser: { elapsedMs: { avatarRequestStart: 6 }, images: [] },
			  avatarHeader: { outcome: "cancelled", serverMs: { actionTotal: null } }
			});
			var superseded = assembleWorkspaceLoadReport({
			  runId: "run-old",
			  treeStatus: "incomplete",
			  browserElapsedMs: { responseReceived: 2 },
			  treeHeader: { outcome: "completed", serverMs: { actionTotal: 8 } }
			});
			echo("disabled=" + (assembleWorkspaceLoadReport(null) === null ? "none" : "present"));
			echo("blank=" + (assembleWorkspaceLoadReport({ runId: "" }) === null ? "none" : "present"));
			echo("failedRun=" + failed.runId);
			echo("failedMembers=" + failed.members.status);
			echo("failedAvatars=" + kind(failed.avatars));
			echo("failedCards=" + kind(failed.members.cardCount));
			echo("cancelledAvatars=" + cancelled.avatars.status);
			echo("cancelledDisplay=" + kind(cancelled.avatars.browserElapsedMs.displayComplete));
			echo("cancelledServer=" + kind(cancelled.avatars.serverMs));
			echo("cancelledHits=" + kind(cancelled.avatars.retentionHits));
			echo("superseded=" + superseded.tree.status);
			echo("supersededServer=" + superseded.tree.serverMs.actionTotal);
			echo("same=" + (failed.runId === cancelled.runId ? "yes" : "no"));
			""");

		Assert.Equal("disabled=none", lines[0]);
		Assert.Equal("blank=none", lines[1]);
		Assert.Equal("failedRun=run-failed", lines[2]);
		Assert.Equal("failedMembers=failed", lines[3]);
		Assert.Equal("failedAvatars=absent", lines[4]);
		Assert.Equal("failedCards=absent", lines[5]);
		Assert.Equal("cancelledAvatars=cancelled", lines[6]);
		Assert.Equal("cancelledDisplay=absent", lines[7]);
		Assert.Equal("cancelledServer=absent", lines[8]);
		Assert.Equal("cancelledHits=absent", lines[9]);
		Assert.Equal("superseded=incomplete", lines[10]);
		Assert.Equal("supersededServer=8", lines[11]);
		Assert.Equal("same=no", lines[12]);
	}

	/// <summary>Runs the production report function and returns its printed lines.</summary>
	/// <param name="driver">Statements that call the production function and print the fields under test.</param>
	/// <returns>The printed lines.</returns>
	private static List<string> RunReport(string driver)
	{
		var navigator = File.ReadAllText(SourcePath("js", "navigator.js"));
		var functionText = FunctionBody(navigator, "function assembleWorkspaceLoadReport(profile)");
		var script = functionText + """


			function echo(value) {
			  WScript.Echo(String(value));
			}

			function names(entries) {
			  var text = "";
			  var index;
			  for (index = 0; index < entries.length; index++) {
			    if (index > 0) {
			      text += ",";
			    }
			    text += entries[index].name;
			  }
			  return text;
			}

			function kind(value) {
			  return value === undefined || value === null ? "absent" : "present";
			}

			""" + driver;
		var path = Path.Combine(Path.GetTempPath(), "pb-workspace-load-report.js");
		File.WriteAllText(path, script);
		var process = Process.Start(new ProcessStartInfo
		{
			FileName = Path.Combine(Environment.SystemDirectory, "cscript.exe"),
			Arguments = "//nologo \"" + path + "\"",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		});
		Assert.NotNull(process);
		var output = process.StandardOutput.ReadToEnd();
		var error = process.StandardError.ReadToEnd();
		process.WaitForExit();
		Assert.True(process.ExitCode == 0, error + output);
		return output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
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
}
