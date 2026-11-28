// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Workspace;

/// <summary>
/// Durations for the first member-detail page of one workspace open. The member JSON does not include these values.
/// </summary>
/// <remarks>
/// A profile exists only for skip 0 and a valid run id. A later page does not start one.
/// Connection and read durations stay unset when that stage did not run.
/// Action total stops when the outcome is set. It ends when the result is prepared, not when the response bytes are sent.
/// </remarks>
internal sealed class WorkspaceMemberDetailProfile : IWorkspaceGatewayTiming
{
	private readonly Stopwatch _action = Stopwatch.StartNew();
	private Stopwatch? _responsePreparation;
	private AnchorProfileReadCapture? _profileRead;
	private bool _outcomeSet;
	private bool _logged;

	private WorkspaceMemberDetailProfile(string runId)
	{
		RunId = runId;
	}

	/// <summary>The run id from the workspace open.</summary>
	internal string RunId { get; }

	/// <summary>completed, unauthorized, unavailable, rejected, cancelled, failed, or incomplete.</summary>
	internal string Outcome { get; private set; } = "incomplete";

	/// <summary>Time spent opening the connection used to resolve the account. Null when that stage did not run.</summary>
	internal long? ResolveConnectionOpenMs { get; private set; }

	/// <summary>Time spent resolving the account. Null when that stage did not run.</summary>
	internal long? AccountResolutionMs { get; private set; }

	/// <summary>Time spent preparing the selected occurrence. Null when that stage did not run.</summary>
	internal long? PrepareOccurrenceMs { get; private set; }

	/// <summary>Time spent opening the connection used for the profile procedure. Null when that stage did not run.</summary>
	internal long? ProfileConnectionOpenMs { get; private set; }

	/// <summary>Time spent authorizing and reading profiles. Null when that stage did not run.</summary>
	internal long? ProfileAuthorizationMs { get; private set; }

	/// <summary>The profiled procedure read. Null when this request is not profiled.</summary>
	internal AnchorProfileReadCapture? ProfileRead => _profileRead;

	/// <summary>Time after the profile read until the member-detail result is ready. Null when that stage did not run.</summary>
	internal long? ResponsePreparationMs { get; private set; }

	/// <summary>Time from the start of this profile through the prepared outcome. Null until the outcome is set.</summary>
	internal long? ActionTotalMs { get; private set; }

	/// <summary>
	/// Starts a profile for the first page when the run id is valid. Any other skip, or an invalid id, does not start one.
	/// </summary>
	/// <param name="runId">The request header value.</param>
	/// <param name="skip">The accepted member skip. Only zero is profiled.</param>
	/// <returns>The profile, or null when this request is not the profiled first page.</returns>
	internal static WorkspaceMemberDetailProfile? ForInitialPage(string? runId, int skip)
	{
		if (skip != 0 || !Guid.TryParse(runId, out var parsed) || parsed == Guid.Empty)
		{
			return null;
		}

		return new WorkspaceMemberDetailProfile(parsed.ToString("D"));
	}

	/// <summary>Records the account-resolution connection. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	/// <inheritdoc />
	void IWorkspaceGatewayTiming.SetResolveConnectionOpen(long elapsedMilliseconds)
	{
		SetResolveConnectionOpen(elapsedMilliseconds);
	}

	/// <inheritdoc />
	void IWorkspaceGatewayTiming.SetAccountResolution(long elapsedMilliseconds)
	{
		SetAccountResolution(elapsedMilliseconds);
	}

	/// <inheritdoc />
	void IWorkspaceGatewayTiming.SetProfileConnectionOpen(long elapsedMilliseconds)
	{
		SetProfileConnectionOpen(elapsedMilliseconds);
	}

	/// <inheritdoc />
	void IWorkspaceGatewayTiming.SetProfileAuthorization(long elapsedMilliseconds)
	{
		SetProfileAuthorization(elapsedMilliseconds);
	}

