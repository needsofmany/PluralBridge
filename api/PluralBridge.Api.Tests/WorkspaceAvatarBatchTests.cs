// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PluralBridge.Api.Controllers;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// The avatar batch authorizes a member page before it returns images.
/// These calls use the production batch service, the common loader, and the avatar factory. They do not open a database connection.
/// </summary>
public sealed class WorkspaceAvatarBatchTests
{
	/// <summary>
	/// The authorized page returns factory bytes in preview order. A member with no image stays null, and a member the procedure omits is absent.
	/// </summary>
	/// <returns>The task completes when the page matches that order.</returns>
	[Fact]
	public async Task Batch_ReturnsFactoryBytesInPreviewOrder()
	{
		var omitted = Guid.Parse("a1000000-0000-4000-8000-0000000000ee");
		var context = PageContext(
		[
			AvatarTestDataFactory.RedMemberId,
			AvatarTestDataFactory.RedMemberId,
			AvatarTestDataFactory.MemberWithoutAvatarId,
			omitted,
			AvatarTestDataFactory.BlueMemberId
		]);
		var gateway = new AuthorizingGateway(AvatarTestDataFactory.AccountId)
		{
			ProfileIds =
			[
				AvatarTestDataFactory.RedMemberId,
				AvatarTestDataFactory.MemberWithoutAvatarId,
				AvatarTestDataFactory.BlueMemberId
			]
		};
		var reader = new FactoryAvatarReader();
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader);

