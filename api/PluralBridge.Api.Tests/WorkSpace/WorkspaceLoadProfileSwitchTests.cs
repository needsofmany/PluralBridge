// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PluralBridge.Api.Tests;

/// <summary>
/// The workspace-load profiling switch is on only when the setting is strictly true.
/// These checks run the production predicate. They do not open a database connection.
/// </summary>
public sealed class WorkspaceLoadProfileSwitchTests
{
	/// <summary>
	/// Missing tools, a missing setting, false, and non-boolean values stay off. Strict true is on.
	/// </summary>
	[Fact]
	public void Predicate_IsEnabledOnlyForStrictTrue()
	{
		var script = File.ReadAllText(SourcePath());
		var predicate = FunctionBody(script, "function workspaceLoadProfileEnabled(tools)");
		Assert.Contains("source.workspaceLoadProfile === true", predicate, StringComparison.Ordinal);
		Assert.DoesNotContain("== true", predicate.Replace("=== true", "", StringComparison.Ordinal), StringComparison.Ordinal);

		var results = RunPredicate(predicate);
		Assert.Equal("false", results["missingTools"]);
		Assert.Equal("false", results["missingSetting"]);
		Assert.Equal("false", results["falseValue"]);
		Assert.Equal("true", results["trueValue"]);
		Assert.Equal("false", results["stringTrue"]);
		Assert.Equal("false", results["one"]);
		Assert.Equal("false", results["nullValue"]);
	}

	/// <summary>
	/// The initializer writes false only when the profiling property is absent and does not replace the other flags.
	/// </summary>
	[Fact]
	public void Initializer_LeavesProfilingOffWithoutChangingOtherFlags()
	{
		var script = File.ReadAllText(SourcePath());
		var ensure = FunctionBody(script, "function ensureWorkspaceLoadProfileDisabledByDefault(tools)");
		Assert.Contains("hasOwnProperty.call(tools, \"workspaceLoadProfile\")", ensure, StringComparison.Ordinal);
		Assert.Contains("tools.workspaceLoadProfile = false", ensure, StringComparison.Ordinal);
		Assert.DoesNotContain("databaseWritesDisabled", ensure, StringComparison.Ordinal);
		Assert.DoesNotContain("workspaceTreeEnabled", ensure, StringComparison.Ordinal);
		Assert.Contains("ensureDatabaseWritesEnabledByDefault(window.PluralBridge.developerTools);", script, StringComparison.Ordinal);
		Assert.Contains("ensureWorkspaceTreeDisabledByDefault(window.PluralBridge.developerTools);", script, StringComparison.Ordinal);
		Assert.Contains("ensureWorkspaceLoadProfileDisabledByDefault(window.PluralBridge.developerTools);", script, StringComparison.Ordinal);
	}

	/// <summary>Runs the extracted production predicate for the required setting values.</summary>
	/// <param name="predicate">The production function text.</param>
	/// <returns>The named results printed by the script host.</returns>
	private static Dictionary<string, string> RunPredicate(string predicate)
	{
		var host = predicate.Replace("const source", "var source", StringComparison.Ordinal);
		var program = """
			var window = { PluralBridge: {} };
			""" + host + """

			function report(name, tools) {
			    WScript.Echo(name + "=" + (workspaceLoadProfileEnabled(tools) ? "true" : "false"));
			}

			report("missingTools", null);
			report("missingSetting", {});
			report("falseValue", { workspaceLoadProfile: false });
			report("trueValue", { workspaceLoadProfile: true });
			report("stringTrue", { workspaceLoadProfile: "true" });
			report("one", { workspaceLoadProfile: 1 });
			report("nullValue", { workspaceLoadProfile: null });
			""";
		var path = Path.Combine(Path.GetTempPath(), "pb-workspace-load-profile-" + Guid.NewGuid().ToString("N") + ".js");
		File.WriteAllText(path, program);
		try
		{
			using var process = Process.Start(new ProcessStartInfo
			{
				FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cscript.exe"),
				Arguments = "//Nologo \"" + path + "\"",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			});
			Assert.NotNull(process);
			var output = process.StandardOutput.ReadToEnd();
			var error = process.StandardError.ReadToEnd();
			process.WaitForExit();
			Assert.True(process.ExitCode == 0, error + output);
			return output
				.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(line => line.Split('=', 2))
				.ToDictionary(part => part[0], part => part[1], StringComparer.Ordinal);
		}
		finally
		{
			File.Delete(path);
		}
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

	/// <summary>Finds the developer-tools source from this test file.</summary>
	/// <param name="sourceFile">The test file path supplied by the compiler.</param>
	/// <returns>The absolute source path.</returns>
	private static string SourcePath([CallerFilePath] string sourceFile = "")
	{
		return Path.GetFullPath(Path.Combine(
			Path.GetDirectoryName(sourceFile) ?? string.Empty,
			"..",
			"..",
			"..",
			"app",
			"src",
			"js",
			"developer-tools.js"));
	}
}
