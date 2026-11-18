// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using PluralBridge.Api;
using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Workspace;

/// <summary>The outcome of one opening session, before the store is touched.</summary>
internal abstract record WorkspaceOpeningResult
{
	/// <summary>The session resolved no authenticated account. Nothing was published.</summary>
	internal sealed record MissingAccount : WorkspaceOpeningResult;

	/// <summary>The snapshot transaction committed and the captured rows are complete.</summary>
	/// <param name="Build">The rows copied before the connection closed.</param>
	internal sealed record Ready(WorkspaceCapturedBuild Build) : WorkspaceOpeningResult;
}

/// <summary>Rows captured from one committed opening build.</summary>
/// <param name="AccountId">The authenticated account.</param>
/// <param name="Edges">Copied graph edges.</param>
/// <param name="Occurrences">Copied walk occurrences.</param>
/// <param name="NodeLabels">Copied node labels.</param>
/// <param name="SystemEdgeLabels">Copied system edge labels.</param>
/// <param name="HierarchyPreview">Derived navigation rows with anchor names applied.</param>
/// <param name="AccessFacts">Membership facts for the anchors in the copy.</param>
internal sealed record WorkspaceCapturedBuild(
	Guid AccountId,
	IReadOnlyList<WorkspaceGraphEdge> Edges,
	IReadOnlyList<WorkspaceOccurrence> Occurrences,
	IReadOnlyList<WorkspaceNodeLabel> NodeLabels,
	IReadOnlyList<WorkspaceSystemEdgeLabel> SystemEdgeLabels,
	IReadOnlyList<PluralMapPreviewRow> HierarchyPreview,
	IReadOnlyList<WorkspaceAccessFact> AccessFacts);

/// <summary>
/// Runs the opening database reads. A failed or cancelled call returns no build.
/// </summary>
internal interface IWorkspaceOpeningSession
{
	/// <summary>Opens one snapshot transaction, or reports that no account is available.</summary>
	/// <param name="user">The authenticated session.</param>
	/// <param name="cancellationToken">Cancels the build. Cancellation does not publish a context.</param>
	/// <returns>The committed capture, or <see cref="WorkspaceOpeningResult.MissingAccount"/>.</returns>
	Task<WorkspaceOpeningResult> ExecuteAsync(ClaimsPrincipal user, CancellationToken cancellationToken);
}

