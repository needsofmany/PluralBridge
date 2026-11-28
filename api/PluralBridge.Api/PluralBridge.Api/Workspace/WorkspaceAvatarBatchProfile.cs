// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Workspace;

/// <summary>
/// Durations for the first avatar batch of one workspace open. The avatar JSON does not include these values.
/// </summary>
/// <remarks>
/// A profile exists only for skip 0 and a valid run id. A later page does not start one.
/// Connection, authorization, and image-read durations stay unset when that stage did not run.
/// Action total stops when the outcome is set. It ends when the result is prepared, not when the response bytes are sent.
/// One member line is kept for each authorized page member. Retention misses share one image query.
/// That query is recorded once on this profile. A graph built only for the image read is recorded once when that read builds one.
/// The profile procedure's graph stays on profile authorization. Shared stages are not copied onto each member.
/// A single-member read still records its own stages on that member.
/// </remarks>
internal sealed class WorkspaceAvatarBatchProfile : IWorkspaceGatewayTiming
{
	private readonly Stopwatch _action = Stopwatch.StartNew();
	private readonly List<Member> _members = [];
	private AnchorProfileReadCapture? _profileRead;
	private bool _outcomeSet;
	private bool _logged;
	private bool _sharedReadRecorded;

	private WorkspaceAvatarBatchProfile(string runId)
	{
		RunId = runId;
	}

	/// <summary>The run id from the workspace open.</summary>
	internal string RunId { get; }

	/// <summary>completed, unauthorized, unavailable, rejected, cancelled, failed, or incomplete.</summary>
	internal string Outcome { get; private set; } = "incomplete";

	/// <inheritdoc />
	public long? ResolveConnectionOpenMs { get; private set; }

	/// <inheritdoc />
	public long? AccountResolutionMs { get; private set; }

	/// <inheritdoc />
	public long? ProfileConnectionOpenMs { get; private set; }

	/// <inheritdoc />
	public long? ProfileAuthorizationMs { get; private set; }

	/// <summary>Time spent walking the authorized page and loading its retention misses. Null when the loop did not run.</summary>
	internal long? ImageLoopMs { get; private set; }

	/// <summary>The one image-read connection for the miss list. Null when that read did not run.</summary>
	internal long? ImageConnectionOpenMs { get; private set; }

	/// <summary>The graph-table stage of the one miss read. Null when that read did not run.</summary>
	internal long? SharedGraphTablesMs { get; private set; }

	/// <summary>The topology-seed stage of the one miss read. Null when that read did not run.</summary>
	internal long? SharedTopologySeedMs { get; private set; }

	/// <summary>The topology-walker stage of the one miss read. Null when that read did not run.</summary>
	internal long? SharedTopologyWalkerMs { get; private set; }

	/// <summary>The graph-expansion stage of the one miss read. Null when that read did not run.</summary>
	internal long? SharedGraphExpansionMs { get; private set; }

	/// <summary>The image-query stage of the one miss read. Null when that read did not run.</summary>
	internal long? SharedAvatarQueryMs { get; private set; }

	/// <summary>Time spent creating the empty graph-copy table. Null when that command did not run.</summary>
	internal long? GraphCopyTableMs { get; private set; }

	/// <summary>Time spent classifying retained images and misses. Null when that scan did not run.</summary>
	internal long? MissScanMs { get; private set; }

	/// <summary>Time spent retaining loaded images. Null when no image was retained.</summary>
	internal long? RetainMs { get; private set; }

	/// <summary>The profiled procedure read. Null when this request is not profiled.</summary>
	internal AnchorProfileReadCapture? ProfileRead => _profileRead;

	/// <summary>Time spent converting image bytes to base64. Null when no conversion ran.</summary>
	internal long? Base64Ms { get; private set; }

	/// <summary>Time spent preparing the response after the image loop. Null when that stage did not run.</summary>
	internal long? ResponsePreparationMs { get; private set; }

	/// <summary>Time from the start of this profile through the prepared outcome. Null until the outcome is set.</summary>
	internal long? ActionTotalMs { get; private set; }

	/// <summary>One line per authorized page member, in the order they were recorded.</summary>
	internal IReadOnlyList<Member> Members => _members;

	/// <summary>
	/// Starts a profile for the first page when the run id is valid. Any other skip, or an invalid id, does not start one.
	/// </summary>
	/// <param name="runId">The request header value.</param>
	/// <param name="skip">The accepted avatar skip. Only zero is profiled.</param>
	/// <returns>The profile, or null when this request is not the profiled first page.</returns>
	internal static WorkspaceAvatarBatchProfile? ForInitialPage(string? runId, int skip)
	{
		if (skip != 0 || !Guid.TryParse(runId, out var parsed) || parsed == Guid.Empty)
		{
			return null;
		}

		return new WorkspaceAvatarBatchProfile(parsed.ToString("D"));
	}

