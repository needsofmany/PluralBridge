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
/// Legacy selection still requests the anchor profile route. A late detail response does not paint.
/// This specification starts with the flag off. The browser initializer sets workspaceTreeEnabled to true when the property is absent.
/// A failed replacement keeps the previous tree and context id. Automatic recovery does not start another load.
/// </remarks>
internal sealed class WorkspaceTreeViewSession
{
	private readonly List<string> _requests = new();
	private int _generation;

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
	public void Select(string path)
	{
		SelectedPath = path;
		DetailGeneration = _generation;
		if (WorkspaceTreeEnabled)
		{
			if (string.IsNullOrEmpty(ContextId))
			{
				DetailRequested = false;
				return;
			}

			DetailRequested = true;
			_requests.Add("GET member-details " + ContextId + " " + path);
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
}
