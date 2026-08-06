using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using PluralBridge.Api;
using PluralBridge.Api.Controllers;
using Xunit;

// ReSharper disable once CheckNamespace
namespace PluralBridge.Api.Tests.Controllers;

public sealed class MembersControllerApiSurfaceTests
{
	[Fact]
	public void MembersListEndpoint_UsesSystemScopedRoute()
	{
		var controllerRoute = typeof(MembersController)
			.GetCustomAttribute<RouteAttribute>();

		Assert.NotNull(controllerRoute);
		Assert.Equal(Globals.membersRoute, controllerRoute.Template);

		var getMethods = typeof(MembersController)
			.GetMethods(BindingFlags.Instance | BindingFlags.Public)
			.Where(method => method.Name == nameof(MembersController.Get))
			.ToList();

		var listMethod = Assert.Single(getMethods, method =>
		{
			var parameters = method.GetParameters();

			return parameters.Length == 1
				   && parameters[0].ParameterType == typeof(Guid)
				   && parameters[0].Name == "systemId";
		});

		Assert.NotNull(listMethod.GetCustomAttribute<HttpGetAttribute>());
	}

	[Fact]
	public void MemberDetailEndpoint_UsesMemberRouteUnderSystemScope()
	{
		var getMethods = typeof(MembersController)
			.GetMethods(BindingFlags.Instance | BindingFlags.Public)
			.Where(method => method.Name == nameof(MembersController.Get))
			.ToList();

		var detailMethod = Assert.Single(getMethods, method =>
		{
			var parameters = method.GetParameters();

			return parameters.Length == 2
				   && parameters[0].ParameterType == typeof(Guid)
				   && parameters[0].Name == "systemId"
				   && parameters[1].ParameterType == typeof(Guid)
				   && parameters[1].Name == "memberId";
		});

		var httpGet = detailMethod.GetCustomAttribute<HttpGetAttribute>();

		Assert.NotNull(httpGet);
		Assert.Equal(Globals.routeMemberId, httpGet.Template);
	}

	[Fact]
	public void MemberCreateEndpoint_UsesPostAndDoesNotAcceptAuthorityFields()
	{
		var createMethod = typeof(MembersController)
			.GetMethod(nameof(MembersController.Create));

		Assert.NotNull(createMethod);
		Assert.NotNull(createMethod.GetCustomAttribute<HttpPostAttribute>());

		var parameters = createMethod.GetParameters();

		Assert.Equal(2, parameters.Length);
		Assert.Equal("systemId", parameters[0].Name);
		Assert.Equal(typeof(Guid), parameters[0].ParameterType);
		Assert.Equal("request", parameters[1].Name);
		Assert.Equal(typeof(MembersController.AddMemberRequest), parameters[1].ParameterType);

		var requestProperties = typeof(MembersController.AddMemberRequest)
			.GetProperties(BindingFlags.Instance | BindingFlags.Public)
			.Select(property => property.Name)
			.ToHashSet(StringComparer.Ordinal);

		Assert.Contains(nameof(MembersController.AddMemberRequest.DisplayName), requestProperties);
		Assert.DoesNotContain("SystemId", requestProperties);
		Assert.DoesNotContain("MemberId", requestProperties);
		Assert.DoesNotContain("AccountId", requestProperties);
		Assert.DoesNotContain("SystemMembershipId", requestProperties);
	}

	[Fact]
	public void MemberEditEndpoint_UsesPutAndDoesNotAcceptAuthorityFields()
	{
		var editMethod = typeof(MembersController)
			.GetMethod(nameof(MembersController.Edit));

		Assert.NotNull(editMethod);

		var httpPut = editMethod.GetCustomAttribute<HttpPutAttribute>();

		Assert.NotNull(httpPut);
		Assert.Equal(Globals.routeMemberId, httpPut.Template);

		var parameters = editMethod.GetParameters();

		Assert.Equal(3, parameters.Length);
		Assert.Equal("systemId", parameters[0].Name);
		Assert.Equal(typeof(Guid), parameters[0].ParameterType);
		Assert.Equal("memberId", parameters[1].Name);
		Assert.Equal(typeof(Guid), parameters[1].ParameterType);
		Assert.Equal("request", parameters[2].Name);
		Assert.Equal(typeof(MembersController.EditMemberRequest), parameters[2].ParameterType);

		var requestProperties = typeof(MembersController.EditMemberRequest)
			.GetProperties(BindingFlags.Instance | BindingFlags.Public)
			.Select(property => property.Name)
			.ToHashSet(StringComparer.Ordinal);

		Assert.Contains(nameof(MembersController.EditMemberRequest.DisplayName), requestProperties);
		Assert.DoesNotContain("SystemId", requestProperties);
		Assert.DoesNotContain("AccountId", requestProperties);
		Assert.DoesNotContain("SystemMembershipId", requestProperties);
	}

	[Fact]
	public void MemberAuditOperations_UseExpectedOperationNames()
	{
		Assert.Equal("member.add", MemberWriteAuditWriter.MemberAddOperation);
		Assert.Equal("member.edit", MemberWriteAuditWriter.MemberEditOperation);
	}
}
