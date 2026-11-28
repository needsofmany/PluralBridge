// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Diagnostics;
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
/// <param name="AnchorSettingRows">This account's stored visibility and permission rows. Null means none were captured.</param>
internal sealed record WorkspaceCapturedBuild(
	Guid AccountId,
	IReadOnlyList<WorkspaceGraphEdge> Edges,
	IReadOnlyList<WorkspaceOccurrence> Occurrences,
	IReadOnlyList<WorkspaceNodeLabel> NodeLabels,
	IReadOnlyList<WorkspaceSystemEdgeLabel> SystemEdgeLabels,
	IReadOnlyList<PluralMapPreviewRow> HierarchyPreview,
	IReadOnlyList<WorkspaceAccessFact> AccessFacts,
	IReadOnlyList<WorkspaceStoredAnchorSetting>? AnchorSettingRows = null);

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

	/// <summary>
	/// Opens one snapshot transaction and, when <paramref name="profile"/> is present, records connection, account, and opening-build durations on it.
	/// A null profile runs the same build and records nothing.
	/// </summary>
	/// <param name="user">The authenticated session.</param>
	/// <param name="cancellationToken">Cancels the build. Cancellation does not publish a context.</param>
	/// <param name="profile">The workspace-tree profile for this request. Null leaves the build untimed.</param>
	/// <returns>The committed capture, or <see cref="WorkspaceOpeningResult.MissingAccount"/>.</returns>
	Task<WorkspaceOpeningResult> ExecuteProfiledAsync(ClaimsPrincipal user, CancellationToken cancellationToken, WorkspaceTreeLoadProfile? profile)
		=> ExecuteAsync(user, cancellationToken);
}

/// <summary>
/// Executes <see cref="WorkspaceOpeningPlan"/> on one connection and one snapshot transaction.
/// </summary>
/// <remarks>
/// Temp tables are created by this session. Step 4 is not called.
/// The transaction commits only after render, copies, memberships, anchor settings, and anchor names have been read.
/// Snapshot error 3952 stops the build. The session does not change the isolation level and retry.
/// </remarks>
internal sealed class SqlWorkspaceOpeningSession(IConfiguration configuration) : IWorkspaceOpeningSession
{
	/// <summary>Runs the opening plan for the signed-in account.</summary>
	/// <param name="user">The authenticated session.</param>
	/// <param name="cancellationToken">Cancels the connection, command, or read.</param>
	/// <returns>The committed capture, or a missing-account result.</returns>
	public Task<WorkspaceOpeningResult> ExecuteAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
	{
		return ExecuteProfiledAsync(user, cancellationToken, null);
	}

	/// <summary>Runs the opening plan and records its stages when a profile is present.</summary>
	/// <param name="user">The authenticated session.</param>
	/// <param name="cancellationToken">Cancels the connection, command, or read.</param>
	/// <param name="profile">The workspace-tree profile. Null records no stage durations.</param>
	/// <returns>The committed capture, or a missing-account result.</returns>
	public async Task<WorkspaceOpeningResult> ExecuteProfiledAsync(
		ClaimsPrincipal user,
		CancellationToken cancellationToken,
		WorkspaceTreeLoadProfile? profile)
	{
		var connectionString = configuration.GetConnectionString(Globals.connectionString);
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new WorkspaceOpeningException("The workspace tree connection string is not configured.");
		}

