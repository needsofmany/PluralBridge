// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

// ReSharper disable InconsistentNaming

namespace PluralBridge.Api
{
	internal static class Globals
	{
		/// <summary>
		/// Application info
		/// </summary>
		internal const string connectionString = ("PluralBridgeChap2SafeSpine");
		internal const string projectPhase = "Account Implementation";
		internal const string roProof = "read-only proof";

		/// <summary>
		/// Error message parts
		/// </summary>
		internal const string missingConnectionString = "Missing connection string";
		internal const string missingConnStringDetail = $"ConnectionStrings:{connectionString} was not found."; 
		internal const string cantResolveAccess = "Access context could not be resolved.";
		internal const string currContextNotFound = "Current access context not found";
		internal const string currConfiguredAccount = "The configured current account could not be resolved.";
		internal const string requestFailed = "Request failed";
		internal const string currentSystemNoAuth = "Not authorized for current system";
		internal const string noActiveMembershipAccess = "The current account does not have active membership access to the resolved current system.";
		internal const string authenticationRequired = "Authentication is required.";

		/// <summary>
		/// Logger support
		/// </summary>
		internal const string traceLevel = "PB_LEVEL1_TRACE";

		internal const string stageNameRequired = "Stage name is required.";
		internal const string outcomeRequired = "Outcome is required.";

		/// <summary>
		/// HTTP header field names
		/// </summary>
		internal const string correlationID = "X-Correlation-ID";

		/// <summary>
		/// API route and endpoint parts
		/// </summary>
		internal const string apiName = $"{nameof(PluralBridge)}.{nameof(Api)}"; 
		internal const string systemsRouteRoot = "api/systems";
		internal const string systemsEndpointRoot = "/api/systems";
		internal const string routeSystemId = "{systemId:guid}";
		internal const string frontHistoryEndpointSegment = "front-history";
		internal const string customFieldsEndpointSegment = "custom-fields";
		internal const string importBatchesEndpointSegment = "import-batches";
		internal const string importMetadataEndpointSegment = "import-metadata";
		internal const string membersEndpointSegment = "members";
		internal const string privacyBucketsEndpointSegment = "privacy-buckets";
		internal const string sourceIdMappingsEndpointSegment = "source-id-mappings";
		internal const string sourceRecordsEndpointSegment = "source-records";
		internal const string sourceSystemsEndpointSegment = "source-systems";
		internal const string routeMemberId = "{memberId:guid}";
		internal const string accountEndpointSegment = "account";
		internal const string registerEndpointSegment = "register";
		internal const string verifyRegistrationEndpointSegment = "verify-registration";
		internal const string loginEndpointSegment = "login";
		internal const string forgotUsernameEndpointSegment = "forgot-username";
		internal const string forgotPasswordEndpointSegment = "forgot-password";
		internal const string resetPasswordEndpointSegment = "reset-password";
		internal const string changePasswordEndpointSegment = "change-password";
		internal const string profileEndpointSegment = "profile";
		internal const string contactEndpointSegment = "contact";
		internal const string verifyContactEndpointSegment = "verify-contact";

		/// <summary>
		/// Browser authentication routes
		/// </summary>
		internal const string browserLoginRoute = $"/{loginEndpointSegment}";
		internal const string browserLogoutRoute = "/logout";
		internal const string browserAppRoute = "/app/";

		/// <summary>
		/// Browser login form field names
		/// </summary>
		internal const string browserLoginUserNameField = "userName";
		internal const string browserLoginPasswordField = "password";

		/// <summary>
		/// API routes
		/// </summary>
		internal const string customFieldsRoute = $"{systemsRouteRoot}/{routeSystemId}/{customFieldsEndpointSegment}";
		internal const string frontHistoryRoute = $"{systemsRouteRoot}/{routeSystemId}/{frontHistoryEndpointSegment}";
		internal const string importBatchesRoute = $"{systemsRouteRoot}/{routeSystemId}/{importBatchesEndpointSegment}";
		internal const string importMetadataRoute = $"{systemsRouteRoot}/{routeSystemId}/{importMetadataEndpointSegment}";
		internal const string membersRoute = $"{systemsRouteRoot}/{routeSystemId}/{membersEndpointSegment}";
		internal const string privacyBucketsRoute = $"{systemsRouteRoot}/{routeSystemId}/{privacyBucketsEndpointSegment}";
		internal const string sourceIdMappingsRoute = $"{systemsRouteRoot}/{routeSystemId}/{sourceIdMappingsEndpointSegment}";
		internal const string sourceRecordsRoute = $"{systemsRouteRoot}/{routeSystemId}/{sourceRecordsEndpointSegment}";
		internal const string sourceSystemsRoute = $"api/{sourceSystemsEndpointSegment}";
		internal const string sourceSystemsEndpoint = $"/api/{sourceSystemsEndpointSegment}";
		internal const string memberRoute = $"{membersRoute}/{routeMemberId}";
		internal const string accountRouteRoot = $"api/{accountEndpointSegment}";
	}

	internal enum LogStageParts
	{
		started,
		error_path,
		reached,
		data_access,
		failed,
		completed,
		endpoint
	}

	internal enum CountKeys
	{
		// ReSharper disable once UnusedMember.Global
		sourceSystems,
		importBatches,
		systems,
		members,
		privacyBuckets,
		customFields,
		frontHistory,
		sourceRecords,
		sourceIdMappings
	}
}
