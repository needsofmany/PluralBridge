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
		/// Account code values
		/// </summary>
		internal const string accountCodeNumericFormat = "D6";
		internal static readonly int accountCodeExpirationMinutes = 15;
		internal static readonly string accountCodeEmailExpiresLine =
			$"This code expires in {accountCodeExpirationMinutes} minutes.";
		internal const string accountCodeInsertReturnedNoRows = "Registration verification code insert did not return inserted values.";

		/// <summary>
		/// Account code SQL parameter names
		/// </summary>
		internal const string sqlParameterAccountId = "@AccountId";
		internal const string sqlParameterAccountCodeId = "@AccountCodeId";
		internal const string sqlParameterCodePurpose = "@CodePurpose";
		internal const string sqlParameterDestinationType = "@DestinationType";
		internal const string sqlParameterDestinationNormalized = "@DestinationNormalized";
		internal const string sqlParameterCodeHash = "@CodeHash";
		internal const string sqlParameterCodeHashAlgorithm = "@CodeHashAlgorithm";
		internal const string sqlParameterCodeHashVersion = "@CodeHashVersion";
		internal const string sqlParameterCorrelationId = "@CorrelationId";
		internal const string sqlParameterExpirationMinutes = "@ExpirationMinutes";

		/// <summary>
		/// Account code database field names
		/// </summary>
		internal const string accountCodeHashFieldName = "CodeHash";

		/// <summary>
		/// Account service operation names
		/// </summary>
		internal const string guidFormatNoHyphens = "N";
		internal const string accountOperationRegistration = "registration";
		internal const string accountOperationRegistrationVerification = "registration_verification";
		internal const string accountOperationLogin = "login";
		internal const string accountOperationUsernameRecovery = "username_recovery";
		internal const string accountOperationPasswordReset = "password_reset";
		internal const string accountOperationPasswordChange = "password_change";
		internal const string accountOperationProfile = "profile";
		internal const string accountOperationContact = "contact";
		internal const string accountOperationContactVerification = "contact_verification";

		/// <summary>
		/// Account service response messages
		/// </summary>
		internal const string accountRegistrationCouldNotBeCompleted = "Registration could not be completed.";
		internal const string accountRegistrationAcceptedVerificationRequired = "Registration was accepted. Verification is required before login.";
		internal const string accountRegistrationAccepted = "Registration was accepted.";
		internal const string accountCodeCouldNotBeAccepted = "The code could not be accepted.";
		internal const string accountCodeExpiredOrCouldNotBeAccepted = "The code expired or could not be accepted.";
		internal const string accountRequestCouldNotBeCompleted = "The request could not be completed.";
		internal const string accountRegistrationVerificationCompleted = "Registration verification completed.";
		internal const string accountLoginCouldNotBeCompleted = "Login could not be completed.";
		internal const string accountLoginCompleted = "Login completed.";
		internal const string accountUsernameRecoveryInstructionsSent = "If the account can be found, recovery instructions will be sent.";
		internal const string accountPasswordResetInstructionsSent = "If the account can be found, password reset instructions will be sent.";
		internal const string accountPasswordResetCouldNotBeCompleted = "Password reset could not be completed.";
		internal const string accountPasswordResetCompleted = "Password reset completed.";
		internal const string accountPasswordChangeCouldNotBeCompleted = "Password change could not be completed.";
		internal const string accountPasswordChangeCompleted = "Password change completed.";
		internal const string accountProfileUpdateCouldNotBeCompleted = "Profile update could not be completed.";
		internal const string accountProfileUpdateCompleted = "Profile update completed.";
		internal const string accountContactUpdateCouldNotBeCompleted = "Contact update could not be completed.";
		internal const string accountContactUpdateVerificationRequired = "Contact update verification is required.";
		internal const string accountContactVerificationCouldNotBeCompleted = "Contact verification could not be completed.";
		internal const string accountContactVerificationCompleted = "Contact verification completed.";

		/// <summary>
		/// Azure account code delivery configuration
		/// </summary>
		internal const string accountCodeDeliveryAzureConfigurationSection = "AccountCodeDelivery:AzureCommunicationServices";
		internal const string accountCodeDeliveryConnectionStringKey = "ConnectionString";
		internal const string accountCodeDeliveryFromAddressKey = "FromAddress";

		/// <summary>
		/// Azure account code delivery messages
		/// </summary>
		internal const string unsupportedAccountCodeDeliveryDestinationType = "Unsupported account code delivery destination type:";
		internal const string azureAccountCodeEmailDeliveryNotConfigured = "Azure account code email delivery is not configured.";
		internal const string azureAccountCodeEmailDeliveryNotConfiguredLog = "Azure account code email delivery is not configured. MissingConnectionString={MissingConnectionString} MissingFromAddress={MissingFromAddress} AccountId={AccountId} CodePurpose={CodePurpose} CorrelationId={CorrelationId}";
		internal const string accountCodeEmailSentLog = "Account code email sent. AccountId={AccountId} CodePurpose={CodePurpose} DestinationType={DestinationType} CorrelationId={CorrelationId} OperationId={OperationId}";
		internal const string accountCodeEmailDeliveryFailedLog = "Account code email delivery failed. AccountId={AccountId} CodePurpose={CodePurpose} DestinationType={DestinationType} CorrelationId={CorrelationId}";

		/// <summary>
		/// Account code email subjects
		/// </summary>
		internal const string accountCodeEmailSubjectRegistrationVerification = "PluralBridge verification code";
		internal const string accountCodeEmailSubjectUsernameRecovery = "PluralBridge username recovery code";
		internal const string accountCodeEmailSubjectPasswordReset = "PluralBridge password reset code";
		internal const string accountCodeEmailSubjectContactVerification = "PluralBridge contact verification code";
		internal const string accountCodeEmailSubjectDefault = "PluralBridge account code";

		/// <summary>
		/// Account code email body parts
		/// </summary>
		internal const string accountCodeEmailActionVerification = "verification";
		internal const string accountCodeEmailActionUsernameRecovery = "username recovery";
		internal const string accountCodeEmailActionPasswordReset = "password reset";
		internal const string accountCodeEmailActionContactVerification = "contact verification";
		internal const string accountCodeEmailActionDefault = "account";
		internal const string accountCodeEmailIgnoreLine = "If you did not request this code, you can ignore this message.";

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