	/// <inheritdoc />
	public void SetResolveConnectionOpen(long elapsedMilliseconds)
	{
		ResolveConnectionOpenMs ??= elapsedMilliseconds;
	}

	/// <inheritdoc />
	public void SetAccountResolution(long elapsedMilliseconds)
	{
		AccountResolutionMs ??= elapsedMilliseconds;
	}

	/// <inheritdoc />
	public void SetProfileConnectionOpen(long elapsedMilliseconds)
	{
		ProfileConnectionOpenMs ??= elapsedMilliseconds;
	}

	/// <inheritdoc />
	public void SetProfileAuthorization(long elapsedMilliseconds)
	{
		ProfileAuthorizationMs ??= elapsedMilliseconds;
	}

	/// <inheritdoc />
	public void SetProfileRead(AnchorProfileReadCapture capture)
	{
		_profileRead = capture;
	}

	/// <inheritdoc />
	public void SetGraphCopyTable(long elapsedMilliseconds)
	{
		GraphCopyTableMs ??= elapsedMilliseconds;
	}

	/// <summary>Records the miss scan. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	internal void SetMissScan(long elapsedMilliseconds)
	{
		MissScanMs ??= elapsedMilliseconds;
	}

	/// <summary>Records image retention. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	internal void SetRetain(long elapsedMilliseconds)
	{
		RetainMs ??= elapsedMilliseconds;
	}

	/// <summary>Records the image-loop duration. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	internal void SetImageLoop(long elapsedMilliseconds)
	{
		ImageLoopMs ??= elapsedMilliseconds;
	}

	/// <summary>Adds one base64 conversion to the conversion total.</summary>
	/// <param name="elapsedMilliseconds">The measured conversion.</param>
	internal void AddBase64(long elapsedMilliseconds)
	{
		Base64Ms = (Base64Ms ?? 0) + elapsedMilliseconds;
	}

	/// <summary>Records response preparation. A later write does not replace it.</summary>
	/// <param name="elapsedMilliseconds">The measured duration.</param>
	internal void SetResponsePreparation(long elapsedMilliseconds)
	{
		ResponsePreparationMs ??= elapsedMilliseconds;
	}

	/// <summary>Records a retained image. No image read is added.</summary>
	/// <param name="memberId">The authorized member.</param>
	internal void RecordHit(Guid memberId)
	{
		_members.Add(new Member(memberId, "hit", "bytes"));
	}

	/// <summary>Starts a miss before its image read. The read timings are added later.</summary>
	/// <param name="memberId">The authorized member.</param>
	internal void BeginMiss(Guid memberId)
	{
		_members.Add(new Member(memberId, "miss", null));
	}

	/// <summary>
	/// Copies the connection time and the avatar-read stages onto the open miss.
	/// A null timing leaves the stages unset. This does not change the image bytes.
	/// </summary>
	/// <param name="memberId">The member whose read finished.</param>
	/// <param name="connectionOpenMilliseconds">The connection open for that read. Null when it was not measured.</param>
	/// <param name="timing">The stages produced by the avatar reader. Null when the reader did not time the read.</param>
	internal void RecordRead(Guid memberId, long? connectionOpenMilliseconds, AnchorMemberProfileSave.AvatarReadTiming? timing)
	{
		var member = Find(memberId);
		if (member == null)
		{
			return;
		}

		member.ConnectionOpenMs = connectionOpenMilliseconds;
		if (timing == null)
		{
			return;
		}

		member.GraphTablesMs = timing.GraphTablesMilliseconds;
		member.TopologySeedMs = timing.TopologySeedMilliseconds;
		member.TopologyWalkerMs = timing.TopologyWalkerMilliseconds;
		member.GraphExpansionMs = timing.GraphExpansionMilliseconds;
		member.AvatarQueryMs = timing.AvatarQueryMilliseconds;
		member.ByteLength = timing.ByteLength;
	}

