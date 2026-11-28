// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// One profiled call to the anchor profile procedure.
/// </summary>
/// <remarks>
/// Created only when a workspace-load profile is already running.
/// The email, anchor name, and branch JSON are hashed and then discarded. Lengths are kept so two calls can be compared.
/// <see cref="ExecuteReaderAwaitMs"/> is the client wait for <c>ExecuteReaderAsync</c>. It is not SQL Server execution time.
/// The graph copy insert inside the procedure is not a separate duration. On an avatar call it remains inside <see cref="TotalReaderMs"/>.
/// </remarks>
internal sealed class AnchorProfileReadCapture
{
	internal const string MemberCaller = "member";

	internal const string AvatarCaller = "avatar";

	private static readonly string[] SetNames =
	[
		"profiles",
		"branchValid",
		"branchAnchors",
		"placements",
		"fronting",
		"avatarTimestamps"
	];

	private readonly List<ResultSet> _sets = [];

	/// <summary>Starts a capture for a member or avatar profile read.</summary>
	/// <param name="caller"><see cref="MemberCaller"/> or <see cref="AvatarCaller"/>.</param>
	internal AnchorProfileReadCapture(string caller)
	{
		Caller = caller == AvatarCaller ? AvatarCaller : MemberCaller;
	}

	/// <summary>member or avatar.</summary>
	internal string Caller { get; }

	/// <summary>SHA-256 of the four procedure arguments. The arguments themselves are not kept.</summary>
	internal string ArgumentFingerprint { get; private set; } = "";

	/// <summary>system, group, place, or other. An unexpected value is not stored.</summary>
	internal string ObjectType { get; private set; } = "other";

	/// <summary>Character length of the email argument.</summary>
	internal int EmailLength { get; private set; }

	/// <summary>Character length of the anchor-name argument.</summary>
	internal int AnchorNameLength { get; private set; }

	/// <summary>Character length of the branch JSON argument.</summary>
	internal int BranchJsonLength { get; private set; }

	/// <summary>Client wait until the reader was returned. Null until that wait finishes.</summary>
	internal long? ExecuteReaderAwaitMs { get; set; }

	/// <summary>Client time from the start of that wait through mapping. Null until the read finishes.</summary>
	internal long? TotalReaderMs { get; set; }

	/// <summary>True when this call is the avatar procedure and the graph insert stays inside <see cref="TotalReaderMs"/>.</summary>
	internal bool GraphCopyInsertIncludedInReader => Caller == AvatarCaller;

	/// <summary>The result sets drained for this call, in procedure order.</summary>
	internal IReadOnlyList<ResultSet> Sets => _sets;

	/// <summary>
	/// Records lengths and a fingerprint of the four arguments that will be sent.
	/// </summary>
	/// <param name="email">Account email. It is not stored.</param>
	/// <param name="objectType">system, group, or place. Any other text is recorded as other.</param>
	/// <param name="anchorName">Anchor display name. It is not stored.</param>
	/// <param name="branchJson">Branch JSON sent to the procedure. It is not stored.</param>
	internal void Bind(string email, string objectType, string anchorName, string branchJson)
	{
		EmailLength = email.Length;
		AnchorNameLength = anchorName.Length;
		BranchJsonLength = branchJson.Length;
		ObjectType = objectType is "system" or "group" or "place" ? objectType : "other";
		ArgumentFingerprint = Fingerprint(email, objectType, anchorName, branchJson);
	}

	/// <summary>Records one drained result set.</summary>
	/// <param name="index">Zero-based set number.</param>
	/// <param name="rows">Rows copied from that set.</param>
	/// <param name="drainMs">Client time spent reading that set, including the move to the next set.</param>
	internal void AddDrain(int index, int rows, long drainMs)
	{
		while (_sets.Count <= index)
		{
			_sets.Add(new ResultSet(NameFor(_sets.Count)));
		}

		_sets[index].Rows = rows;
		_sets[index].DrainMs = drainMs;
	}

	/// <summary>Adds mapping time for one result set.</summary>
	/// <param name="index">Zero-based set number.</param>
	/// <param name="mapMs">Client time spent mapping that set.</param>
	internal void AddMap(int index, long mapMs)
	{
		while (_sets.Count <= index)
		{
			_sets.Add(new ResultSet(NameFor(_sets.Count)));
		}

		_sets[index].MapMs += mapMs;
	}

