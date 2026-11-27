// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Workspace;

/// <summary>
/// The browser state transitions for the workspace-tree switch.
/// </summary>
/// <remarks>
/// The navigator implements these transitions. This class is the executable specification the tests run.
/// There is no JavaScript test host in this repository. A late generation or a changed mode does not paint.
/// Workspace selection requests member details for the retained context and does not fall back to the plural-map route.
/// The first request uses skip 0. A later request uses the next window of 20 unique direct members. One detail request is in flight at a time.
/// Skip advances by that window, including when the response omits profiles. A failed window keeps the cards and the same skip.
/// Legacy selection still requests the anchor profile route. A late detail response does not paint.
/// This specification starts with the flag off. The browser initializer sets workspaceTreeEnabled to true when the property is absent.
/// A failed replacement keeps the previous tree and context id. Automatic recovery does not start another load.
/// </remarks>
internal sealed class WorkspaceTreeViewSession
{
	/// <summary>Direct member cards requested in one window. The same size the server takes after skip.</summary>
	public const int MemberDetailBatchSize = 20;

	private readonly List<string> _requests = new();
	private readonly List<MemberPreviewRow> _preview = new();
	private readonly List<MemberCardView> _cards = new();
	private int _generation;
	private string? _inflightPath;
	private int _inflightSkip;
	private int _inflightGeneration;
	private bool _firstBatchAvatarsRequested;
	private bool _firstBatchAvatarsFailed;

	/// <summary>True when the developer flag selects the new tree.</summary>
	public bool WorkspaceTreeEnabled { get; private set; }

	/// <summary>The context id displayed with the current tree.</summary>
	public string? ContextId { get; private set; }

	/// <summary>The tree currently displayed.</summary>
	public IReadOnlyList<string> Tree { get; private set; } = Array.Empty<string>();

	/// <summary>The focused path. A selectable anchor updates this and starts the detail request for the active mode.</summary>
	public string? SelectedPath { get; private set; }

	/// <summary>The last error that did not clear a retained tree.</summary>
	public string? Error { get; private set; }

	/// <summary>True after a selection requested member details or legacy profiles.</summary>
	public bool DetailRequested { get; private set; }

	/// <summary>The generation captured when the current detail request started.</summary>
	public int DetailGeneration { get; private set; }

	/// <summary>Unique direct members already requested for the visible anchor. The next window starts here.</summary>
	public int DetailSkip { get; private set; }

	/// <summary>True while the visible anchor has a member-detail request that has not completed or failed.</summary>
	public bool DetailInFlight { get; private set; }

	/// <summary>True after every preview window for the visible anchor has been requested.</summary>
	public bool DetailExhausted { get; private set; }

	/// <summary>The last member-detail failure that still belongs to the visible anchor. Null after a window paints.</summary>
	public string? DetailError { get; private set; }

	/// <summary>
	/// True when the member pane is still too short to scroll, or the user is within one pane of its bottom.
	/// A successful window then starts the next request. A failure does not.
	/// </summary>
	public bool PaneNeedsAnotherBatch { get; set; }

	/// <summary>Cards currently shown, in the order they were appended. An existing entry is not replaced.</summary>
	public IReadOnlyList<MemberCardView> MemberCards => _cards;

	/// <summary>The member pane scroll offset. An avatar response does not change it.</summary>
	public int MemberScrollTop { get; set; }

	/// <summary>
	/// The anchor heading for the visible selection. The parenthetical count is every unique direct member in the retained preview.
	/// Loading a later window does not change it.
	/// </summary>
	public string DetailHeading { get; private set; } = "";

	/// <summary>True after sign-out proceeds.</summary>
	public bool LoggedOut { get; private set; }

	/// <summary>The requests this session recorded, in order.</summary>
	public IReadOnlyList<string> Requests => _requests;

	/// <summary>
	/// Switches paths, clears the displayed workspace, and releases a held context.
	/// </summary>
	/// <param name="enabled">The new flag value.</param>
	public void SetWorkspaceTreeEnabled(bool enabled)
	{
		var previous = ContextId;
		WorkspaceTreeEnabled = enabled;
		_generation++;
		ContextId = null;
		Tree = Array.Empty<string>();
		SelectedPath = null;
		Error = null;
		DetailRequested = false;
		ResetMemberPaging();
		if (!string.IsNullOrEmpty(previous))
		{
			_requests.Add("DELETE " + previous);
		}
	}