		var ready = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));

		Assert.Equal(1, gateway.Reads);
		Assert.Equal(
			[
				AvatarTestDataFactory.RedMemberId,
				AvatarTestDataFactory.MemberWithoutAvatarId,
				AvatarTestDataFactory.BlueMemberId
			],
			ready.Images.Select(image => image.MemberId).ToArray());
		Assert.Equal(AvatarTestDataFactory.RedPng, ready.Images[0].Bytes);
		Assert.Equal("image/png", ready.Images[0].ContentType);
		Assert.Null(ready.Images[1].Bytes);
		Assert.Null(ready.Images[1].ContentType);
		Assert.Equal(AvatarTestDataFactory.BluePng, ready.Images[2].Bytes);
		Assert.NotEqual(ready.Images[0].Bytes, ready.Images[2].Bytes);
		Assert.DoesNotContain(ready.Images, image => image.MemberId == omitted);
		Assert.Equal(
			[AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.MemberWithoutAvatarId, AvatarTestDataFactory.BlueMemberId],
			reader.Members);
		Assert.Null(context.FindAvatar(AvatarTestDataFactory.MemberWithoutAvatarId));
		Assert.Same(AvatarTestDataFactory.RedPng, context.FindAvatar(AvatarTestDataFactory.RedMemberId)!.Bytes);
	}

	/// <summary>
	/// A retained image is returned without another read, and the profile procedure still runs first.
	/// </summary>
	/// <returns>The task completes when the second authorized call reuses the entry and a failed call does not return it.</returns>
	[Fact]
	public async Task Batch_ReusesARetainedImageAfterAuthorization()
	{
		var context = PageContext([AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId]);
		var loaded = await AnchorMemberProfileSave.LoadAvatarAsync(
			useDatabase: false,
			AvatarTestDataFactory.AccountId,
			AvatarTestDataFactory.RedMemberId,
			CancellationToken.None,
			testDataFactory: AvatarTestDataFactory.Read);
		var retained = context.RetainAvatar(AvatarTestDataFactory.RedMemberId, loaded);
		var gateway = new AuthorizingGateway(AvatarTestDataFactory.AccountId)
		{
			ProfileIds = [AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId]
		};
		var reader = new FactoryAvatarReader();
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader);

		var first = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		var second = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));

		Assert.Equal(2, gateway.Reads);
		Assert.Equal([AvatarTestDataFactory.BlueMemberId], reader.Members);
		Assert.Same(retained, context.FindAvatar(AvatarTestDataFactory.RedMemberId));
		Assert.Same(retained!.Bytes, first.Images[0].Bytes);
		Assert.Same(first.Images[0].Bytes, second.Images[0].Bytes);
		Assert.Equal(AvatarTestDataFactory.BluePng, second.Images[1].Bytes);

		gateway.Failure = new InvalidOperationException("The profile procedure rejected the anchor.");
		var reads = reader.Members.Count;
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.Equal(reads, reader.Members.Count);
		Assert.Same(retained, context.FindAvatar(AvatarTestDataFactory.RedMemberId));
	}

	/// <summary>
	/// Another account and an invalid occurrence do not receive images or call the reader.
	/// </summary>
	/// <returns>The task completes when both requests are rejected.</returns>
	[Fact]
	public async Task Batch_RejectsAnotherAccountAndAWrongOccurrence()
	{
		var context = PageContext([AvatarTestDataFactory.RedMemberId]);
		var gateway = new AuthorizingGateway(Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"))
		{
			ProfileIds = [AvatarTestDataFactory.RedMemberId]
		};
		var reader = new FactoryAvatarReader();
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader);

		Assert.IsType<WorkspaceAvatarBatchResult.ContextUnavailable>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.Equal(0, gateway.Reads);
		Assert.Empty(reader.Members);

		gateway.AccountId = AvatarTestDataFactory.AccountId;
		var rejected = Assert.IsType<WorkspaceAvatarBatchResult.OccurrenceRejected>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "missing-path", CancellationToken.None));
		Assert.Equal("The selected occurrence is not in this workspace.", rejected.Error);
		Assert.Equal(0, gateway.Reads);
		Assert.Empty(reader.Members);

		var negative = Assert.IsType<WorkspaceAvatarBatchResult.OccurrenceRejected>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None, -1));
		Assert.Equal("The member batch skip must be zero or greater.", negative.Error);
		Assert.Equal(0, gateway.Reads);
	}

	/// <summary>
	/// Skip follows the same unique direct-member windows as cards. An empty page still authorizes and does not read images.
	/// </summary>
	/// <returns>The task completes when the second window and the empty window match those rules.</returns>
	[Fact]
	public async Task Batch_PagesUniqueMembersAndAuthorizesAnEmptyPage()
	{
		var ids = Enumerable.Range(1, 25).Select(MemberId).ToArray();
		var rows = new List<Guid> { ids[0] };
		rows.AddRange(ids);
		var context = PageContext(rows);
		var gateway = new AuthorizingGateway(AvatarTestDataFactory.AccountId) { ProfileIds = ids };
		var reader = new FactoryAvatarReader();
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader);

		var first = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		var second = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None, 20));
		var beyond = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None, 40));

		Assert.Equal(20, first.Images.Count);
		Assert.Equal(ids[0], first.Images[0].MemberId);
		Assert.Equal(ids[19], first.Images[19].MemberId);
		Assert.Equal(1, first.Images.Count(image => image.MemberId == ids[0]));
		Assert.Equal(ids.Skip(20).ToArray(), second.Images.Select(image => image.MemberId).ToArray());
		Assert.Empty(beyond.Images);
		Assert.Equal(3, gateway.Reads);
		Assert.Equal(25, reader.Members.Count);
		Assert.Equal(ids, reader.Members.Distinct().ToArray());
	}

	/// <summary>
	/// Cancellation and a reader failure do not return a partial page. Images already retained stay on the context.
	/// </summary>
	/// <returns>The task completes when both failures leave authorization in force.</returns>
	[Fact]
	public async Task Batch_PropagatesCancellationAndReaderFailure()
	{
		var context = PageContext([AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId]);
		var gateway = new AuthorizingGateway(AvatarTestDataFactory.AccountId)
		{
			ProfileIds = [AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId]
		};
		var reader = new FactoryAvatarReader { FailOn = AvatarTestDataFactory.BlueMemberId };
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader);

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.NotNull(context.FindAvatar(AvatarTestDataFactory.RedMemberId));
		Assert.Null(context.FindAvatar(AvatarTestDataFactory.BlueMemberId));

		gateway.Failure = new OperationCanceledException();
		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.NotNull(context.FindAvatar(AvatarTestDataFactory.RedMemberId));

		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();
		gateway.Failure = null;
		var reads = reader.Members.Count;
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", cancelled.Token));
		Assert.Equal(reads, reader.Members.Count);

		reader.FailOn = null;
		reader.MissingStorage = true;
		reader.Members.Clear();
		var missing = await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None);
		Assert.IsType<WorkspaceAvatarBatchResult.AvatarStorageMissing>(missing);
		Assert.Equal([AvatarTestDataFactory.BlueMemberId], reader.Members);
		Assert.Null(context.FindAvatar(AvatarTestDataFactory.BlueMemberId));
	}

	/// <summary>Publishes one anchor whose direct members are the supplied ids, including duplicates in that order.</summary>
	/// <param name="memberIds">Direct member ids in preview order.</param>
	/// <returns>The published context.</returns>
	private static WorkspaceContext PageContext(IReadOnlyList<Guid> memberIds)
	{
		var systemId = Guid.Parse("11111111-1111-4111-8111-111111111111");
		var rows = new List<PluralMapPreviewRow>
		{
			new("preview", "system-a", "system-a", "Harbor", "system", systemId, null, false, "Harbor")
		};
		for (var index = 0; index < memberIds.Count; index++)
		{
			rows.Add(new PluralMapPreviewRow(
				"preview",
				"member-" + index.ToString(),
				"member-" + index.ToString(),
				"Member",
				"member",
				memberIds[index],
				"system-a",
				false,
				null));
		}

		var context = WorkspaceContext.Create(
			AvatarTestDataFactory.AccountId,
			[],
			[new WorkspaceOccurrence("account", AvatarTestDataFactory.AccountId, "system", systemId, 0, "system-a")],
			[],
			[],
			rows,
			[new WorkspaceAccessFact(Guid.NewGuid(), "system", systemId, 1, "Active", true)]);
		Store(context);
		return context;
	}

	/// <summary>Stores a context where a later lookup can acquire it. Each call uses a new store.</summary>
	/// <param name="context">The context to publish.</param>
	/// <returns>The store that holds it.</returns>
	private static WorkspaceContextStore Store(WorkspaceContext context)
	{
		var store = new WorkspaceContextStore(
			new ManualWorkspaceClock(),
			new MutableWorkspaceContextSettings(),
			new SilentLogger());
		store.Publish(context);
		return store;
	}

	/// <summary>A numbered member id used to fill a page that has no factory image.</summary>
	/// <param name="index">The member number, 1 through 255.</param>
	/// <returns>The member id.</returns>
	private static Guid MemberId(int index)
	{
		return Guid.Parse("00000000-0000-4000-8000-0000000000" + index.ToString("X2"));
	}

	/// <summary>Builds the SQL exception number the avatar reader reports when its table is absent.</summary>
	/// <param name="number">The SQL error number.</param>
	/// <returns>An exception whose <see cref="SqlException.Number"/> is that value.</returns>
	private static SqlException CreateSqlException(int number)
	{
		var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		var errorCtor = typeof(SqlError).GetConstructors(flags).OrderByDescending(ctor => ctor.GetParameters().Length).First();
		var errorArgs = errorCtor.GetParameters().Select((parameter, index) => Argument(parameter.ParameterType, index == 0 ? number : 0)).ToArray();
		var error = errorCtor.Invoke(errorArgs);
		var collection = Activator.CreateInstance(typeof(SqlErrorCollection), true)!;
		typeof(SqlErrorCollection).GetMethods(flags).First(method => method.Name == "Add").Invoke(collection, [error]);
		var create = typeof(SqlException).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			.Where(method => method.Name == "CreateException")
			.Where(method => method.GetParameters().All(parameter =>
				parameter.ParameterType == typeof(SqlErrorCollection)
				|| parameter.ParameterType == typeof(string)
				|| parameter.ParameterType == typeof(Guid)
				|| typeof(Exception).IsAssignableFrom(parameter.ParameterType)))
			.OrderBy(method => method.GetParameters().Length)
			.First();
		var createArgs = create.GetParameters().Select(parameter => Argument(parameter.ParameterType, 0)).ToArray();
		for (var index = 0; index < createArgs.Length; index++)
		{
			if (create.GetParameters()[index].ParameterType == typeof(SqlErrorCollection))
			{
				createArgs[index] = collection;
			}
		}

		return (SqlException)create.Invoke(null, createArgs)!;
	}

	/// <summary>Supplies a constructor argument for a SQL exception type.</summary>
	/// <param name="type">The parameter type.</param>
	/// <param name="number">The integer to use for the first integer parameter.</param>
	/// <returns>A value of that type.</returns>
	private static object? Argument(Type type, int number)
	{
		if (type == typeof(int))
		{
			return number;
		}

		if (type == typeof(byte))
		{
			return (byte)0;
		}

		if (type == typeof(uint))
		{
			return 0u;
		}

		if (type == typeof(Guid))
		{
			return Guid.Empty;
		}

		if (type == typeof(string))
		{
			return "avatar";
		}

		return type.IsValueType ? Activator.CreateInstance(type) : null;
	}

	/// <summary>Resolves one account and returns only the profile ids the test authorizes.</summary>
	private sealed class AuthorizingGateway(Guid accountId) : IWorkspaceProfileGateway
	{
		/// <summary>The account Acquire must match. A different id makes the context unavailable.</summary>
		public Guid AccountId { get; set; } = accountId;

		/// <summary>How many profile reads ran.</summary>
		public int Reads { get; private set; }

		/// <summary>Members the procedure returns. A page member outside this set is omitted.</summary>
		public IReadOnlyList<Guid> ProfileIds { get; set; } = [];

		/// <summary>A failure raised instead of a profile bundle. Cancellation uses this path too.</summary>
		public Exception? Failure { get; set; }

		/// <inheritdoc />
		public Task<AccessContextHelper.Account?> ResolveAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult<AccessContextHelper.Account?>(new AccessContextHelper.Account(
				AccountId,
				"owner@example.com",
				"Owner",
				1,
				new AccessContextHelper.AccountStatus(1, "Active", "Active", 1, true),
				DateTime.UtcNow,
				null));
		}

		/// <inheritdoc />
		public Task<AnchorProfileBundle> ReadProfilesAsync(
			string email,
			string objectType,
			string anchorName,
			string branch,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (Failure is not null)
			{
				return Task.FromException<AnchorProfileBundle>(Failure);
			}

			Reads++;
			var profiles = ProfileIds
				.Select(id => new AnchorMemberProfile(id, "Member", null, null, null, null, null, null, false))
				.ToArray();
			return Task.FromResult(new AnchorProfileBundle(profiles, true, [], [], [], []));
		}
	}

	/// <summary>Loads missing images through the factory. It records every member the batch asks for.</summary>
	private sealed class FactoryAvatarReader : IWorkspaceAvatarReader
	{
		/// <summary>Members passed to the loader, in request order.</summary>
		public List<Guid> Members { get; } = [];

		/// <summary>A member whose read fails after any earlier member has been retained.</summary>
		public Guid? FailOn { get; set; }

		/// <summary>True when the next read reports that avatar storage is not installed.</summary>
		public bool MissingStorage { get; set; }

		/// <inheritdoc />
		public Task<(byte[] Bytes, string ContentType)?> ReadAsync(
			Guid accountId,
			Guid memberId,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Members.Add(memberId);
			if (MissingStorage)
			{
				throw CreateSqlException(208);
			}

			if (FailOn == memberId)
			{
				throw new InvalidOperationException("The avatar could not be read.");
			}

			return AnchorMemberProfileSave.LoadAvatarAsync(
				useDatabase: false,
				accountId,
				memberId,
				cancellationToken,
				testDataFactory: AvatarTestDataFactory.Read);
		}
	}

	/// <summary>A store logger that keeps no messages.</summary>
	private sealed class SilentLogger : ILogger<WorkspaceContextStore>
	{
		/// <inheritdoc />
		public IDisposable BeginScope<TState>(TState state)
			where TState : notnull
		{
			return SilentScope.Instance;
		}

		/// <inheritdoc />
		public bool IsEnabled(LogLevel logLevel)
		{
			return false;
		}

		/// <inheritdoc />
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
		}
	}

	/// <summary>The scope returned by <see cref="SilentLogger"/>.</summary>
	private sealed class SilentScope : IDisposable
	{
		/// <summary>The only scope.</summary>
		public static readonly SilentScope Instance = new();

		/// <inheritdoc />
		public void Dispose()
		{
		}
	}
}