/// <summary>
/// Executes <see cref="WorkspaceOpeningPlan"/> on one connection and one snapshot transaction.
/// </summary>
/// <remarks>
/// Temp tables are created by this session. Step 4 is not called.
/// The transaction commits only after render, copies, memberships, and anchor names have been read.
/// Snapshot error 3952 stops the build. The session does not change the isolation level and retry.
/// </remarks>
internal sealed class SqlWorkspaceOpeningSession(IConfiguration configuration) : IWorkspaceOpeningSession
{
	/// <summary>Runs the opening plan for the signed-in account.</summary>
	/// <param name="user">The authenticated session.</param>
	/// <param name="cancellationToken">Cancels the connection, command, or read.</param>
	/// <returns>The committed capture, or a missing-account result.</returns>
	public async Task<WorkspaceOpeningResult> ExecuteAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
	{
		var connectionString = configuration.GetConnectionString(Globals.connectionString);
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new WorkspaceOpeningException("The workspace tree connection string is not configured.");
		}

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);
		await using var transaction = await BeginSnapshotAsync(connection, cancellationToken);
		var committed = false;
		try
		{
			var account = await AccessContextHelper.ResolveCurrentAccountAsync(connection, user, cancellationToken, transaction);
			if (account is null)
			{
				await transaction.RollbackAsync(cancellationToken);
				return new WorkspaceOpeningResult.MissingAccount();
			}

			WorkspaceRenderOutput? render = null;
			List<WorkspaceGraphEdge>? edges = null;
			List<WorkspaceOccurrence>? occurrences = null;
			List<WorkspaceNodeLabel>? nodeLabels = null;
			List<WorkspaceSystemEdgeLabel>? systemEdgeLabels = null;
			List<WorkspaceAccessFact>? facts = null;

			foreach (var command in WorkspaceOpeningPlan.Commands)
			{
				cancellationToken.ThrowIfCancellationRequested();
				switch (command.Kind)
				{
					case WorkspaceOpeningCommandKind.Text:
						await ExecuteTextAsync(connection, transaction, command.Text, cancellationToken);
						break;
					case WorkspaceOpeningCommandKind.StoredProcedure:
						await ExecuteProcedureAsync(connection, transaction, command.Text, account.AccountId, command.PassesAccountId, cancellationToken);
						break;
					case WorkspaceOpeningCommandKind.ReadRender:
						render = await ReadRenderAsync(connection, transaction, cancellationToken);
						break;
					case WorkspaceOpeningCommandKind.ReadCopies:
						(edges, occurrences, nodeLabels, systemEdgeLabels) = await ReadCopiesAsync(connection, transaction, cancellationToken);
						break;
					case WorkspaceOpeningCommandKind.ReadMemberships:
						facts = await ReadMembershipsAsync(connection, transaction, account.AccountId, cancellationToken);
						break;
					case WorkspaceOpeningCommandKind.ReadAnchorNames:
						break;
					case WorkspaceOpeningCommandKind.Commit:
						if (render == null || edges == null || occurrences == null || nodeLabels == null || systemEdgeLabels == null || facts == null)
						{
							throw new WorkspaceRenderContractException();
						}

						var named = await ApplyAnchorNamesAsync(connection, transaction, render.HierarchyPreview, cancellationToken);
						await transaction.CommitAsync(cancellationToken);
						committed = true;
						return new WorkspaceOpeningResult.Ready(new WorkspaceCapturedBuild(
							account.AccountId,
							edges,
							occurrences,
							nodeLabels,
							systemEdgeLabels,
							named,
							facts));
				}
			}

			throw new WorkspaceOpeningException("The workspace opening plan ended before commit.");
		}
		catch (SqlException exception) when (exception.Number == WorkspaceOpeningPlan.SnapshotNotAllowedError)
		{
			throw new WorkspaceSnapshotUnavailableException();
		}
		finally
		{
			if (!committed)
			{
				try
				{
					await transaction.RollbackAsync(CancellationToken.None);
				}
				catch (Exception)
				{
					// The transaction was already rolled back, committed, or disposed.
				}
			}
		}
	}

	/// <summary>
	/// Starts the one snapshot transaction. SQL error 3952 becomes <see cref="WorkspaceSnapshotUnavailableException"/>.
	/// The session does not retry under another isolation level.
	/// </summary>
	/// <param name="connection">The open connection for this build.</param>
	/// <param name="cancellationToken">Cancels before the transaction starts.</param>
	/// <returns>The snapshot transaction.</returns>
	/// <exception cref="WorkspaceSnapshotUnavailableException">Snapshot isolation is not available.</exception>
	private static async Task<SqlTransaction> BeginSnapshotAsync(SqlConnection connection, CancellationToken cancellationToken)
	{
		try
		{
			return (SqlTransaction)await connection.BeginTransactionAsync(WorkspaceOpeningPlan.Isolation, cancellationToken);
		}
		catch (SqlException exception) when (exception.Number == WorkspaceOpeningPlan.SnapshotNotAllowedError)
		{
			throw new WorkspaceSnapshotUnavailableException();
		}
	}

	private static async Task ExecuteTextAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		string text,
		CancellationToken cancellationToken)
	{
		await using var command = new SqlCommand(text, connection, transaction);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	/// <summary>
	/// Runs one installed procedure. The account id is passed only when the opening plan says that procedure takes it.
	/// Render has no account parameter.
	/// </summary>
	/// <param name="connection">The open connection.</param>
	/// <param name="transaction">The snapshot transaction.</param>
	/// <param name="procedure">The installed procedure name.</param>
	/// <param name="accountId">The authenticated account.</param>
	/// <param name="passesAccountId">True when the plan passes <paramref name="accountId"/>.</param>
	/// <param name="cancellationToken">Cancels the procedure.</param>
	private static async Task ExecuteProcedureAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		string procedure,
		Guid accountId,
		bool passesAccountId,
		CancellationToken cancellationToken)
	{
		await using var command = new SqlCommand(procedure, connection, transaction)
		{
			CommandType = CommandType.StoredProcedure
		};
		if (passesAccountId)
		{
			command.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
		}

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	/// <summary>
	/// Reads the three render sets. A missing or reordered contract throws and the build is not published.
	/// </summary>
	/// <param name="connection">The open connection.</param>
	/// <param name="transaction">The snapshot transaction.</param>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The mapped render output.</returns>
	/// <exception cref="WorkspaceRenderContractException">The result sets do not match the captured contract.</exception>
	private static async Task<WorkspaceRenderOutput> ReadRenderAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		CancellationToken cancellationToken)
	{
		await using var command = new SqlCommand(WorkspaceOpeningPlan.RenderProcedure, connection, transaction)
		{
			CommandType = CommandType.StoredProcedure
		};
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		var sets = await PluralMapReader.ReadAllAsync(reader, cancellationToken);
		return PluralMapReader.MapRenderOutput(sets) ?? throw new WorkspaceRenderContractException();
	}

	/// <summary>
	/// Copies the four temp-table results in plan order: edges, occurrences, node labels, then system-edge labels.
	/// Ordinal column positions follow those temp tables. This read happens before the connection closes.
	/// </summary>
	/// <param name="connection">The open connection.</param>
	/// <param name="transaction">The snapshot transaction.</param>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The immutable source rows for the context.</returns>
	private static async Task<(List<WorkspaceGraphEdge> Edges, List<WorkspaceOccurrence> Occurrences, List<WorkspaceNodeLabel> Nodes, List<WorkspaceSystemEdgeLabel> Labels)> ReadCopiesAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		CancellationToken cancellationToken)
	{
		var copy = WorkspaceOpeningPlan.Commands.Single(command => command.Kind == WorkspaceOpeningCommandKind.ReadCopies);
		await using var command = new SqlCommand(copy.Text, connection, transaction);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		var edges = new List<WorkspaceGraphEdge>();
		while (await reader.ReadAsync(cancellationToken))
		{
			edges.Add(new WorkspaceGraphEdge(
				reader.GetString(0),
				reader.GetGuid(1),
				reader.GetString(2),
				reader.GetGuid(3),
				reader.GetInt32(4),
				reader.GetString(5)));
		}

		await reader.NextResultAsync(cancellationToken);
		var occurrences = new List<WorkspaceOccurrence>();
		while (await reader.ReadAsync(cancellationToken))
		{
			occurrences.Add(new WorkspaceOccurrence(
				reader.GetString(0),
				reader.GetGuid(1),
				reader.GetString(2),
				reader.GetGuid(3),
				reader.GetInt32(4),
				reader.GetString(5)));
		}

		await reader.NextResultAsync(cancellationToken);
		var nodes = new List<WorkspaceNodeLabel>();
		while (await reader.ReadAsync(cancellationToken))
		{
			nodes.Add(new WorkspaceNodeLabel(reader.GetString(0), reader.GetGuid(1), reader.GetString(2)));
		}

		await reader.NextResultAsync(cancellationToken);
		var labels = new List<WorkspaceSystemEdgeLabel>();
		while (await reader.ReadAsync(cancellationToken))
		{
			labels.Add(new WorkspaceSystemEdgeLabel(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
		}

		return (edges, occurrences, nodes, labels);
	}

	/// <summary>
	/// Reads membership rows for the copied nodes. A null object id is skipped because it is not an authorization object.
	/// Seed reachability and profile authorization stay distinguishable through status and the active flag.
	/// </summary>
	/// <param name="connection">The open connection.</param>
	/// <param name="transaction">The snapshot transaction.</param>
	/// <param name="accountId">The authenticated account.</param>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The captured membership facts.</returns>
	private static async Task<List<WorkspaceAccessFact>> ReadMembershipsAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		CancellationToken cancellationToken)
	{
		var membership = WorkspaceOpeningPlan.Commands.Single(command => command.Kind == WorkspaceOpeningCommandKind.ReadMemberships);
		await using var command = new SqlCommand(membership.Text, connection, transaction);
		command.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		var facts = new List<WorkspaceAccessFact>();
		while (await reader.ReadAsync(cancellationToken))
		{
			if (reader.IsDBNull(2))
			{
				continue;
			}

			facts.Add(new WorkspaceAccessFact(
				reader.GetGuid(0),
				reader.GetString(1),
				reader.GetGuid(2),
				reader.IsDBNull(3) ? null : reader.GetInt32(3),
				reader.IsDBNull(4) ? null : reader.GetString(4),
				reader.IsDBNull(5) ? null : reader.GetBoolean(5)));
		}

		return facts;
	}

	/// <summary>
	/// Reads anchor names on the same snapshot transaction. The names are display text, not an access grant.
	/// </summary>
	/// <param name="connection">The open connection.</param>
	/// <param name="transaction">The snapshot transaction.</param>
	/// <param name="rows">The preview rows already derived from render.</param>
	/// <param name="cancellationToken">Cancels the name read.</param>
	/// <returns>The preview with anchor names applied.</returns>
	private static async Task<IReadOnlyList<PluralMapPreviewRow>> ApplyAnchorNamesAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		IReadOnlyList<PluralMapPreviewRow> rows,
		CancellationToken cancellationToken)
	{
		var names = await PluralMapReader.ReadAnchorNamesAsync(connection, rows, cancellationToken, transaction);
		return PluralMapReader.ApplyAnchorNames(rows, names);
	}
}