	/// <summary>Writes this capture into a profiling header. Argument text is not written.</summary>
	/// <param name="text">The header being built.</param>
	internal void AppendJson(StringBuilder text)
	{
		text.Append("{\"caller\":\"").Append(Caller)
			.Append("\",\"argumentFingerprint\":\"").Append(ArgumentFingerprint)
			.Append("\",\"objectType\":\"").Append(ObjectType)
			.Append("\",\"emailLength\":").Append(EmailLength)
			.Append(",\"anchorNameLength\":").Append(AnchorNameLength)
			.Append(",\"branchJsonLength\":").Append(BranchJsonLength)
			.Append(",\"executeReaderAwaitMs\":").Append(Number(ExecuteReaderAwaitMs))
			.Append(",\"totalReaderMs\":").Append(Number(TotalReaderMs))
			.Append(",\"resultSets\":[");
		for (var index = 0; index < _sets.Count; index++)
		{
			if (index > 0)
			{
				text.Append(',');
			}

			var set = _sets[index];
			text.Append("{\"name\":\"").Append(set.Name)
				.Append("\",\"rows\":").Append(set.Rows)
				.Append(",\"drainMs\":").Append(set.DrainMs)
				.Append(",\"mapMs\":").Append(set.MapMs)
				.Append('}');
		}

		text.Append(']');
		if (GraphCopyInsertIncludedInReader)
		{
			text.Append(",\"graphCopyInsert\":\"includedInProcedureReader\"");
		}

		text.Append('}');
	}

	/// <summary>The log fragment. It repeats the fingerprint and lengths, not the argument text.</summary>
	/// <returns>One space-separated diagnostic fragment.</returns>
	internal string LogText()
	{
		var sets = new StringBuilder();
		for (var index = 0; index < _sets.Count; index++)
		{
			if (index > 0)
			{
				sets.Append(',');
			}

			var set = _sets[index];
			sets.Append(set.Name).Append(':').Append(set.Rows).Append('/').Append(set.DrainMs).Append('/').Append(set.MapMs);
		}

		return "ProfileCaller=" + Caller
			+ " ArgumentFingerprint=" + ArgumentFingerprint
			+ " ObjectType=" + ObjectType
			+ " EmailLength=" + EmailLength
			+ " AnchorNameLength=" + AnchorNameLength
			+ " BranchJsonLength=" + BranchJsonLength
			+ " ExecuteReaderAwaitMs=" + (ExecuteReaderAwaitMs?.ToString() ?? "null")
			+ " TotalReaderMs=" + (TotalReaderMs?.ToString() ?? "null")
			+ " ResultSets=" + sets
			+ (GraphCopyInsertIncludedInReader ? " GraphCopyInsert=includedInProcedureReader" : "");
	}

	/// <summary>Hashes the four arguments in order. The caller does not keep the source text.</summary>
	/// <param name="email">Account email.</param>
	/// <param name="objectType">Anchor object type.</param>
	/// <param name="anchorName">Anchor display name.</param>
	/// <param name="branchJson">Branch JSON.</param>
	/// <returns>Uppercase SHA-256 hex.</returns>
	internal static string Fingerprint(string email, string objectType, string anchorName, string branchJson)
	{
		var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		Append(hasher, email);
		Append(hasher, objectType);
		Append(hasher, anchorName);
		Append(hasher, branchJson);
		return Convert.ToHexString(hasher.GetHashAndReset());
	}

	/// <summary>Appends one length-prefixed UTF-8 value.</summary>
	/// <param name="hasher">The fingerprint hasher.</param>
	/// <param name="value">One procedure argument.</param>
	private static void Append(IncrementalHash hasher, string value)
	{
		var bytes = Encoding.UTF8.GetBytes(value);
		hasher.AppendData(BitConverter.GetBytes(bytes.Length));
		hasher.AppendData(bytes);
	}

	/// <summary>The stable name for one set number.</summary>
	/// <param name="index">Zero-based set number.</param>
	/// <returns>The contract name, or setN past the six-set contract.</returns>
	private static string NameFor(int index)
	{
		return index < SetNames.Length ? SetNames[index] : "set" + index.ToString();
	}

	/// <summary>Formats one optional duration.</summary>
	/// <param name="value">The measured milliseconds.</param>
	/// <returns>The number, or null.</returns>
	private static string Number(long? value)
	{
		return value.HasValue ? value.Value.ToString() : "null";
	}

	/// <summary>One result set in a profiled read.</summary>
	internal sealed class ResultSet
	{
		/// <summary>Creates an empty set timing.</summary>
		/// <param name="name">The set name.</param>
		internal ResultSet(string name)
		{
			Name = name;
		}

		/// <summary>The set name.</summary>
		internal string Name { get; }

		/// <summary>Rows copied from the reader.</summary>
		internal int Rows { get; set; }

		/// <summary>Client time spent draining the set.</summary>
		internal long DrainMs { get; set; }

		/// <summary>Client time spent mapping the set.</summary>
		internal long MapMs { get; set; }
	}
}