	/// <summary>
	/// Records the connection and graph stages of the one miss read. A second call does not replace them.
	/// The stages stay on this profile. They are not copied onto the member lines.
	/// </summary>
	/// <param name="connectionOpenMilliseconds">The connection open for the batch read. Null when it was not measured.</param>
	/// <param name="timing">The stages produced by the batch reader. Null when the reader did not time the read.</param>
	internal void RecordSharedRead(long? connectionOpenMilliseconds, AnchorMemberProfileSave.AvatarReadTiming? timing)
	{
		if (_sharedReadRecorded)
		{
			return;
		}

		_sharedReadRecorded = true;
		ImageConnectionOpenMs = connectionOpenMilliseconds;
		if (timing == null)
		{
			return;
		}

		SharedGraphTablesMs = timing.GraphTablesMilliseconds;
		SharedTopologySeedMs = timing.TopologySeedMilliseconds;
		SharedTopologyWalkerMs = timing.TopologyWalkerMilliseconds;
		SharedGraphExpansionMs = timing.GraphExpansionMilliseconds;
		SharedAvatarQueryMs = timing.AvatarQueryMilliseconds;
	}

	/// <summary>Records whether the miss returned bytes or no image.</summary>
	/// <param name="memberId">The member whose read finished.</param>
	/// <param name="byteLength">The returned byte length. Null when the member has no image.</param>
	internal void CompleteMiss(Guid memberId, int? byteLength)
	{
		var member = Find(memberId);
		if (member == null)
		{
			return;
		}

		member.Image = byteLength == null ? "null" : "bytes";
		member.ByteLength ??= byteLength;
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
		var text = new StringBuilder();
		text.Append("{\"runId\":\"").Append(RunId)
			.Append("\",\"outcome\":\"").Append(Outcome)
			.Append("\",\"phase\":\"avatars\",\"serverMs\":{")
			.Append("\"actionTotal\":").Append(Number(ActionTotalMs))
			.Append(",\"resolveConnectionOpen\":").Append(Number(ResolveConnectionOpenMs))
			.Append(",\"accountResolution\":").Append(Number(AccountResolutionMs))
			.Append(",\"profileConnectionOpen\":").Append(Number(ProfileConnectionOpenMs))
			.Append(",\"profileAuthorization\":").Append(Number(ProfileAuthorizationMs))
			.Append(",\"imageLoop\":").Append(Number(ImageLoopMs))
			.Append(",\"imageConnectionOpen\":").Append(Number(ImageConnectionOpenMs))
			.Append(",\"graphTables\":").Append(Number(SharedGraphTablesMs))
			.Append(",\"topologySeed\":").Append(Number(SharedTopologySeedMs))
			.Append(",\"topologyWalker\":").Append(Number(SharedTopologyWalkerMs))
			.Append(",\"graphExpansion\":").Append(Number(SharedGraphExpansionMs))
			.Append(",\"avatarQuery\":").Append(Number(SharedAvatarQueryMs))
			.Append(",\"graphCopyTable\":").Append(Number(GraphCopyTableMs))
			.Append(",\"missScan\":").Append(Number(MissScanMs))
			.Append(",\"retain\":").Append(Number(RetainMs))
			.Append(",\"base64\":").Append(Number(Base64Ms))
			.Append(",\"responsePreparation\":").Append(Number(ResponsePreparationMs))
			.Append('}');
		if (_profileRead != null)
		{
			text.Append(",\"profileRead\":");
			_profileRead.AppendJson(text);
		}

		text.Append(",\"members\":[");
		for (var index = 0; index < _members.Count; index++)
		{
			if (index > 0)
			{
				text.Append(',');
			}

			var member = _members[index];
			text.Append("{\"memberId\":\"").Append(member.MemberId.ToString("D"))
				.Append("\",\"retention\":\"").Append(member.Retention)
				.Append("\",\"image\":").Append(member.Image == null ? "null" : "\"" + member.Image + "\"")
				.Append(",\"connectionOpen\":").Append(Number(member.ConnectionOpenMs))
				.Append(",\"graphTables\":").Append(Number(member.GraphTablesMs))
				.Append(",\"topologySeed\":").Append(Number(member.TopologySeedMs))
				.Append(",\"topologyWalker\":").Append(Number(member.TopologyWalkerMs))
				.Append(",\"graphExpansion\":").Append(Number(member.GraphExpansionMs))
				.Append(",\"avatarQuery\":").Append(Number(member.AvatarQueryMs))
				.Append(",\"byteLength\":").Append(Number(member.ByteLength is int length ? length : null))
				.Append('}');
		}

		text.Append("]}");
		return text.ToString();
	}

