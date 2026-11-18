// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace PluralBridge.Api.Workspace;

/// <summary>
/// Process configuration for the workspace context store.
/// </summary>
/// <remarks>
/// There is no developer-settings endpoint in this application. These values come from configuration.
/// An ordinary workspace request cannot replace them. Idle timeout is not a messaging TTL.
/// </remarks>
public sealed class WorkspaceContextOptions
{
	/// <summary>The configuration section name.</summary>
	public const string SectionName = "WorkspaceContext";

	/// <summary>How long an unused context stays available. The default is 30 minutes.</summary>
	public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

	/// <summary>The retained-size budget for one API process. The default is 128 MiB.</summary>
	public long BudgetBytes { get; set; } = 128L * 1024 * 1024;
}

/// <summary>The idle timeout and budget observed by the store on each operation.</summary>
/// <remarks>
/// This contract is public because <see cref="WorkspaceContextStore"/> has a public constructor.
/// The configuration reader and the test settings remain internal.
/// </remarks>
public interface IWorkspaceContextSettings
{
	/// <summary>The current idle timeout. It must be positive and finite.</summary>
	TimeSpan IdleTimeout { get; }

	/// <summary>The current process budget, in bytes.</summary>
	long BudgetBytes { get; }
}

/// <summary>Reads the current validated <see cref="WorkspaceContextOptions"/>.</summary>
internal sealed class OptionsWorkspaceContextSettings(IOptionsMonitor<WorkspaceContextOptions> options) : IWorkspaceContextSettings
{
	/// <summary>The configured idle timeout.</summary>
	public TimeSpan IdleTimeout => options.CurrentValue.IdleTimeout;

	/// <summary>The configured budget.</summary>
	public long BudgetBytes => options.CurrentValue.BudgetBytes;
}

/// <summary>Settings a test can replace. A longer timeout does not clear an already expired entry.</summary>
internal sealed class MutableWorkspaceContextSettings : IWorkspaceContextSettings
{
	/// <summary>The idle timeout the next store operation reads.</summary>
	public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

	/// <summary>The budget the next store operation reads.</summary>
	public long BudgetBytes { get; set; } = 128L * 1024 * 1024;
}

/// <summary>Rejects a non-positive timeout or budget before the host finishes starting.</summary>
internal sealed class WorkspaceContextOptionsValidator : IValidateOptions<WorkspaceContextOptions>
{
	/// <summary>Validates the bound configuration.</summary>
	/// <param name="name">The named options instance. The default instance is used.</param>
	/// <param name="options">The bound values.</param>
	/// <returns>Success, or a failure that names the invalid field.</returns>
	public ValidateOptionsResult Validate(string? name, WorkspaceContextOptions options)
	{
		if (options.IdleTimeout <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail("WorkspaceContext:IdleTimeout must be positive and finite.");
		}

		if (options.BudgetBytes <= 0)
		{
			return ValidateOptionsResult.Fail("WorkspaceContext:BudgetBytes must be positive.");
		}

		return ValidateOptionsResult.Success;
	}
}