	/// <summary>Starts a load and returns its generation.</summary>
	/// <returns>The generation a later response must present.</returns>
	public int BeginLoad()
	{
		_generation++;
		DetailInFlight = false;
		var mode = WorkspaceTreeEnabled ? "workspace" : "legacy";
		_requests.Add("GET " + mode);
		return _generation;
	}

	/// <summary>Applies a workspace response when the generation and mode still match.</summary>
	/// <param name="generation">The generation returned by <see cref="BeginLoad"/>.</param>
	/// <param name="workspaceModeAtStart">The mode captured when the load started.</param>
	/// <param name="contextId">The new context id.</param>
	/// <param name="tree">The navigation rows.</param>
	/// <returns>True when the response painted the workspace.</returns>
	public bool CompleteWorkspaceLoad(int generation, bool workspaceModeAtStart, string contextId, IReadOnlyList<string> tree)
	{
		if (!IsCurrent(generation, workspaceModeAtStart))
		{
			return false;
		}

		var previous = ContextId;
		Tree = tree.ToArray();
		ContextId = contextId;
		SelectedPath = null;
		Error = null;
		DetailRequested = false;
		ResetMemberPaging();
		if (!string.IsNullOrEmpty(previous) && !string.Equals(previous, contextId, StringComparison.Ordinal))
		{
			_requests.Add("DELETE " + previous);
		}

		return true;
	}

	/// <summary>Applies a legacy map response when the generation and mode still match.</summary>
	/// <param name="generation">The generation returned by <see cref="BeginLoad"/>.</param>
	/// <param name="workspaceModeAtStart">The mode captured when the load started.</param>
	/// <param name="tree">The navigation rows.</param>
	/// <returns>True when the response painted the legacy tree.</returns>
	public bool CompleteLegacyLoad(int generation, bool workspaceModeAtStart, IReadOnlyList<string> tree)
	{
		if (!IsCurrent(generation, workspaceModeAtStart))
		{
			return false;
		}

		Tree = tree.ToArray();
		ContextId = null;
		SelectedPath = null;
		Error = null;
		return true;
	}

	/// <summary>
	/// Records a failed load. A replacement failure keeps the previous tree and context.
	/// </summary>
	/// <param name="generation">The generation returned by <see cref="BeginLoad"/>.</param>
	/// <param name="workspaceModeAtStart">The mode captured when the load started.</param>
	/// <param name="replacing">True when a tree and context were already displayed.</param>
	/// <param name="error">The error shown to the user.</param>
	/// <returns>True when this failure still belongs to the visible mode.</returns>
	public bool FailLoad(int generation, bool workspaceModeAtStart, bool replacing, string error)
	{
		if (!IsCurrent(generation, workspaceModeAtStart))
		{
			return false;
		}

		Error = error;
		if (replacing)
		{
			return true;
		}

		Tree = Array.Empty<string>();
		ContextId = null;
		SelectedPath = null;
		return true;
	}

	/// <summary>
	/// Focuses an anchor. Workspace mode requests direct member cards for the retained context.
	/// Legacy mode requests the anchor profile route. A workspace selection without a context id does not request.
	/// </summary>
	/// <param name="path">The selected path.</param>
	/// <param name="headingLabel">The anchor label, such as "System: Needs of the Many". Null leaves the heading blank.</param>
	public void Select(string path, string? headingLabel = null)
	{
		SelectedPath = path;
		DetailGeneration = _generation;
		ResetMemberPaging();
		if (headingLabel is not null)
		{
			DetailHeading = headingLabel + " (" + DirectMemberCount(path).ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
		}
		if (WorkspaceTreeEnabled)
		{
			if (string.IsNullOrEmpty(ContextId))
			{
				DetailRequested = false;
				return;
			}

			DetailRequested = true;
			StartMemberDetailRequest();
			return;
		}

		DetailRequested = true;
		_requests.Add("GET profile");
	}