	/// <inheritdoc />
	void IWorkspaceGatewayTiming.SetProfileRead(AnchorProfileReadCapture capture)
	{
		_profileRead = capture;
	}

	internal void SetResolveConnectionOpen(long elapsedMilliseconds)
	{
		ResolveConnectionOpenMs ??= elapsedMilliseconds;
	}

	/// <summary>Records account resolution. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	internal void SetAccountResolution(long elapsedMilliseconds)
	{
		AccountResolutionMs ??= elapsedMilliseconds;
	}

	/// <summary>Records occurrence preparation. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	internal void SetPrepareOccurrence(long elapsedMilliseconds)
	{
		PrepareOccurrenceMs ??= elapsedMilliseconds;
	}

	/// <summary>Records the profile-read connection. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	internal void SetProfileConnectionOpen(long elapsedMilliseconds)
	{
		ProfileConnectionOpenMs ??= elapsedMilliseconds;
	}

	/// <summary>Records profile authorization and read. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	internal void SetProfileAuthorization(long elapsedMilliseconds)
	{
		ProfileAuthorizationMs ??= elapsedMilliseconds;
	}

	/// <summary>Starts the clock for work after the profile read. A second start is ignored.</summary>
	internal void BeginResponsePreparation()
	{
		_responsePreparation ??= Stopwatch.StartNew();
	}

	/// <summary>Stops response preparation. A stage that never started stays unset.</summary>
	internal void EndResponsePreparation()
	{
		if (_responsePreparation == null || ResponsePreparationMs != null)
		{
			return;
		}

		_responsePreparation.Stop();
		ResponsePreparationMs = _responsePreparation.ElapsedMilliseconds;
	}

	/// <summary>Stops the action clock and keeps the first outcome.</summary>
	/// <param name="outcome">The prepared outcome.</param>
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

	/// <summary>The response header body. The same numbers are written to the profiling log.</summary>
	/// <returns>One JSON object with the run id, outcome, and server durations.</returns>
	internal string HeaderJson()
	{
		var text = "{\"runId\":\"" + RunId
			+ "\",\"outcome\":\"" + Outcome
			+ "\",\"phase\":\"members\",\"serverMs\":{"
			+ "\"actionTotal\":" + Number(ActionTotalMs)
			+ ",\"resolveConnectionOpen\":" + Number(ResolveConnectionOpenMs)
			+ ",\"accountResolution\":" + Number(AccountResolutionMs)
			+ ",\"prepareOccurrence\":" + Number(PrepareOccurrenceMs)
			+ ",\"profileConnectionOpen\":" + Number(ProfileConnectionOpenMs)
			+ ",\"profileAuthorization\":" + Number(ProfileAuthorizationMs)
			+ ",\"responsePreparation\":" + Number(ResponsePreparationMs)
			+ "}";
		if (_profileRead != null)
		{
			text = text + ",\"profileRead\":";
			var body = new System.Text.StringBuilder();
			_profileRead.AppendJson(body);
			text += body.ToString();
		}

		return text + "}";
	}

	/// <summary>Writes one profiling log. A second call does not write again.</summary>
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
			"WorkspaceMemberDetailProfile RunId={RunId} Outcome={Outcome} ResolveConnectionOpenMs={ResolveConnectionOpenMs} AccountResolutionMs={AccountResolutionMs} PrepareOccurrenceMs={PrepareOccurrenceMs} ProfileConnectionOpenMs={ProfileConnectionOpenMs} ProfileAuthorizationMs={ProfileAuthorizationMs} ResponsePreparationMs={ResponsePreparationMs} ActionTotalMs={ActionTotalMs} {ProfileRead}",
			RunId,
			Outcome,
			ResolveConnectionOpenMs,
			AccountResolutionMs,
			PrepareOccurrenceMs,
			ProfileConnectionOpenMs,
			ProfileAuthorizationMs,
			ResponsePreparationMs,
			ActionTotalMs,
			_profileRead?.LogText() ?? "ProfileCaller=null");
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
}
