// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Workspace;

/// <summary>
/// The browser state transitions for the workspace-tree switch.
/// </summary>
/// <remarks>
/// The navigator implements these transitions. This class is the executable specification the tests run.
/// There is no JavaScript test host in this repository. A late generation or a changed mode does not paint.
/// The workspace mode does not request profiles or avatars and does not fall back to the plural-map route.
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

	/// <summary>The focused path. Workspace selection changes only this value.</summary>
	public string? SelectedPath { get; private set; }

	/// <summary>The last error that did not clear a retained tree.</summary>
	public string? Error { get; private set; }

	/// <summary>True after a legacy selection requested member profiles.</summary>
	public bool DetailRequested { get; private set; }

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

	/// <summary>Focuses an anchor. Workspace mode does not request profiles.</summary>
	/// <param name="path">The selected path.</param>
	public void Select(string path)
	{
		SelectedPath = path;
		if (WorkspaceTreeEnabled)
		{
			return;
		}

		DetailRequested = true;
		_requests.Add("GET profile");
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