	/// <summary>
	/// A detail response paints only when its generation is still current and the path is still selected.
	/// </summary>
	/// <param name="generation">The generation captured when the selection started.</param>
	/// <param name="path">The path whose cards arrived.</param>
	/// <returns>True when those cards belong on the current pane.</returns>
	public bool PaintDetail(int generation, string path)
	{
		return generation == DetailGeneration
			&& generation == _generation
			&& string.Equals(SelectedPath, path, StringComparison.Ordinal);
	}

	/// <summary>
	/// Retains the preview rows used to count direct members. Duplicate member ids, other parents, and non-members stay in the list.
	/// The count used for paging keeps the first member id for the selected parent, in this order.
	/// </summary>
	/// <param name="rows">Preview rows. Null retains an empty list.</param>
	public void RetainMemberPreview(IReadOnlyList<MemberPreviewRow>? rows)
	{
		_preview.Clear();
		if (rows is null)
		{
			return;
		}

		_preview.AddRange(rows);
	}

	/// <summary>Counts unique direct member ids for one occurrence, using the retained preview.</summary>
	/// <param name="path">The selected occurrence path.</param>
	/// <returns>The number of windows are based on this count. An empty id is not counted.</returns>
	public int DirectMemberCount(string path)
	{
		return DirectMemberIds(path).Count;
	}

	/// <summary>
	/// Starts the next member window when none is in flight and the preview still has one.
	/// A second call while the first is in flight does not request.
	/// </summary>
	/// <returns>True when this call recorded a request.</returns>
	public bool RequestNextPage()
	{
		if (!WorkspaceTreeEnabled || DetailInFlight || DetailExhausted || string.IsNullOrEmpty(ContextId) || string.IsNullOrEmpty(SelectedPath))
		{
			return false;
		}

		var before = _requests.Count;
		StartMemberDetailRequest();
		return _requests.Count > before;
	}