/// <summary>
/// Publishes a context only after the opening session has committed a complete build.
/// </summary>
/// <remarks>
/// The public constructor is the host registration. It keeps the SQL session inside this assembly.
/// Tests use the internal constructor and supply a session that does not connect.
/// </remarks>
public sealed class WorkspaceTreeService
{
	private readonly IWorkspaceOpeningSession _session;
	private readonly WorkspaceContextStore _store;

	/// <summary>Creates the service registered by the host.</summary>
	/// <param name="configuration">Application configuration. The session reads the PluralBridge connection string.</param>
	/// <param name="store">The store that receives a committed build.</param>
	public WorkspaceTreeService(IConfiguration configuration, WorkspaceContextStore store)
		: this(new SqlWorkspaceOpeningSession(configuration), store)
	{
	}

	/// <summary>Creates the service with a caller-supplied session.</summary>
	/// <param name="session">The opening session.</param>
	/// <param name="store">The store that receives a committed build.</param>
	internal WorkspaceTreeService(IWorkspaceOpeningSession session, WorkspaceContextStore store)
	{
		_session = session;
		_store = store;
	}

	/// <summary>Runs one opening build and stores it.</summary>
	/// <param name="user">The authenticated session.</param>
	/// <param name="cancellationToken">Cancels the build before publish.</param>
	/// <returns>The response, or null when the session has no account.</returns>
	internal async Task<WorkspaceTreeResponse?> OpenAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
	{
		var result = await _session.ExecuteAsync(user, cancellationToken);
		if (result is WorkspaceOpeningResult.MissingAccount)
		{
			return null;
		}

		if (result is not WorkspaceOpeningResult.Ready ready)
		{
			throw new WorkspaceOpeningException("The workspace opening build did not complete.");
		}

		cancellationToken.ThrowIfCancellationRequested();
		var context = WorkspaceContext.Create(
			ready.Build.AccountId,
			ready.Build.Edges,
			ready.Build.Occurrences,
			ready.Build.NodeLabels,
			ready.Build.SystemEdgeLabels,
			ready.Build.HierarchyPreview,
			ready.Build.AccessFacts);
		var published = _store.Publish(context);
		return new WorkspaceTreeResponse(published.ContextId, published.AccountId, published.HierarchyPreview);
	}
}