		await using var connection = new SqlConnection(connectionString);
		await MeasureAsync(profile, static (timing, elapsed) => timing.SetConnectionOpen(elapsed), async () =>
		{
			await connection.OpenAsync(cancellationToken);
		});
		await using var transaction = await BeginSnapshotAsync(connection, cancellationToken);
		var committed = false;
		try
		{
			AccessContextHelper.Account? account = null;
			await MeasureAsync(profile, static (timing, elapsed) => timing.SetAccountResolution(elapsed), async () =>
			{
				account = await AccessContextHelper.ResolveCurrentAccountAsync(connection, user, cancellationToken, transaction);
			});
			if (account is null)
			{
				await transaction.RollbackAsync(cancellationToken);
				return new WorkspaceOpeningResult.MissingAccount();
			}

			var resolvedAccount = account;

			WorkspaceRenderOutput? render = null;
			List<WorkspaceGraphEdge>? edges = null;
			List<WorkspaceOccurrence>? occurrences = null;
			List<WorkspaceNodeLabel>? nodeLabels = null;
			List<WorkspaceSystemEdgeLabel>? systemEdgeLabels = null;
			List<WorkspaceAccessFact>? facts = null;
			List<WorkspaceStoredAnchorSetting>? anchorSettingRows = null;

			WorkspaceOpeningResult? built = null;
			await MeasureAsync(profile, static (timing, elapsed) => timing.SetOpeningBuild(elapsed), async () =>
			{
			foreach (var command in WorkspaceOpeningPlan.Commands)
			{
				cancellationToken.ThrowIfCancellationRequested();
				switch (command.Kind)
				{
					case WorkspaceOpeningCommandKind.Text:
						await TimeCommand(profile, "tempTables", () => ExecuteTextAsync(connection, transaction, command.Text, cancellationToken));
						break;
					case WorkspaceOpeningCommandKind.StoredProcedure:
						if (command.Text == WorkspaceOpeningPlan.SeedProcedure)
						{
							await TimeCommand(profile, OpeningCommandName(command.Text), () => ExecuteSeedCaptureAsync(connection, transaction, resolvedAccount.AccountId, cancellationToken));
						}
						else
						{
							await TimeCommand(profile, OpeningCommandName(command.Text), () => ExecuteProcedureAsync(connection, transaction, command.Text, resolvedAccount.AccountId, command.PassesAccountId, cancellationToken));
						}
						break;
					case WorkspaceOpeningCommandKind.ReadRender:
						render = await TimeCommand(profile, "renderLayer", () => ReadRenderAsync(connection, transaction, cancellationToken));
						break;
					case WorkspaceOpeningCommandKind.ReadCopies:
						(edges, occurrences, nodeLabels, systemEdgeLabels) = await ReadCopiesAsync(connection, transaction, cancellationToken, profile);
						break;
					case WorkspaceOpeningCommandKind.ReadMemberships:
						facts = await TimeCommand(profile, "memberships", () => ReadMembershipsAsync(connection, transaction, resolvedAccount.AccountId, cancellationToken));
						break;
					case WorkspaceOpeningCommandKind.ReadAnchorSettings:
						anchorSettingRows = await TimeCommand(profile, "anchorSettings", () => ReadAnchorSettingsAsync(connection, transaction, resolvedAccount.AccountId, cancellationToken));
						break;
					case WorkspaceOpeningCommandKind.ReadAnchorNames:
						break;
					case WorkspaceOpeningCommandKind.Commit:
						if (render == null || edges == null || occurrences == null || nodeLabels == null || systemEdgeLabels == null || facts == null || anchorSettingRows == null)
						{
							throw new WorkspaceRenderContractException();
						}

						var named = await TimeCommand(profile, "anchorNames", () => ApplyAnchorNamesAsync(connection, transaction, render.HierarchyPreview, cancellationToken));
						await TimeCommand(profile, "commit", async () =>
						{
							await transaction.CommitAsync(cancellationToken);
						});
						committed = true;
						built = new WorkspaceOpeningResult.Ready(new WorkspaceCapturedBuild(
							resolvedAccount.AccountId,
							edges,
							occurrences,
							nodeLabels,
							systemEdgeLabels,
							named,
							facts,
							anchorSettingRows));
						return;
				}
			}

			throw new WorkspaceOpeningException("The workspace opening plan ended before commit.");
			});
			if (built == null)
			{
				throw new WorkspaceOpeningException("The workspace opening plan ended before commit.");
			}

			return built;
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

	/// <summary>Runs one opening command. A null profile does not start a timer.</summary>
	/// <param name="profile">The workspace-tree profile. Null leaves the command untimed.</param>
	/// <param name="name">The command name stored on the profile.</param>
	/// <param name="action">The command work. Its result and errors stay unchanged.</param>
	/// <returns>The command result.</returns>
	private static async Task<T> TimeCommand<T>(
		WorkspaceTreeLoadProfile? profile,
		string name,
		Func<Task<T>> action)
	{
		if (profile == null)
		{
			return await action();
		}

		var watch = Stopwatch.StartNew();
		try
		{
			return await action();
		}
		finally
		{
			watch.Stop();
			profile.AddOpeningCommand(name, watch.ElapsedMilliseconds);
		}
	}

	/// <summary>Runs one opening command that does not return a value. A null profile does not start a timer.</summary>
	/// <param name="profile">The workspace-tree profile. Null leaves the command untimed.</param>
	/// <param name="name">The command name stored on the profile.</param>
	/// <param name="action">The command work.</param>
	/// <returns>The task completes when the command finishes.</returns>
	private static Task TimeCommand(
		WorkspaceTreeLoadProfile? profile,
		string name,
		Func<Task> action)
	{
		return TimeCommand(profile, name, async () =>
		{
			await action();
			return 0;
		});
	}

	/// <summary>The stable name for one graph procedure in the opening plan.</summary>
	/// <param name="procedure">The procedure name from the plan.</param>
	/// <returns>The diagnostic name.</returns>
	private static string OpeningCommandName(string procedure)
	{
		if (procedure == WorkspaceOpeningPlan.SeedProcedure)
		{
			return "topologySeed";
		}

		if (procedure == WorkspaceOpeningPlan.WalkerProcedure)
		{
			return "topologyWalker";
		}

		if (procedure == WorkspaceOpeningPlan.ExpansionProcedure)
		{
			return "graphExpansion";
		}

		if (procedure == WorkspaceOpeningPlan.LabelerProcedure)
		{
			return "nodeLabeler";
		}

		return "procedure";
	}

	private static async Task MeasureAsync(
		WorkspaceTreeLoadProfile? profile,
		Action<WorkspaceTreeLoadProfile, long> record,
		Func<Task> action)
	{
		if (profile == null)
		{
			await action();
			return;
		}

		var watch = Stopwatch.StartNew();
		try
		{
			await action();
		}
		finally
		{
			watch.Stop();
			record(profile, watch.ElapsedMilliseconds);
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
	/// Captures the seed procedure's one result set into the opening temp tables.
	/// </summary>
	/// <param name="connection">The open connection. It already owns the empty graph tables.</param>
	/// <param name="transaction">The snapshot transaction.</param>
	/// <param name="accountId">The authenticated account.</param>
	/// <param name="cancellationToken">Cancels the seed procedure or the copy.</param>
	/// <remarks>
	/// Walker, expansion, and the labeler stay on <see cref="ExecuteProcedureAsync"/>.
	/// The seed procedure no longer writes <c>#TopologyEdges</c> or <c>#SystemEdgeLabels</c>.
	/// </remarks>
	private static async Task ExecuteSeedCaptureAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		CancellationToken cancellationToken)
	{
		await using var command = new SqlCommand(
			AnchorMemberProfileSave.SeedRowsDeclarationSql + AnchorMemberProfileSave.SeedResultCaptureSql,
			connection,
			transaction);
		command.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
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
		CancellationToken cancellationToken,
		WorkspaceTreeLoadProfile? profile)
	{
		var copy = WorkspaceOpeningPlan.Commands.Single(command => command.Kind == WorkspaceOpeningCommandKind.ReadCopies);
		await using var command = new SqlCommand(copy.Text, connection, transaction);
		SqlDataReader? reader = null;
		try
		{
		var edges = await TimeCommand(profile, "copyEdges", async () =>
		{
			reader = await command.ExecuteReaderAsync(cancellationToken);
			var rows = new List<WorkspaceGraphEdge>();
			while (await reader.ReadAsync(cancellationToken))
			{
				rows.Add(new WorkspaceGraphEdge(
					reader.GetString(0),
					reader.GetGuid(1),
					reader.GetString(2),
					reader.GetGuid(3),
					reader.GetInt32(4),
					reader.GetString(5)));
			}

			return rows;
		});
		var openReader = reader ?? throw new WorkspaceOpeningException("The opening copy did not return its reader.");
		var occurrences = await TimeCommand(profile, "copyOccurrences", async () =>
		{
			await openReader.NextResultAsync(cancellationToken);
			var rows = new List<WorkspaceOccurrence>();
			while (await openReader.ReadAsync(cancellationToken))
			{
				rows.Add(new WorkspaceOccurrence(
					openReader.GetString(0),
					openReader.GetGuid(1),
					openReader.GetString(2),
					openReader.GetGuid(3),
					openReader.GetInt32(4),
					openReader.GetString(5)));
			}

			return rows;
		});
		var nodes = await TimeCommand(profile, "copyNodeLabels", async () =>
		{
			await openReader.NextResultAsync(cancellationToken);
			var rows = new List<WorkspaceNodeLabel>();
			while (await openReader.ReadAsync(cancellationToken))
			{
				rows.Add(new WorkspaceNodeLabel(openReader.GetString(0), openReader.GetGuid(1), openReader.GetString(2)));
			}

			return rows;
		});
		var labels = await TimeCommand(profile, "copySystemEdgeLabels", async () =>
		{
			await openReader.NextResultAsync(cancellationToken);
			var rows = new List<WorkspaceSystemEdgeLabel>();
			while (await openReader.ReadAsync(cancellationToken))
			{
				rows.Add(new WorkspaceSystemEdgeLabel(openReader.GetGuid(0), openReader.IsDBNull(1) ? null : openReader.GetString(1)));
			}

			return rows;
		});

		return (edges, occurrences, nodes, labels);
		}
		finally
		{
			if (reader != null)
			{
				await reader.DisposeAsync();
			}
		}
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
	/// Reads stored visibility and permission rows for the authenticated account.
	/// A row that does not identify an anchor fails the build rather than being dropped.
	/// </summary>
	/// <param name="connection">The open connection.</param>
	/// <param name="transaction">The snapshot transaction.</param>
	/// <param name="accountId">The authenticated account. Another account's rows are not read.</param>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The stored rows. An anchor with no row is absent here and later recorded as missing.</returns>
	private static async Task<List<WorkspaceStoredAnchorSetting>> ReadAnchorSettingsAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		Guid accountId,
		CancellationToken cancellationToken)
	{
		var settings = WorkspaceOpeningPlan.Commands.Single(command => command.Kind == WorkspaceOpeningCommandKind.ReadAnchorSettings);
		await using var command = new SqlCommand(settings.Text, connection, transaction);
		command.Parameters.Add("@AccountId", SqlDbType.UniqueIdentifier).Value = accountId;
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		var rows = new List<WorkspaceStoredAnchorSetting>();
		while (await reader.ReadAsync(cancellationToken))
		{
			if (reader.IsDBNull(0) || reader.IsDBNull(1) || reader.IsDBNull(2) || reader.IsDBNull(4))
			{
				throw new WorkspaceOpeningException("A stored anchor setting did not include its anchor or its modes.");
			}

			rows.Add(new WorkspaceStoredAnchorSetting(
				reader.GetString(0),
				reader.GetGuid(1),
				reader.GetString(2),
				reader.IsDBNull(3) ? null : reader.GetInt32(3),
				reader.GetString(4),
				reader.IsDBNull(5) ? null : reader.GetString(5)));
		}

		return rows;
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
	/// <remarks>The host path sets <c>useDatabase</c> so the SQL session supplies the build. A null profile records no durations.</remarks>
	internal Task<WorkspaceTreeResponse?> OpenAsync(
		ClaimsPrincipal user,
		CancellationToken cancellationToken,
		WorkspaceTreeLoadProfile? profile = null)
	{
		return LoadTrackedAsync(useDatabase: true, user, cancellationToken, profile, null);
	}

	/// <summary>
	/// Loads one tree through the database session or the test-data factory and publishes it.
	/// </summary>
	/// <param name="useDatabase">True reads through the database session. False uses the factory and does not open a connection.</param>
	/// <param name="user">The authenticated session. The factory does not read it.</param>
	/// <param name="cancellationToken">Cancels after the source returns and before the context is constructed or published.</param>
	/// <param name="profile">The workspace-tree profile. Null records no durations.</param>
	/// <param name="testDataFactory">Factory build used when <paramref name="useDatabase"/> is false. Null selects the all-inherit fixture.</param>
	/// <returns>The response, or null when the database session has no account. Cancellation throws before publication.</returns>
	/// <remarks>The factory path leaves connection, account, and opening-build durations unset. Both paths use <see cref="CreateContext"/>.</remarks>
	internal async Task<WorkspaceTreeResponse?> LoadTrackedAsync(
		bool useDatabase,
		ClaimsPrincipal user,
		CancellationToken cancellationToken,
		WorkspaceTreeLoadProfile? profile,
		Func<WorkspaceCapturedBuild>? testDataFactory)
	{
		var build = await ReadBuildAsync(useDatabase, user, cancellationToken, testDataFactory, profile);
		if (build == null)
		{
			return null;
		}

		cancellationToken.ThrowIfCancellationRequested();
		var context = Measure(profile, static (timing, elapsed) => timing.SetCreateContext(elapsed), () => CreateContext(build));
		var published = Measure(profile, static (timing, elapsed) => timing.SetPublish(elapsed), () => _store.Publish(context));
		return new WorkspaceTreeResponse(published.ContextId, published.AccountId, published.HierarchyPreview);
	}

	/// <summary>Runs one in-memory stage. A null profile performs the work and stores no duration.</summary>
	/// <typeparam name="T">The stage result.</typeparam>
	/// <param name="profile">The profile receiving the duration. Null stores nothing.</param>
	/// <param name="record">Writes the elapsed milliseconds onto the profile.</param>
	/// <param name="action">The stage work.</param>
	/// <returns>The stage result.</returns>
	private static T Measure<T>(WorkspaceTreeLoadProfile? profile, Action<WorkspaceTreeLoadProfile, long> record, Func<T> action)
	{
		if (profile == null)
		{
			return action();
		}

		var watch = Stopwatch.StartNew();
		try
		{
			return action();
		}
		finally
		{
			watch.Stop();
			record(profile, watch.ElapsedMilliseconds);
		}
	}

	/// <summary>
	/// Loads one tree and constructs its context. The boolean chooses the source, not a second mapping.
	/// </summary>
	/// <param name="useDatabase">True reads through the database session. False uses <see cref="WorkspaceTreeTestDataFactory"/> and does not open a connection.</param>
	/// <param name="user">The authenticated session. The factory does not read it.</param>
	/// <param name="cancellationToken">Cancels after the source returns and before the context is constructed.</param>
	/// <param name="testDataFactory">Factory build used when <paramref name="useDatabase"/> is false. Null selects the all-inherit three-system fixture. The database path ignores this value.</param>
	/// <returns>The constructed context, or null when the database session has no account.</returns>
	/// <remarks>Both sources return <see cref="WorkspaceCapturedBuild"/>. <see cref="CreateContext"/> is the only construction path.</remarks>
	internal async Task<WorkspaceContext?> LoadContextAsync(
		bool useDatabase,
		ClaimsPrincipal user,
		CancellationToken cancellationToken,
		Func<WorkspaceCapturedBuild>? testDataFactory = null)
	{
		var build = await ReadBuildAsync(useDatabase, user, cancellationToken, testDataFactory, null);
		if (build == null)
		{
			return null;
		}

		cancellationToken.ThrowIfCancellationRequested();
		return CreateContext(build);
	}

	/// <summary>Selects the database session or the test-data factory. Both return the same build type.</summary>
	/// <param name="useDatabase">True uses the SQL session. False uses the factory.</param>
	/// <param name="user">The authenticated session for the database path.</param>
	/// <param name="cancellationToken">Cancels the database read. The factory does not connect.</param>
	/// <param name="testDataFactory">Factory build used when <paramref name="useDatabase"/> is false. Null selects the all-inherit fixture.</param>
	/// <returns>The captured build, or null when the database session has no account.</returns>
	private async Task<WorkspaceCapturedBuild?> ReadBuildAsync(
		bool useDatabase,
		ClaimsPrincipal user,
		CancellationToken cancellationToken,
		Func<WorkspaceCapturedBuild>? testDataFactory,
		WorkspaceTreeLoadProfile? profile)
	{
		if (!useDatabase)
		{
			var factory = testDataFactory ?? WorkspaceTreeTestDataFactory.ThreeNestedSystems;
			return factory();
		}

		var result = await _session.ExecuteProfiledAsync(user, cancellationToken, profile);
		if (result is WorkspaceOpeningResult.MissingAccount)
		{
			return null;
		}

		if (result is not WorkspaceOpeningResult.Ready ready)
		{
			throw new WorkspaceOpeningException("The workspace opening build did not complete.");
		}

		return ready.Build;
	}

	/// <summary>Constructs the retained context from a captured build. Database and factory builds both use this method.</summary>
	/// <param name="build">Rows and stored settings from either source.</param>
	/// <returns>The context, including anchor settings encoded by the production mapping.</returns>
	private static WorkspaceContext CreateContext(WorkspaceCapturedBuild build)
	{
		return WorkspaceContext.Create(
			build.AccountId,
			build.Edges,
			build.Occurrences,
			build.NodeLabels,
			build.SystemEdgeLabels,
			build.HierarchyPreview,
			build.AccessFacts,
			build.AnchorSettingRows);
	}
}