	/// <summary>
	/// Appends cards for the in-flight window and advances skip by <see cref="MemberDetailBatchSize"/>.
	/// Profiles for members already shown, or outside that window, are not added. A stale generation or path changes nothing.
	/// </summary>
	/// <param name="generation">The generation captured when the selection started.</param>
	/// <param name="path">The path whose cards arrived.</param>
	/// <param name="profileIds">Member ids returned for this window. Omitted ids are not cards and do not change the next skip.</param>
	/// <returns>True when the cards belong on the current pane.</returns>
	public bool AcceptMemberDetails(int generation, string path, IReadOnlyList<string> profileIds)
	{
		if (!DetailResponseIsCurrent(generation, path))
		{
			return false;
		}

		var completedSkip = _inflightSkip;
		var shown = new HashSet<string>(_cards.Select(card => card.MemberId), StringComparer.OrdinalIgnoreCase);
		foreach (var id in DirectMemberIds(path).Skip(completedSkip).Take(MemberDetailBatchSize))
		{
			if (!shown.Add(id) || !profileIds.Any(profileId => string.Equals(profileId, id, StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}

			_cards.Add(new MemberCardView(id));
		}

		DetailInFlight = false;
		DetailError = null;
		DetailSkip = completedSkip + MemberDetailBatchSize;
		if (completedSkip == 0 && _cards.Count > 0 && !_firstBatchAvatarsRequested)
		{
			_firstBatchAvatarsRequested = true;
			_requests.Add("GET member-avatars " + ContextId + " " + path + " 0");
		}
		if (DetailSkip >= DirectMemberIds(path).Count)
		{
			DetailExhausted = true;
		}
		else if (PaneNeedsAnotherBatch)
		{
			StartMemberDetailRequest();
		}

		return true;
	}

	/// <summary>
	/// Applies the first-page avatar response to cards already shown. A null image leaves the placeholder.
	/// A pending avatar edit, the card text, expansion, and scroll position stay as they are.
	/// </summary>
	/// <param name="generation">The generation captured when the selection started.</param>
	/// <param name="path">The path whose images arrived.</param>
	/// <param name="images">Member images in response order. Null image text means the member has no image.</param>
	/// <returns>True when this response still belongs to the first page of the visible selection.</returns>
	/// <remarks>A later page does not request avatars. A stale generation or path does not change the cards.</remarks>
	public bool AcceptMemberAvatars(int generation, string path, IReadOnlyList<MemberAvatarImage> images)
	{
		if (!FirstBatchAvatarResponseIsCurrent(generation, path))
		{
			return false;
		}

		foreach (var image in images)
		{
			if (!TryMemberId(image.MemberId, out var id))
			{
				continue;
			}

			var card = _cards.FirstOrDefault(candidate => string.Equals(candidate.MemberId, id, StringComparison.OrdinalIgnoreCase));
			if (card is null || card.PendingAvatar || image.Image is null)
			{
				continue;
			}

			card.Avatar = image.Image;
		}

		return true;
	}

	/// <summary>
	/// Records a failed first-page avatar read. The cards stay, and the failure does not request the images again.
	/// </summary>
	/// <param name="generation">The generation captured when the selection started.</param>
	/// <param name="path">The path whose image request failed.</param>
	/// <returns>True when the failure still belongs to the visible first page. A second failure does not add a request.</returns>
	public bool FailMemberAvatars(int generation, string path)
	{
		if (!FirstBatchAvatarResponseIsCurrent(generation, path) || _firstBatchAvatarsFailed)
		{
			return false;
		}

		_firstBatchAvatarsFailed = true;
		return true;
	}

	/// <summary>The first-page avatar response is current only after that page requested it and the selection is unchanged.</summary>
	/// <param name="generation">The generation captured when the selection started.</param>
	/// <param name="path">The path whose response arrived.</param>
	/// <returns>True when the response may update the visible first page.</returns>
	private bool FirstBatchAvatarResponseIsCurrent(int generation, string path)
	{
		return _firstBatchAvatarsRequested
			&& generation == DetailGeneration
			&& generation == _generation
			&& string.Equals(SelectedPath, path, StringComparison.Ordinal);
	}

	/// <summary>
	/// Records a failed window without removing cards or moving skip. It does not start another request.
	/// </summary>
	/// <param name="generation">The generation captured when the selection started.</param>
	/// <param name="path">The path whose request failed.</param>
	/// <returns>True when the failure still belongs to the visible selection.</returns>
	public bool FailMemberDetails(int generation, string path)
	{
		if (!DetailResponseIsCurrent(generation, path))
		{
			return false;
		}

		DetailInFlight = false;
		DetailError = "The member profiles could not be loaded.";
		return true;
	}

	/// <summary>
	/// Attempts release, then signs out even when release fails.
	/// </summary>
	/// <param name="releaseFails">True when the release request throws.</param>
	public void SignOut(bool releaseFails)
	{
		if (!string.IsNullOrEmpty(ContextId))
		{
			_requests.Add("DELETE " + ContextId);
		}

		if (releaseFails)
		{
			Error = "release-failed";
		}

		ContextId = null;
		Tree = Array.Empty<string>();
		SelectedPath = null;
		ResetMemberPaging();
		LoggedOut = true;
		_requests.Add("POST logout");
	}

	/// <summary>
	/// A response may paint only when both its generation and the mode captured at start still match.
	/// A flag change after the request started makes the response stale.
	/// </summary>
	/// <param name="generation">The generation captured when the load started.</param>
	/// <param name="workspaceModeAtStart">The path mode captured when the load started.</param>
	/// <returns>True when this response still belongs to the visible load.</returns>
	private bool IsCurrent(int generation, bool workspaceModeAtStart)
	{
		return generation == _generation && workspaceModeAtStart == WorkspaceTreeEnabled;
	}

	/// <summary>Clears the visible member page. The retained preview stays.</summary>
	private void ResetMemberPaging()
	{
		DetailSkip = 0;
		DetailInFlight = false;
		DetailExhausted = false;
		DetailError = null;
		DetailHeading = "";
		_inflightPath = null;
		_inflightSkip = 0;
		_inflightGeneration = 0;
		_firstBatchAvatarsRequested = false;
		_firstBatchAvatarsFailed = false;
		_cards.Clear();
	}

	/// <summary>Records one member-detail window for the visible context and occurrence.</summary>
	private void StartMemberDetailRequest()
	{
		if (DetailInFlight || DetailExhausted || string.IsNullOrEmpty(ContextId) || string.IsNullOrEmpty(SelectedPath))
		{
			return;
		}

		if (DetailSkip > 0 && DetailSkip >= DirectMemberIds(SelectedPath).Count)
		{
			DetailExhausted = true;
			return;
		}

		DetailInFlight = true;
		_inflightPath = SelectedPath;
		_inflightSkip = DetailSkip;
		_inflightGeneration = DetailGeneration;
		_requests.Add("GET member-details " + ContextId + " " + SelectedPath + " " + DetailSkip.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}

	/// <summary>A detail response is current only for the window that is still in flight.</summary>
	/// <param name="generation">The generation captured when the selection started.</param>
	/// <param name="path">The path whose response arrived.</param>
	/// <returns>True when that window is still the visible request.</returns>
	private bool DetailResponseIsCurrent(int generation, string path)
	{
		return DetailInFlight
			&& generation == _inflightGeneration
			&& generation == DetailGeneration
			&& generation == _generation
			&& string.Equals(SelectedPath, path, StringComparison.Ordinal)
			&& string.Equals(_inflightPath, path, StringComparison.Ordinal);
	}

	/// <summary>Direct member ids for one parent, in preview order, with the first copy of each id kept.</summary>
	/// <param name="path">The selected occurrence path.</param>
	/// <returns>The ids paging uses. An empty guid is omitted.</returns>
	private List<string> DirectMemberIds(string path)
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var ids = new List<string>();
		foreach (var row in _preview)
		{
			if (!string.Equals(row.ParentPath, path, StringComparison.Ordinal)
				|| !string.Equals(row.ObjectType, "member", StringComparison.Ordinal)
				|| !TryMemberId(row.ObjectId, out var id)
				|| !seen.Add(id))
			{
				continue;
			}

			ids.Add(id);
		}

		return ids;
	}

	/// <summary>Accepts a member id. Blank text and an empty guid are not members.</summary>
	/// <param name="value">The preview object id.</param>
	/// <param name="id">The canonical id. Empty when the method returns false.</param>
	/// <returns>True when the value is a member id paging can count.</returns>
	private static bool TryMemberId(string? value, out string id)
	{
		if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
		{
			id = "";
			return false;
		}

		id = parsed.ToString("D");
		return true;
	}

	/// <summary>One retained preview row. Paging filters these the same way the server filters its preview.</summary>
	/// <param name="ParentPath">The parent occurrence path. Another path is not a direct member of the selection.</param>
	/// <param name="ObjectType">The row type. Only member rows are counted.</param>
	/// <param name="ObjectId">The member id. The first non-empty id is kept.</param>
	public readonly record struct MemberPreviewRow(string? ParentPath, string? ObjectType, string? ObjectId);

	/// <summary>One card left on the pane. Later windows append a new card and leave this one in place.</summary>
	public sealed class MemberCardView
	{
		/// <summary>Creates a card for one member id.</summary>
		/// <param name="memberId">The canonical member id.</param>
		public MemberCardView(string memberId)
		{
			MemberId = memberId;
		}

		/// <summary>The member this card shows.</summary>
		public string MemberId { get; }

		/// <summary>Edits held by the card. A later window does not clear this.</summary>
		public string State { get; set; } = "";

		/// <summary>True when the card is expanded. A later window does not collapse it.</summary>
		public bool Expanded { get; set; }

		/// <summary>The image applied to this card. Null is the placeholder.</summary>
		public string? Avatar { get; set; }

		/// <summary>True when a local avatar edit must keep its preview. A loaded image does not replace it.</summary>
		public bool PendingAvatar { get; set; }
	}

	/// <summary>One image in a first-page avatar response.</summary>
	/// <param name="MemberId">The member the image addresses.</param>
	/// <param name="Image">The image content. Null means that member has no image.</param>
	public readonly record struct MemberAvatarImage(string? MemberId, string? Image);
}
