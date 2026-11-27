// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Claims;
using PluralBridge.Api.Controllers;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// Workspace member cards use one resolution of the selected occurrence.
/// These tests do not open a database connection and do not walk the chain themselves.
/// </summary>
public sealed class WorkspaceMemberDetailInheritanceTests
{
	private static readonly Guid NullMemberA = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd01");
	private static readonly Guid NullMemberB = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd02");
	private static readonly Guid ExplicitMember = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd03");
	private static readonly Guid UnsetMember = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd04");
	private static readonly Guid OneSidedMember = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd05");

	/// <summary>
	/// Null placements take the occurrence defaults. Explicit and unset stay stored.
	/// The two null members share one result. An untouched null side stays inherit on the placement fields.
	/// </summary>
	[Fact]
	public async Task Details_ApplyOneOccurrenceResolutionToTheMemberBatch()
	{
		var memberIds = new[] { NullMemberA, NullMemberB, ExplicitMember, UnsetMember, OneSidedMember };
		var context = await LoadInnerContext(memberIds);
		var store = new WorkspaceContextStore(new ManualWorkspaceClock(), new MutableWorkspaceContextSettings(), new SilentLogger());
		store.Publish(context);
		var gateway = new PlacementGateway(WorkspaceTreeTestDataFactory.AccountId);
		var service = new WorkspaceMemberDetailService(store, gateway);

		var ready = Assert.IsType<WorkspaceMemberDetailResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "outer/middle/inner", CancellationToken.None));

		Assert.Equal(1, gateway.Reads);
		Assert.Equal(memberIds, ready.Cards.Select(card => card.MemberId).ToArray());
		Assert.All(ready.Cards, card => Assert.Null(card.AvatarUrl));

		var firstNull = ready.Cards[0];
		var secondNull = ready.Cards[1];
		AssertInheritedDefault(firstNull);
		AssertInheritedDefault(secondNull);
		Assert.Equal(firstNull.ResolvedVisibilityTypeId, secondNull.ResolvedVisibilityTypeId);
		Assert.Equal(firstNull.ResolvedPermissionCode, secondNull.ResolvedPermissionCode);

		var explicitCard = ready.Cards[2];
		Assert.Equal("explicit", explicitCard.PlacementVisibilityMode);
		Assert.Equal(2, explicitCard.PlacementVisibilityTypeId);
		Assert.Equal("explicit", explicitCard.ResolvedVisibilityMode);
		Assert.Equal(2, explicitCard.ResolvedVisibilityTypeId);
		Assert.Equal("explicit", explicitCard.PlacementPermissionMode);
		Assert.Equal("editor", explicitCard.PlacementPermissionCode);
		Assert.Equal("editor", explicitCard.ResolvedPermissionCode);

		var unsetCard = ready.Cards[3];
		Assert.Equal("unset", unsetCard.PlacementVisibilityMode);
		Assert.Null(unsetCard.PlacementVisibilityTypeId);
		Assert.Equal("unset", unsetCard.ResolvedVisibilityMode);
		Assert.Null(unsetCard.ResolvedVisibilityTypeId);
		Assert.Equal("unset", unsetCard.PlacementPermissionMode);
		Assert.Null(unsetCard.PlacementPermissionCode);
		Assert.Equal("unset", unsetCard.ResolvedPermissionMode);
		Assert.Null(unsetCard.ResolvedPermissionCode);

		var oneSided = ready.Cards[4];
		Assert.Equal("explicit", oneSided.PlacementVisibilityMode);
		Assert.Equal(3, oneSided.PlacementVisibilityTypeId);
		Assert.Equal(3, oneSided.ResolvedVisibilityTypeId);
		Assert.Equal("inherit", oneSided.PlacementPermissionMode);
		Assert.Null(oneSided.PlacementPermissionCode);
		Assert.Equal("explicit", oneSided.ResolvedPermissionMode);
		Assert.Equal("viewer", oneSided.ResolvedPermissionCode);
	}

	/// <summary>A null placement keeps inherit on the control fields and puts the occurrence result on the resolved fields.</summary>
	/// <param name="card">A member whose stored placement modes were null.</param>
	private static void AssertInheritedDefault(AnchorMemberCard card)
	{
		Assert.Equal("inherit", card.PlacementVisibilityMode);
		Assert.Null(card.PlacementVisibilityTypeId);
		Assert.Equal("explicit", card.ResolvedVisibilityMode);
		Assert.Equal(1, card.ResolvedVisibilityTypeId);
		Assert.Equal("inherit", card.PlacementPermissionMode);
		Assert.Null(card.PlacementPermissionCode);
		Assert.Equal("explicit", card.ResolvedPermissionMode);
		Assert.Equal("viewer", card.ResolvedPermissionCode);
	}

	private static async Task<WorkspaceContext> LoadInnerContext(IReadOnlyList<Guid> memberIds)
	{
		var service = new WorkspaceTreeService(
			new GuardSession(),
			new WorkspaceContextStore(new ManualWorkspaceClock(), new MutableWorkspaceContextSettings(), new SilentLogger()));
		var context = await service.LoadContextAsync(
			useDatabase: false,
			new ClaimsPrincipal(),
			CancellationToken.None,
			() => WorkspaceTreeTestDataFactory.ThreeNestedSystemsWithIndependentSidesAndMembers(memberIds));
		Assert.NotNull(context);
		return context;
	}

	/// <summary>Fails if context construction opens the database session.</summary>
	private sealed class GuardSession : IWorkspaceOpeningSession
	{
		public Task<WorkspaceOpeningResult> ExecuteAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("The factory path must not open the database session.");
		}
	}

	/// <summary>Returns the batch placements without a connection. It has no save operation.</summary>
	private sealed class PlacementGateway : IWorkspaceProfileGateway
	{
		private readonly Guid _accountId;

		public PlacementGateway(Guid accountId)
		{
			_accountId = accountId;
		}

		public int Reads { get; private set; }

		public Task<AccessContextHelper.Account?> ResolveAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
		{
			return Task.FromResult<AccessContextHelper.Account?>(new AccessContextHelper.Account(
				_accountId,
				"owner@example.com",
				"Owner",
				1,
				new AccessContextHelper.AccountStatus(1, "Active", "Active", 1, true),
				DateTime.UtcNow,
				null));
		}

		public Task<AnchorProfileBundle> ReadProfilesAsync(
			string email,
			string objectType,
			string anchorName,
			string branch,
			CancellationToken cancellationToken)
		{
			Reads++;
			AnchorMemberProfile Profile(Guid id)
			{
				return new AnchorMemberProfile(id, "Member", null, null, null, null, null, null, false);
			}

			return Task.FromResult(new AnchorProfileBundle(
				[
					Profile(NullMemberA),
					Profile(NullMemberB),
					Profile(ExplicitMember),
					Profile(UnsetMember),
					Profile(OneSidedMember)
				],
				true,
				[],
				[
					new AnchorMemberPlacement(NullMemberA, null, null, null, null),
					new AnchorMemberPlacement(NullMemberB, null, null, null, null),
					new AnchorMemberPlacement(ExplicitMember, "explicit", 2, "explicit", "editor"),
					new AnchorMemberPlacement(UnsetMember, "unset", null, "unset", null),
					new AnchorMemberPlacement(OneSidedMember, "explicit", 3, null, null)
				],
				[],
				[]));
		}
	}

	private sealed class SilentLogger : Microsoft.Extensions.Logging.ILogger<WorkspaceContextStore>
	{
		public IDisposable BeginScope<TState>(TState state)
			where TState : notnull
		{
			return Empty.Instance;
		}

		public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
		{
			return false;
		}

		public void Log<TState>(
			Microsoft.Extensions.Logging.LogLevel logLevel,
			Microsoft.Extensions.Logging.EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
		}

		private sealed class Empty : IDisposable
		{
			internal static readonly Empty Instance = new();

			public void Dispose()
			{
			}
		}
	}
}