	/// <summary>
	/// Writes the profiling header and log. A failure is swallowed so the avatar response is unchanged.
	/// </summary>
	/// <param name="headers">The response headers. The profiling header is separate from the avatar JSON.</param>
	/// <param name="logger">The request logger.</param>
	/// <returns>True when the header and log were written.</returns>
	internal bool TryWrite(Microsoft.AspNetCore.Http.IHeaderDictionary headers, ILogger logger)
	{
		try
		{
			headers[WorkspaceTreeLoadProfile.ResponseHeaderName] = HeaderJson();
			Log(logger);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
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
			"WorkspaceAvatarBatchProfile RunId={RunId} Outcome={Outcome} ResolveConnectionOpenMs={ResolveConnectionOpenMs} AccountResolutionMs={AccountResolutionMs} ProfileConnectionOpenMs={ProfileConnectionOpenMs} ProfileAuthorizationMs={ProfileAuthorizationMs} ImageLoopMs={ImageLoopMs} ImageConnectionOpenMs={ImageConnectionOpenMs} GraphTablesMs={GraphTablesMs} TopologySeedMs={TopologySeedMs} TopologyWalkerMs={TopologyWalkerMs} GraphExpansionMs={GraphExpansionMs} AvatarQueryMs={AvatarQueryMs} GraphCopyTableMs={GraphCopyTableMs} MissScanMs={MissScanMs} RetainMs={RetainMs} {ProfileRead} Base64Ms={Base64Ms} ResponsePreparationMs={ResponsePreparationMs} ActionTotalMs={ActionTotalMs} Members={Members}",
			RunId,
			Outcome,
			ResolveConnectionOpenMs,
			AccountResolutionMs,
			ProfileConnectionOpenMs,
			ProfileAuthorizationMs,
			ImageLoopMs,
			ImageConnectionOpenMs,
			SharedGraphTablesMs,
			SharedTopologySeedMs,
			SharedTopologyWalkerMs,
			SharedGraphExpansionMs,
			SharedAvatarQueryMs,
			GraphCopyTableMs,
			MissScanMs,
			RetainMs,
			_profileRead?.LogText() ?? "ProfileCaller=null",
			Base64Ms,
			ResponsePreparationMs,
			ActionTotalMs,
			_members.Count);
	}

	/// <summary>Finds the recorded line for one member.</summary>
	/// <param name="memberId">The member id.</param>
	/// <returns>The line, or null when that member was not recorded.</returns>
	private Member? Find(Guid memberId)
	{
		for (var index = _members.Count - 1; index >= 0; index--)
		{
			if (_members[index].MemberId == memberId)
			{
				return _members[index];
			}
		}

		return null;
	}

	/// <summary>Formats one duration for the header. An unset stage is null.</summary>
	/// <param name="value">The measured milliseconds or byte length.</param>
	/// <returns>The invariant number, or null.</returns>
	private static string Number(long? value)
	{
		return value.HasValue
			? value.Value.ToString(CultureInfo.InvariantCulture)
			: "null";
	}

	/// <summary>One authorized member in the profiled batch.</summary>
	internal sealed class Member
	{
		/// <summary>Creates a line before its image outcome is known.</summary>
		/// <param name="memberId">The member id.</param>
		/// <param name="retention">hit or miss.</param>
		/// <param name="image">bytes, null, or null when the miss has not finished.</param>
		internal Member(Guid memberId, string retention, string? image)
		{
			MemberId = memberId;
			Retention = retention;
			Image = image;
		}

		/// <summary>The member id.</summary>
		internal Guid MemberId { get; }

		/// <summary>hit when the context already held the image. miss when it was read.</summary>
		internal string Retention { get; }

		/// <summary>bytes when an image was returned. null when the member has no image. Unset while a miss is still reading.</summary>
		internal string? Image { get; set; }

		/// <summary>The image-read connection. Null for a hit or an unmeasured miss.</summary>
		internal long? ConnectionOpenMs { get; set; }

		/// <summary>The graph-table stage from <see cref="AnchorMemberProfileSave.AvatarReadTiming"/>. Null when that stage did not run.</summary>
		internal long? GraphTablesMs { get; set; }

		/// <summary>The topology-seed stage. Null when that stage did not run.</summary>
		internal long? TopologySeedMs { get; set; }

		/// <summary>The topology-walker stage. Null when that stage did not run.</summary>
		internal long? TopologyWalkerMs { get; set; }

		/// <summary>The graph-expansion stage. Null when that stage did not run.</summary>
		internal long? GraphExpansionMs { get; set; }

		/// <summary>The avatar-query stage. Null when that stage did not run.</summary>
		internal long? AvatarQueryMs { get; set; }

		/// <summary>The image length. Null when the member has no image or the length was not recorded.</summary>
		internal int? ByteLength { get; set; }
	}
}
