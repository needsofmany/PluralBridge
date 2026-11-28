// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace PluralBridge.Api.Workspace;

/// <summary>
/// Durations for one profiled workspace-tree load. The tree JSON does not include these values.
/// </summary>
/// <remarks>
/// A profile exists only for a valid run id. Connection, account, and opening-build durations stay unset when that stage did not run.
/// Action total stops when the outcome is set. It ends when the result is prepared, not when the response bytes are sent.
/// </remarks>
internal sealed class WorkspaceTreeLoadProfile
{
	/// <summary>The request header that carries the run id.</summary>
	internal const string RequestHeaderName = "X-PB-Workspace-Profile-Id";

	/// <summary>The response header that carries the correlated durations. It is not part of the tree JSON.</summary>
	internal const string ResponseHeaderName = "X-PB-Workspace-Profile";

	private readonly Stopwatch _action = Stopwatch.StartNew();
	private readonly List<OpeningCommand> _commands = [];
	private bool _outcomeSet;
	private bool _logged;

	private WorkspaceTreeLoadProfile(string runId)
	{
		RunId = runId;
	}

	/// <summary>The run id from the request.</summary>
	internal string RunId { get; }

	/// <summary>completed, unauthorized, cancelled, failed, or incomplete.</summary>
	internal string Outcome { get; private set; } = "incomplete";

	/// <summary>Time spent opening the database connection. Null when that stage did not run.</summary>
	internal long? ConnectionOpenMs { get; private set; }

	/// <summary>Time spent resolving the account. Null when that stage did not run.</summary>
	internal long? AccountResolutionMs { get; private set; }

	/// <summary>Time spent running the opening plan after the account was resolved. Null when that stage did not run.</summary>
	internal long? OpeningBuildMs { get; private set; }

	/// <summary>One elapsed time for each opening command. Empty when this request is not profiled.</summary>
	internal IReadOnlyList<OpeningCommand> OpeningCommands => _commands;

	/// <summary>Time spent constructing the context. Null when construction did not run.</summary>
	internal long? CreateContextMs { get; private set; }

	/// <summary>Time spent publishing the context. Null when publication did not run.</summary>
	internal long? PublishMs { get; private set; }

	/// <summary>Time from the start of this profile through the prepared outcome. Null until the outcome is set.</summary>
	internal long? ActionTotalMs { get; private set; }

	/// <summary>
	/// Starts a profile for a valid run id. Missing, blank, empty, and non-guid values do not start one.
	/// </summary>
	/// <param name="runId">The request header value.</param>
	/// <returns>The profile, or null when the id is not valid.</returns>
	internal static WorkspaceTreeLoadProfile? TryStart(string? runId)
	{
		if (!Guid.TryParse(runId, out var parsed) || parsed == Guid.Empty)
		{
			return null;
		}

		return new WorkspaceTreeLoadProfile(parsed.ToString("D"));
	}

	/// <summary>Records the connection-open duration. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	internal void SetConnectionOpen(long elapsedMilliseconds)
	{
		ConnectionOpenMs ??= elapsedMilliseconds;
	}

	/// <summary>Records the account-resolution duration. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	internal void SetAccountResolution(long elapsedMilliseconds)
	{
		AccountResolutionMs ??= elapsedMilliseconds;
	}

	/// <summary>Records the opening-build duration. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	internal void SetOpeningBuild(long elapsedMilliseconds)
	{
		OpeningBuildMs ??= elapsedMilliseconds;
	}

	/// <summary>Records one command inside the opening build. A disabled profile never calls this.</summary>
	/// <param name="name">The command name.</param>
	/// <param name="elapsedMilliseconds">The client elapsed time for that command.</param>
	internal void AddOpeningCommand(string name, long elapsedMilliseconds)
	{
		_commands.Add(new OpeningCommand(name, elapsedMilliseconds));
	}

	/// <summary>Records context construction. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	internal void SetCreateContext(long elapsedMilliseconds)
	{
		CreateContextMs ??= elapsedMilliseconds;
	}

	/// <summary>Records context publication. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	internal void SetPublish(long elapsedMilliseconds)
	{
		PublishMs ??= elapsedMilliseconds;
	}

	/// <summary>
	/// Stops the action clock and keeps the first outcome.
	/// </summary>
	/// <param name="outcome">completed, unauthorized, cancelled, failed, or incomplete.</param>
	internal void SetOutcome(string outcome)
	{
		if (_outcomeSet)
		{
			return;
		}

		_outcomeSet = true;
		_action.Stop();
		Outcome = outcome;
		ActionTotalMs = _action.ElapsedMilliseconds;
	}

	/// <summary>
	/// The response header body. The same numbers are written to the profiling log.
	/// </summary>
	/// <returns>One JSON object with the run id, outcome, and server durations.</returns>
	internal string HeaderJson()
	{
		var text = "{\"runId\":\"" + RunId
			+ "\",\"outcome\":\"" + Outcome
			+ "\",\"serverMs\":{"
			+ "\"actionTotal\":" + Number(ActionTotalMs)
			+ ",\"connectionOpen\":" + Number(ConnectionOpenMs)
			+ ",\"accountResolution\":" + Number(AccountResolutionMs)
			+ ",\"openingBuild\":" + Number(OpeningBuildMs)
			+ ",\"createContext\":" + Number(CreateContextMs)
			+ ",\"publish\":" + Number(PublishMs)
			+ "}";
		if (_commands.Count > 0)
		{
			text = text + ",\"openingCommands\":[";
			for (var index = 0; index < _commands.Count; index++)
			{
				if (index > 0)
				{
					text += ",";
				}

				text += "{\"name\":\"" + _commands[index].Name + "\",\"ms\":" + _commands[index].Milliseconds.ToString(CultureInfo.InvariantCulture) + "}";
			}

			text += "]";
		}

		return text + "}";
	}

	/// <summary>
	/// Writes one profiling log. A second call does not write again. A null profile is not logged by the caller.
	/// </summary>
	/// <param name="logger">The request logger. The message contains the run id and the same durations as <see cref="HeaderJson"/>.</param>
	internal void Log(ILogger logger)
	{
		if (_logged)
		{
			return;
		}

		_logged = true;
		if (!_outcomeSet)
		{
			SetOutcome("incomplete");
		}

		logger.LogInformation(
			"WorkspaceTreeProfile RunId={RunId} Outcome={Outcome} ConnectionOpenMs={ConnectionOpenMs} AccountResolutionMs={AccountResolutionMs} OpeningBuildMs={OpeningBuildMs} OpeningCommands={OpeningCommands} CreateContextMs={CreateContextMs} PublishMs={PublishMs} ActionTotalMs={ActionTotalMs}",
			RunId,
			Outcome,
			ConnectionOpenMs,
			AccountResolutionMs,
			OpeningBuildMs,
			string.Join(",", _commands.Select(command => command.Name + ":" + command.Milliseconds.ToString(CultureInfo.InvariantCulture))),
			CreateContextMs,
			PublishMs,
			ActionTotalMs);
	}

	/// <summary>Formats one duration for the header. An unset stage is null.</summary>
	/// <param name="value">The measured milliseconds.</param>
	/// <returns>The invariant number, or null.</returns>
	private static string Number(long? value)
	{
		return value.HasValue
			? value.Value.ToString(CultureInfo.InvariantCulture)
			: "null";
	}

	/// <summary>One timed command inside the opening build.</summary>
	/// <param name="Name">The command name.</param>
	/// <param name="Milliseconds">The client elapsed time.</param>
	internal readonly record struct OpeningCommand(string Name, long Milliseconds);
}
