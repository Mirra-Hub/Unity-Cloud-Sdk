namespace MirraCloud.Core.Errors
{
    /// <summary>
    /// Catalogue of every Cloud backend error code the SDK is aware of.
    /// </summary>
    /// <remarks>
    /// MUST stay in sync with:
    /// <list type="bullet">
    ///   <item>Backend: <c>Hub Cloud Backend/CloudShared/Errors/Common/CommonErrorCodes.cs</c> and module-specific <c>&lt;Module&gt;ErrorCodes</c> constants in <c>&lt;Module&gt;/Contracts/Errors/</c>.</item>
    ///   <item>Frontend: <c>Frontend/src/cloud/shared/errors/CloudErrorCodes.ts</c>.</item>
    /// </list>
    /// Manual synchronisation by design — codegen is intentionally out of scope.
    /// </remarks>
    public static class CloudErrorCodes
    {
        // === common ===
        public const string CommonNotFound = "common.not_found";
        public const string CommonForbidden = "common.forbidden";
        public const string CommonUnauthorized = "common.unauthorized";
        public const string CommonConflict = "common.conflict";
        public const string CommonValidation = "common.validation";
        public const string CommonConcurrency = "common.concurrency";
        public const string CommonRateLimited = "common.rate_limited";

        // === internal ===
        public const string InternalUnhandled = "internal.unhandled";
        public const string InternalUntyped = "internal.untyped_error";

        // === ab_tests ===
        public const string AbTestsCohortKeysDuplicate = "ab_tests.cohort_keys_duplicate";
        public const string AbTestsCohortShareOutOfRange = "ab_tests.cohort_share_out_of_range";
        public const string AbTestsCohortsCountOutOfRange = "ab_tests.cohorts_count_out_of_range";
        public const string AbTestsCohortsShareSumInvalid = "ab_tests.cohorts_share_sum_invalid";
        public const string AbTestsNotFound = "ab_tests.not_found";
        public const string AbTestsTargetAudienceShareOutOfRange = "ab_tests.target_audience_share_out_of_range";

        // === assets_storage ===
        public const string AssetsStorageAssetNameConflict = "assets_storage.asset_name_conflict";
        public const string AssetsStorageAssetNotFound = "assets_storage.asset_not_found";
        public const string AssetsStorageAssetNotPublic = "assets_storage.asset_not_public";
        public const string AssetsStorageAssetPathInvalid = "assets_storage.asset_path_invalid";
        public const string AssetsStorageAssetProjectOrBranchMismatch = "assets_storage.asset_project_or_branch_mismatch";
        public const string AssetsStorageAssetsConfigNotFound = "assets_storage.assets_config_not_found";
        public const string AssetsStorageAssetsConfigProjectMismatch = "assets_storage.assets_config_project_mismatch";
        public const string AssetsStorageBranchHeadCommitMissing = "assets_storage.branch_head_commit_missing";
        public const string AssetsStorageCacheKeyConflict = "assets_storage.cache_key_conflict";
        public const string AssetsStorageCacheKeyNotFound = "assets_storage.cache_key_not_found";
        public const string AssetsStorageFileNameControlCharacters = "assets_storage.file_name_control_characters";
        public const string AssetsStorageFileNameEmpty = "assets_storage.file_name_empty";
        public const string AssetsStorageFileNameForbiddenCharacters = "assets_storage.file_name_forbidden_characters";
        public const string AssetsStorageFileNameForbiddenSequence = "assets_storage.file_name_forbidden_sequence";
        public const string AssetsStorageFileNameTooLong = "assets_storage.file_name_too_long";
        public const string AssetsStorageFolderNameConflict = "assets_storage.folder_name_conflict";
        public const string AssetsStorageFolderNotFound = "assets_storage.folder_not_found";
        public const string AssetsStorageFolderProjectOrBranchMismatch = "assets_storage.folder_project_or_branch_mismatch";
        public const string AssetsStorageInsufficientStorage = "assets_storage.insufficient_storage";
        public const string AssetsStorageInvalidFolderId = "assets_storage.invalid_folder_id";
        public const string AssetsStorageQuotaLookupFailed = "assets_storage.quota_lookup_failed";
        public const string AssetsStorageTargetFolderProjectMismatch = "assets_storage.target_folder_project_mismatch";

        // === challenges ===
        public const string ChallengesAlreadyFinished = "challenges.already_finished";
        public const string ChallengesClaimNotAllowed = "challenges.claim_not_allowed";
        public const string ChallengesClaimNotReady = "challenges.claim_not_ready";
        public const string ChallengesEmptyPlayerName = "challenges.empty_player_name";
        public const string ChallengesParticipationRequired = "challenges.participation_required";
        public const string ChallengesPlayerNotFound = "challenges.player_not_found";

        // === chats ===
        public const string ChatsChannelAlreadyExists = "chats.channel_already_exists";
        public const string ChatsChannelCreateFailed = "chats.channel_create_failed";
        public const string ChatsChannelDeleted = "chats.channel_deleted";
        public const string ChatsChannelGroupOwnerRefRequired = "chats.channel_group_owner_ref_required";
        public const string ChatsChannelGroupOwnerRefTypeInvalid = "chats.channel_group_owner_ref_type_invalid";
        public const string ChatsChannelInvalidState = "chats.channel_invalid_state";
        public const string ChatsChannelInvalidType = "chats.channel_invalid_type";
        public const string ChatsChannelNameRequired = "chats.channel_name_required";
        public const string ChatsChannelNotActive = "chats.channel_not_active";
        public const string ChatsChannelNotFound = "chats.channel_not_found";
        public const string ChatsChannelOwnerRefRequired = "chats.channel_owner_ref_required";
        public const string ChatsChannelTemplateKeyRequired = "chats.channel_template_key_required";
        public const string ChatsMemberBannedByRequired = "chats.member_banned_by_required";
        public const string ChatsMemberNotInChannel = "chats.member_not_in_channel";
        public const string ChatsMemberProfileIdRequired = "chats.member_profile_id_required";
        public const string ChatsMessageAlreadyDeleted = "chats.message_already_deleted";
        public const string ChatsMessageBodyRequired = "chats.message_body_required";
        public const string ChatsMessageBodyTooLong = "chats.message_body_too_long";
        public const string ChatsMessageNotAuthor = "chats.message_not_author";
        public const string ChatsMessageNotFound = "chats.message_not_found";
        public const string ChatsMessageNumberExceedsLast = "chats.message_number_exceeds_last";
        public const string ChatsMessageNumberInvalid = "chats.message_number_invalid";
        public const string ChatsMessageTaggedMemberInvalid = "chats.message_tagged_member_invalid";
        public const string ChatsMessageTargetNotFound = "chats.message_target_not_found";
        public const string ChatsTemplateNotFound = "chats.template_not_found";

        // === cloud_packages ===
        public const string CloudPackagesFileStorageNotConfigured = "cloud_packages.file_storage_not_configured";
        public const string CloudPackagesIconFileRequired = "cloud_packages.icon_file_required";
        public const string CloudPackagesInstallationNotFound = "cloud_packages.installation_not_found";
        public const string CloudPackagesInstallationWrongPackage = "cloud_packages.installation_wrong_package";
        public const string CloudPackagesInstallationWrongProject = "cloud_packages.installation_wrong_project";
        public const string CloudPackagesInstallInProgress = "cloud_packages.install_in_progress";

        /// <summary>Task-only: carried in a finished install task's errorCode, never returned as an HTTP error.</summary>
        public const string CloudPackagesInstallInterrupted = "cloud_packages.install_interrupted";

        public const string CloudPackagesInstallQueueFull = "cloud_packages.install_queue_full";
        public const string CloudPackagesInstallUnavailable = "cloud_packages.install_unavailable";
        public const string CloudPackagesInvalidInstallationId = "cloud_packages.invalid_installation_id";
        public const string CloudPackagesInvalidPackageId = "cloud_packages.invalid_package_id";
        public const string CloudPackagesInvalidSourceBranchId = "cloud_packages.invalid_source_branch_id";
        public const string CloudPackagesInvalidSourceProjectId = "cloud_packages.invalid_source_project_id";
        public const string CloudPackagesPackageHasNoContent = "cloud_packages.package_has_no_content";
        public const string CloudPackagesPackageNotFound = "cloud_packages.package_not_found";
        public const string CloudPackagesPackageVersionNotFound = "cloud_packages.package_version_not_found";
        public const string CloudPackagesPackageWithoutVersions = "cloud_packages.package_without_versions";

        // === cloud_saves ===
        public const string CloudSavesAccessDenied = "cloud_saves.access_denied";
        public const string CloudSavesBranchEnvironmentEmpty = "cloud_saves.branch_environment_empty";
        public const string CloudSavesCustomIdInvalidFormat = "cloud_saves.custom_id_invalid_format";
        public const string CloudSavesCustomIdRequired = "cloud_saves.custom_id_required";
        public const string CloudSavesCustomIdTooLong = "cloud_saves.custom_id_too_long";
        public const string CloudSavesFieldEncodeMissingFields = "cloud_saves.field_encode_missing_fields";
        public const string CloudSavesFieldEncodeMissingValue = "cloud_saves.field_encode_missing_value";
        public const string CloudSavesFieldEncodeUnsupportedType = "cloud_saves.field_encode_unsupported_type";
        public const string CloudSavesFieldEncodeValueTooLarge = "cloud_saves.field_encode_value_too_large";
        public const string CloudSavesFileNotFound = "cloud_saves.file_not_found";
        public const string CloudSavesFileTooLarge = "cloud_saves.file_too_large";
        public const string CloudSavesIndexAlreadyExists = "cloud_saves.index_already_exists";
        public const string CloudSavesIndexDuplicateFieldKeys = "cloud_saves.index_duplicate_field_keys";
        public const string CloudSavesIndexDuplicateFields = "cloud_saves.index_duplicate_fields";
        public const string CloudSavesIndexFieldCountInvalid = "cloud_saves.index_field_count_invalid";
        public const string CloudSavesIndexFieldKeyRequired = "cloud_saves.index_field_key_required";
        public const string CloudSavesIndexQueryDisabled = "cloud_saves.index_query_disabled";
        public const string CloudSavesIndexQueryDuplicateFilters = "cloud_saves.index_query_duplicate_filters";
        public const string CloudSavesIndexQueryFilterKeyNotInIndex = "cloud_saves.index_query_filter_key_not_in_index";
        public const string CloudSavesIndexQueryFilterKeyRequired = "cloud_saves.index_query_filter_key_required";
        public const string CloudSavesIndexQueryFilterRequired = "cloud_saves.index_query_filter_required";
        public const string CloudSavesIndexQueryFilterValueRequired = "cloud_saves.index_query_filter_value_required";
        public const string CloudSavesIndexQueryLimitOutOfRange = "cloud_saves.index_query_limit_out_of_range";
        public const string CloudSavesIndexQueryNeFilterValueRequired = "cloud_saves.index_query_ne_filter_value_required";
        public const string CloudSavesIndexQueryNoMatchingIndex = "cloud_saves.index_query_no_matching_index";
        public const string CloudSavesIndexQueryOffsetInvalid = "cloud_saves.index_query_offset_invalid";
        public const string CloudSavesIndexQuerySampleSizeInvalid = "cloud_saves.index_query_sample_size_invalid";
        public const string CloudSavesIndexTotalKeysExceeded = "cloud_saves.index_total_keys_exceeded";
        public const string CloudSavesInvalidJsonValue = "cloud_saves.invalid_json_value";
        public const string CloudSavesInvalidMetaJson = "cloud_saves.invalid_meta_json";
        public const string CloudSavesItemDisappearedAfterUpsert = "cloud_saves.item_disappeared_after_upsert";
        public const string CloudSavesJsonDocumentTooLarge = "cloud_saves.json_document_too_large";
        public const string CloudSavesKeyDoesNotExist = "cloud_saves.key_does_not_exist";
        public const string CloudSavesKeyRequired = "cloud_saves.key_required";
        public const string CloudSavesRedisFailure = "cloud_saves.redis_failure";
        public const string CloudSavesServerAccessDenied = "cloud_saves.server_access_denied";
        public const string CloudSavesTooManyKeys = "cloud_saves.too_many_keys";
        public const string CloudSavesUpsertFailed = "cloud_saves.upsert_failed";
        public const string CloudSavesVersionConflict = "cloud_saves.version_conflict";

        // === daily_rewards ===
        public const string DailyRewardsAlreadyClaimedToday = "daily_rewards.already_claimed_today";
        public const string DailyRewardsCalendarCompleted = "daily_rewards.calendar_completed";
        public const string DailyRewardsCalendarDeleteFailed = "daily_rewards.calendar_delete_failed";
        public const string DailyRewardsCalendarDisabled = "daily_rewards.calendar_disabled";
        public const string DailyRewardsCalendarEnded = "daily_rewards.calendar_ended";
        public const string DailyRewardsCalendarNotFound = "daily_rewards.calendar_not_found";
        public const string DailyRewardsCalendarNotStarted = "daily_rewards.calendar_not_started";
        public const string DailyRewardsCalendarUpdateFailed = "daily_rewards.calendar_update_failed";
        public const string DailyRewardsCatchUpNotAllowed = "daily_rewards.catch_up_not_allowed";
        public const string DailyRewardsDayNotConfigured = "daily_rewards.day_not_configured";
        public const string DailyRewardsDayNotFound = "daily_rewards.day_not_found";
        public const string DailyRewardsDayOutsideCatchUp = "daily_rewards.day_outside_catch_up";
        public const string DailyRewardsSegmentMismatch = "daily_rewards.segment_mismatch";

        // === deployment ===
        public const string DeploymentBranchCloneFailed = "deployment.branch_clone_failed";
        public const string DeploymentBranchConflict = "deployment.branch_conflict";
        public const string DeploymentBranchHandlerNotRegistered = "deployment.branch_handler_not_registered";
        public const string DeploymentBranchHasUncommittedChanges = "deployment.branch_has_uncommitted_changes";
        public const string DeploymentBranchInitialCannotDelete = "deployment.branch_initial_cannot_delete";
        public const string DeploymentBranchInitializationFailed = "deployment.branch_initialization_failed";
        public const string DeploymentBranchMissingHeadOrDraft = "deployment.branch_missing_head_or_draft";
        public const string DeploymentBranchNameTaken = "deployment.branch_name_taken";
        public const string DeploymentBranchNoDraftChanges = "deployment.branch_no_draft_changes";
        public const string DeploymentBranchNoHeadCommit = "deployment.branch_no_head_commit";
        public const string DeploymentBranchNotFound = "deployment.branch_not_found";
        public const string DeploymentBranchProjectMismatch = "deployment.branch_project_mismatch";
        public const string DeploymentBranchReferencedByRouting = "deployment.branch_referenced_by_routing";
        public const string DeploymentClientVersionRequired = "deployment.client_version_required";
        public const string DeploymentCommitDraftNotFound = "deployment.commit_draft_not_found";
        public const string DeploymentCommitHeadNotFound = "deployment.commit_head_not_found";
        public const string DeploymentCommitIsDraft = "deployment.commit_is_draft";
        public const string DeploymentCommitNotFound = "deployment.commit_not_found";
        public const string DeploymentCommitNothingToCommit = "deployment.commit_nothing_to_commit";
        public const string DeploymentCommitParentNotFound = "deployment.commit_parent_not_found";
        public const string DeploymentCommitProjectMismatch = "deployment.commit_project_mismatch";
        public const string DeploymentConfigKeyNotRegistered = "deployment.config_key_not_registered";
        public const string DeploymentEnvironmentRequired = "deployment.environment_required";
        public const string DeploymentInvalidBranchId = "deployment.invalid_branch_id";
        public const string DeploymentInvalidClientVersion = "deployment.invalid_client_version";
        public const string DeploymentMergeBranchesDifferentProject = "deployment.merge_branches_different_project";
        public const string DeploymentMergeBranchesHaveDrafts = "deployment.merge_branches_have_drafts";
        public const string DeploymentMergeBranchesMissingHead = "deployment.merge_branches_missing_head";
        public const string DeploymentMergeHeadCommitNotFound = "deployment.merge_head_commit_not_found";
        public const string DeploymentMergeServiceFailure = "deployment.merge_service_failure";
        public const string DeploymentMergeUnresolvedConflicts = "deployment.merge_unresolved_conflicts";
        public const string DeploymentResolveNoActiveBranch = "deployment.resolve_no_active_branch";
        public const string DeploymentRuntimeRoutingDefaultTargetMissing = "deployment.runtime_routing_default_target_missing";
        public const string DeploymentRuntimeRoutingNotConfigured = "deployment.runtime_routing_not_configured";
        public const string DeploymentServiceConfigInvariantViolation = "deployment.service_config_invariant_violation";
        public const string DeploymentTargetBranchProjectMismatch = "deployment.target_branch_project_mismatch";

        // === economy ===
        public const string EconomyBaseTemplateNotFound = "economy.base_template_not_found";
        public const string EconomyBranchEnvironmentNotSet = "economy.branch_environment_not_set";
        public const string EconomyConfigInstanceNotFound = "economy.config_instance_not_found";
        public const string EconomyConfigurationInvalid = "economy.configuration_invalid";
        public const string EconomyContractInvalidArgument = "economy.contract_invalid_argument";
        public const string EconomyCurrencyOutOfRange = "economy.currency_out_of_range";
        public const string EconomyCurrencyPrecisionMismatch = "economy.currency_precision_mismatch";
        public const string EconomyDuplicateResourceKey = "economy.duplicate_resource_key";
        public const string EconomyEnergyConfigNotFound = "economy.energy_config_not_found";
        public const string EconomyEnergyInvalidDuration = "economy.energy_invalid_duration";
        public const string EconomyInvalidAmount = "economy.invalid_amount";
        public const string EconomyInvalidStableId = "economy.invalid_stable_id";
        public const string EconomyInventoryConfigNotFound = "economy.inventory_config_not_found";
        public const string EconomyInventoryDuplicateKey = "economy.inventory_duplicate_key";
        public const string EconomyInventoryInvalidKey = "economy.inventory_invalid_key";
        public const string EconomyInventoryMaxSlotsInvalid = "economy.inventory_max_slots_invalid";
        public const string EconomyInventoryNameRequired = "economy.inventory_name_required";
        public const string EconomyInventoryOverflowCircular = "economy.inventory_overflow_circular";
        public const string EconomyInventoryOverflowTargetMissing = "economy.inventory_overflow_target_missing";
        public const string EconomyInventoryOverflowTargetNotFound = "economy.inventory_overflow_target_not_found";
        public const string EconomyInventoryOverflowTargetSelf = "economy.inventory_overflow_target_self";
        public const string EconomyItemConsumeNotConfigured = "economy.item_consume_not_configured";
        public const string EconomyItemNoSlots = "economy.item_no_slots";
        public const string EconomyItemSlotNotFound = "economy.item_slot_not_found";
        public const string EconomyNotEnoughCurrency = "economy.not_enough_currency";
        public const string EconomyNotEnoughEnergy = "economy.not_enough_energy";
        public const string EconomyNotEnoughItems = "economy.not_enough_items";
        public const string EconomyResourceKeyRequired = "economy.resource_key_required";
        public const string EconomyResourceKindMismatch = "economy.resource_kind_mismatch";
        public const string EconomyResourceNotFound = "economy.resource_not_found";
        public const string EconomyTemplateInheritanceCycle = "economy.template_inheritance_cycle";
        public const string EconomyTemplateNotFound = "economy.template_not_found";
        public const string EconomyUnknownKind = "economy.unknown_kind";

        // === entities ===
        public const string EntitiesBaseTemplateNotFound = "entities.base_template_not_found";
        public const string EntitiesConfigInstanceAlreadyExists = "entities.config_instance_already_exists";
        public const string EntitiesConfigInstanceNotFound = "entities.config_instance_not_found";
        public const string EntitiesConfigInstanceNotInScope = "entities.config_instance_not_in_scope";
        public const string EntitiesConfigKeyDuplicate = "entities.config_key_duplicate";
        public const string EntitiesConfigMissingRoot = "entities.config_missing_root";
        public const string EntitiesConfigNotFound = "entities.config_not_found";
        public const string EntitiesConfigStableIdRequired = "entities.config_stable_id_required";
        public const string EntitiesDefaultValueInvalid = "entities.default_value_invalid";
        public const string EntitiesDerivedRemovedInheritedNode = "entities.derived_removed_inherited_node";
        public const string EntitiesEnumNotFound = "entities.enum_not_found";
        public const string EntitiesExpectedOverrideType = "entities.expected_override_type";
        public const string EntitiesFieldNotFound = "entities.field_not_found";
        public const string EntitiesFlagEnumTooManyValues = "entities.flag_enum_too_many_values";
        public const string EntitiesFolderDuplicateName = "entities.folder_duplicate_name";
        public const string EntitiesFolderMoveIntoDescendant = "entities.folder_move_into_descendant";
        public const string EntitiesFolderMoveIntoItself = "entities.folder_move_into_itself";
        public const string EntitiesFolderNameRequired = "entities.folder_name_required";
        public const string EntitiesFolderNotEmpty = "entities.folder_not_empty";
        public const string EntitiesFolderNotFound = "entities.folder_not_found";
        public const string EntitiesFolderNotInScope = "entities.folder_not_in_scope";
        public const string EntitiesInheritanceCycle = "entities.inheritance_cycle";
        public const string EntitiesInvalidStableIdFormat = "entities.invalid_stable_id_format";
        public const string EntitiesKeyAndNameRequired = "entities.key_and_name_required";
        public const string EntitiesNodeKindMismatch = "entities.node_kind_mismatch";
        public const string EntitiesNodeNotFound = "entities.node_not_found";
        public const string EntitiesNodeTypeMismatch = "entities.node_type_mismatch";
        public const string EntitiesNonArrayCollectionWithItemsType = "entities.non_array_collection_with_items_type";
        public const string EntitiesOverlayAddKeyedToArray = "entities.overlay_add_keyed_to_array";
        public const string EntitiesOverlayArrayMissingItemsType = "entities.overlay_array_missing_items_type";
        public const string EntitiesOverlayArrayTypeIdMismatch = "entities.overlay_array_type_id_mismatch";
        public const string EntitiesOverlayCannotRemoveRoot = "entities.overlay_cannot_remove_root";
        public const string EntitiesOverlayElementTypeMismatch = "entities.overlay_element_type_mismatch";
        public const string EntitiesOverlayEmptyNodeId = "entities.overlay_empty_node_id";
        public const string EntitiesOverlayEmptyParentId = "entities.overlay_empty_parent_id";
        public const string EntitiesOverlayNoOperations = "entities.overlay_no_operations";
        public const string EntitiesOverlayObjectKeyless = "entities.overlay_object_keyless";
        public const string EntitiesOverlayParentNotCollection = "entities.overlay_parent_not_collection";
        public const string EntitiesOverlayRequired = "entities.overlay_required";
        public const string EntitiesOverlayRootReplaceNotCollection = "entities.overlay_root_replace_not_collection";
        public const string EntitiesOverlayUnknownNode = "entities.overlay_unknown_node";
        public const string EntitiesOverlayUnknownParent = "entities.overlay_unknown_parent";
        public const string EntitiesParentFolderNotFound = "entities.parent_folder_not_found";
        public const string EntitiesParentFolderStableIdInvalid = "entities.parent_folder_stable_id_invalid";
        public const string EntitiesRootTypeInvalid = "entities.root_type_invalid";
        public const string EntitiesSdkJsonRootNotObject = "entities.sdk_json_root_not_object";
        public const string EntitiesSelfInheritance = "entities.self_inheritance";
        public const string EntitiesSignatureMismatch = "entities.signature_mismatch";
        public const string EntitiesStructureNotFound = "entities.structure_not_found";
        public const string EntitiesSystemEnumImmutable = "entities.system_enum_immutable";
        public const string EntitiesSystemKeyReserved = "entities.system_key_reserved";
        public const string EntitiesSystemRequiredFieldEmpty = "entities.system_required_field_empty";
        public const string EntitiesSystemRequiredNodeMissing = "entities.system_required_node_missing";
        public const string EntitiesSystemStructureImmutable = "entities.system_structure_immutable";
        public const string EntitiesSystemTemplateImmutable = "entities.system_template_immutable";
        public const string EntitiesSystemTemplateRootNotObject = "entities.system_template_root_not_object";
        public const string EntitiesSystemTypeUnknown = "entities.system_type_unknown";
        public const string EntitiesTemplateAlreadyExists = "entities.template_already_exists";
        public const string EntitiesTemplateHasDerived = "entities.template_has_derived";
        public const string EntitiesTemplateKeyDuplicate = "entities.template_key_duplicate";
        public const string EntitiesTemplateNotFound = "entities.template_not_found";
        public const string EntitiesTemplateNotInScope = "entities.template_not_in_scope";
        public const string EntitiesTemplateStableIdRequired = "entities.template_stable_id_required";
        public const string EntitiesTemplateUsedByInstances = "entities.template_used_by_instances";
        public const string EntitiesTypeRootMustBeObject = "entities.type_root_must_be_object";
        public const string EntitiesTypedNodeMissingTypeId = "entities.typed_node_missing_type_id";
        public const string EntitiesUnknownNodeType = "entities.unknown_node_type";
        public const string EntitiesUnsupportedItemType = "entities.unsupported_item_type";

        // === events ===
        public const string EventsActiveEventsCapReached = "events.active_events_cap_reached";
        public const string EventsConfigInstanceNotFound = "events.config_instance_not_found";
        public const string EventsEventBranchMismatch = "events.event_branch_mismatch";
        public const string EventsEventNotFound = "events.event_not_found";
        public const string EventsInvalidInstanceStableId = "events.invalid_instance_stable_id";
        public const string EventsOverrideBranchMismatch = "events.override_branch_mismatch";
        public const string EventsOverrideHandlerNotRegistered = "events.override_handler_not_registered";
        public const string EventsOverrideNotFound = "events.override_not_found";
        public const string EventsOverridePayloadInvalid = "events.override_payload_invalid";
        public const string EventsScheduleInvalidRange = "events.schedule_invalid_range";
        public const string EventsScheduleRecurrenceRequired = "events.schedule_recurrence_required";
        public const string EventsScheduleRequired = "events.schedule_required";

        // === friends ===
        public const string FriendsInvalidPlayerId = "friends.invalid_player_id";
        public const string FriendsSideBlocked = "friends.side_blocked";

        // === game_analytics ===
        public const string GameAnalyticsConfigNotFound = "game_analytics.config_not_found";
        public const string GameAnalyticsCreatedDateHeaderRequired = "game_analytics.created_date_header_required";
        public const string GameAnalyticsDashboardNotFound = "game_analytics.dashboard_not_found";
        public const string GameAnalyticsDashboardTemplateNotFound = "game_analytics.dashboard_template_not_found";
        public const string GameAnalyticsEventBatchEmpty = "game_analytics.event_batch_empty";
        public const string GameAnalyticsEventBatchTooLarge = "game_analytics.event_batch_too_large";
        public const string GameAnalyticsEventDoesNotAcceptParameters = "game_analytics.event_does_not_accept_parameters";
        public const string GameAnalyticsEventFilterBetweenRequiresTwoValues = "game_analytics.event_filter_between_requires_two_values";
        public const string GameAnalyticsEventFilterContainsRequiresString = "game_analytics.event_filter_contains_requires_string";
        public const string GameAnalyticsEventFilterInRequiresAtLeastOneValue = "game_analytics.event_filter_in_requires_at_least_one_value";
        public const string GameAnalyticsEventFilterInvalidParameterId = "game_analytics.event_filter_invalid_parameter_id";
        public const string GameAnalyticsEventFilterParameterIdentifierRequired = "game_analytics.event_filter_parameter_identifier_required";
        public const string GameAnalyticsEventFilterParameterNotInEvent = "game_analytics.event_filter_parameter_not_in_event";
        public const string GameAnalyticsEventFilterRequiresExactlyOneValue = "game_analytics.event_filter_requires_exactly_one_value";
        public const string GameAnalyticsEventHasNoFilterableParameters = "game_analytics.event_has_no_filterable_parameters";
        public const string GameAnalyticsEventHasNoParameters = "game_analytics.event_has_no_parameters";
        public const string GameAnalyticsEventIdOrNameRequired = "game_analytics.event_id_or_name_required";
        public const string GameAnalyticsEventIdsRequired = "game_analytics.event_ids_required";
        public const string GameAnalyticsEventNameRequired = "game_analytics.event_name_required";
        public const string GameAnalyticsEventNotFound = "game_analytics.event_not_found";
        public const string GameAnalyticsEventParameterNotFound = "game_analytics.event_parameter_not_found";
        public const string GameAnalyticsEventParametersMissing = "game_analytics.event_parameters_missing";
        public const string GameAnalyticsEventsNotFound = "game_analytics.events_not_found";
        public const string GameAnalyticsFunnelConditionsLimit = "game_analytics.funnel_conditions_limit";
        public const string GameAnalyticsFunnelNotFound = "game_analytics.funnel_not_found";
        public const string GameAnalyticsFunnelQueryFailed = "game_analytics.funnel_query_failed";
        public const string GameAnalyticsFunnelStepNotFound = "game_analytics.funnel_step_not_found";
        public const string GameAnalyticsFunnelStepsLimit = "game_analytics.funnel_steps_limit";
        public const string GameAnalyticsInvalidDashboardName = "game_analytics.invalid_dashboard_name";
        public const string GameAnalyticsInvalidDateRange = "game_analytics.invalid_date_range";
        public const string GameAnalyticsInvalidEventId = "game_analytics.invalid_event_id";
        public const string GameAnalyticsInvalidEventName = "game_analytics.invalid_event_name";
        public const string GameAnalyticsInvalidEventParameterValue = "game_analytics.invalid_event_parameter_value";
        public const string GameAnalyticsInvalidFunnelIdForWidget = "game_analytics.invalid_funnel_id_for_widget";
        public const string GameAnalyticsInvalidFunnelName = "game_analytics.invalid_funnel_name";
        public const string GameAnalyticsInvalidFunnelStepNumber = "game_analytics.invalid_funnel_step_number";
        public const string GameAnalyticsInvalidParameterDescription = "game_analytics.invalid_parameter_description";
        public const string GameAnalyticsInvalidParameterId = "game_analytics.invalid_parameter_id";
        public const string GameAnalyticsInvalidParameterName = "game_analytics.invalid_parameter_name";
        public const string GameAnalyticsInvalidProjectId = "game_analytics.invalid_project_id";
        public const string GameAnalyticsInvalidWidgetId = "game_analytics.invalid_widget_id";
        public const string GameAnalyticsInvalidWidgetName = "game_analytics.invalid_widget_name";
        public const string GameAnalyticsMetricKeyDuplicate = "game_analytics.metric_key_duplicate";
        public const string GameAnalyticsMetricKeyRequired = "game_analytics.metric_key_required";
        public const string GameAnalyticsMetricRequired = "game_analytics.metric_required";
        public const string GameAnalyticsMetricTemplateNotFound = "game_analytics.metric_template_not_found";
        public const string GameAnalyticsMetricValueRequired = "game_analytics.metric_value_required";
        public const string GameAnalyticsParameterNotInEvent = "game_analytics.parameter_not_in_event";
        public const string GameAnalyticsPlayerIdHeaderRequired = "game_analytics.player_id_header_required";
        public const string GameAnalyticsQuantileRange = "game_analytics.quantile_range";
        public const string GameAnalyticsQueryBuilderNotFound = "game_analytics.query_builder_not_found";
        public const string GameAnalyticsQueryFailed = "game_analytics.query_failed";
        public const string GameAnalyticsSerializationFailed = "game_analytics.serialization_failed";
        public const string GameAnalyticsSessionIdHeaderRequired = "game_analytics.session_id_header_required";
        public const string GameAnalyticsSystemEventNotDeletable = "game_analytics.system_event_not_deletable";
        public const string GameAnalyticsSystemEventNotRenamable = "game_analytics.system_event_not_renamable";
        public const string GameAnalyticsSystemFilterColumnRequired = "game_analytics.system_filter_column_required";
        public const string GameAnalyticsSystemFilterInvalidInteger = "game_analytics.system_filter_invalid_integer";
        public const string GameAnalyticsSystemFilterUnknownColumn = "game_analytics.system_filter_unknown_column";
        public const string GameAnalyticsUnknownEventParameter = "game_analytics.unknown_event_parameter";
        public const string GameAnalyticsWidgetFunnelRequired = "game_analytics.widget_funnel_required";
        public const string GameAnalyticsWidgetMetricRequired = "game_analytics.widget_metric_required";
        public const string GameAnalyticsWidgetMissing = "game_analytics.widget_missing";
        public const string GameAnalyticsWidgetNotFound = "game_analytics.widget_not_found";

        // === game_hosting ===
        public const string GameHostingArchiveDownloadFailed = "game_hosting.archive_download_failed";
        public const string GameHostingBuildEntryPointMissing = "game_hosting.build_entry_point_missing";
        public const string GameHostingBuildInUse = "game_hosting.build_in_use";
        public const string GameHostingBuildNotFound = "game_hosting.build_not_found";
        public const string GameHostingBuildNotReady = "game_hosting.build_not_ready";
        public const string GameHostingCacheKeyConflict = "game_hosting.cache_key_conflict";
        public const string GameHostingCacheKeyNotFound = "game_hosting.cache_key_not_found";
        public const string GameHostingDownloadLinkUnavailable = "game_hosting.download_link_unavailable";
        public const string GameHostingDownloadLinksUnavailable = "game_hosting.download_links_unavailable";
        public const string GameHostingEmptyGameName = "game_hosting.empty_game_name";
        public const string GameHostingGameDeleteFailed = "game_hosting.game_delete_failed";
        public const string GameHostingGameNotFound = "game_hosting.game_not_found";
        public const string GameHostingGameUpdateFailed = "game_hosting.game_update_failed";
        public const string GameHostingHistoryLimitReached = "game_hosting.history_limit_reached";
        public const string GameHostingHistoryTrimRequired = "game_hosting.history_trim_required";
        public const string GameHostingInsufficientStorage = "game_hosting.insufficient_storage";
        public const string GameHostingInvalidArchiveType = "game_hosting.invalid_archive_type";
        public const string GameHostingInvalidBuildLabel = "game_hosting.invalid_build_label";
        public const string GameHostingInvalidChannelParameter = "game_hosting.invalid_channel_parameter";
        public const string GameHostingInvalidChannelPattern = "game_hosting.invalid_channel_pattern";
        public const string GameHostingInvalidHistoryDeep = "game_hosting.invalid_history_deep";
        public const string GameHostingQuotaLookupFailed = "game_hosting.quota_lookup_failed";
        public const string GameHostingStagingBuildMissing = "game_hosting.staging_build_missing";
        public const string GameHostingUploadInProgress = "game_hosting.upload_in_progress";
        public const string GameHostingUploadQueueFull = "game_hosting.upload_queue_full";

        // === game_localization ===
        public const string GameLocalizationBranchNotEditable = "game_localization.branch_not_editable";
        public const string GameLocalizationCollectionNameInvalid = "game_localization.collection_name_invalid";
        public const string GameLocalizationCollectionNotFound = "game_localization.collection_not_found";
        public const string GameLocalizationCollectionNotInBranch = "game_localization.collection_not_in_branch";
        public const string GameLocalizationDefaultGroupInitializationFailed = "game_localization.default_group_initialization_failed";
        public const string GameLocalizationDefaultGroupNotDeletable = "game_localization.default_group_not_deletable";
        public const string GameLocalizationGroupNameInvalid = "game_localization.group_name_invalid";
        public const string GameLocalizationGroupNotFound = "game_localization.group_not_found";
        public const string GameLocalizationGroupNotInBranch = "game_localization.group_not_in_branch";
        public const string GameLocalizationKeyNameInvalid = "game_localization.key_name_invalid";
        public const string GameLocalizationLanguageNotEnabled = "game_localization.language_not_enabled";
        public const string GameLocalizationLanguageValueNotFound = "game_localization.language_value_not_found";
        public const string GameLocalizationLastLanguageRemoval = "game_localization.last_language_removal";
        public const string GameLocalizationLocalizationCollectionMismatch = "game_localization.localization_collection_mismatch";
        public const string GameLocalizationLocalizationKeyNotFound = "game_localization.localization_key_not_found";
        public const string GameLocalizationLocalizationKeyNotInBranch = "game_localization.localization_key_not_in_branch";
        public const string GameLocalizationLocalizationTextInvalid = "game_localization.localization_text_invalid";
        public const string GameLocalizationSettingsInitializationFailed = "game_localization.settings_initialization_failed";
        public const string GameLocalizationSettingsNotFound = "game_localization.settings_not_found";
        public const string GameLocalizationTranslationEmptyTargets = "game_localization.translation_empty_targets";
        public const string GameLocalizationTranslationFailed = "game_localization.translation_failed";

        // === groups ===
        public const string GroupsAlreadyMember = "groups.already_member";
        public const string GroupsCannotAssignOwnerRole = "groups.cannot_assign_owner_role";
        public const string GroupsCannotKickOwner = "groups.cannot_kick_owner";
        public const string GroupsDefaultMemberRoleImmutable = "groups.default_member_role_immutable";
        public const string GroupsDefaultMemberRoleNotFound = "groups.default_member_role_not_found";
        public const string GroupsGroupAlreadyHasChat = "groups.group_already_has_chat";
        public const string GroupsGroupFull = "groups.group_full";
        public const string GroupsGroupNotFound = "groups.group_not_found";
        public const string GroupsInsufficientPermissions = "groups.insufficient_permissions";
        public const string GroupsInviteExpired = "groups.invite_expired";
        public const string GroupsInviteKeyExpired = "groups.invite_key_expired";
        public const string GroupsInviteKeyInvalid = "groups.invite_key_invalid";
        public const string GroupsInviteKeyNotFound = "groups.invite_key_not_found";
        public const string GroupsInviteNotFound = "groups.invite_not_found";
        public const string GroupsInviteNotPending = "groups.invite_not_pending";
        public const string GroupsInviteWrongTarget = "groups.invite_wrong_target";
        public const string GroupsJoinPolicyNotOpen = "groups.join_policy_not_open";
        public const string GroupsJoinPolicyNotRequest = "groups.join_policy_not_request";
        public const string GroupsJoinRequestNotFound = "groups.join_request_not_found";
        public const string GroupsMaxMembersInvalid = "groups.max_members_invalid";
        public const string GroupsMemberNotFound = "groups.member_not_found";
        public const string GroupsNameRequired = "groups.name_required";
        public const string GroupsNotMember = "groups.not_member";
        public const string GroupsOwnerCannotLeave = "groups.owner_cannot_leave";
        public const string GroupsOwnerImmutable = "groups.owner_immutable";
        public const string GroupsOwnerRoleNotFound = "groups.owner_role_not_found";
        public const string GroupsPendingInviteExists = "groups.pending_invite_exists";
        public const string GroupsPendingRequestExists = "groups.pending_request_exists";
        public const string GroupsPermissionsDeserializationFailed = "groups.permissions_deserialization_failed";
        public const string GroupsPlayerBanned = "groups.player_banned";
        public const string GroupsRequestExpired = "groups.request_expired";
        public const string GroupsRequestNotPending = "groups.request_not_pending";
        public const string GroupsRoleNotFound = "groups.role_not_found";
        public const string GroupsRoleNotSpecified = "groups.role_not_specified";
        public const string GroupsTargetNotMember = "groups.target_not_member";

        // === leaderboards ===
        public const string LeaderboardsParticipationRequired = "leaderboards.participation_required";

        // === platforms ===
        public const string PlatformsConsentStoreUnavailable = "platforms.consent_store_unavailable";
        public const string PlatformsInvalidPlatformName = "platforms.invalid_platform_name";
        public const string PlatformsLegalDocumentAlreadyPublished = "platforms.legal_document_already_published";
        public const string PlatformsLegalDocumentCustomKeyRequired = "platforms.legal_document_custom_key_required";
        public const string PlatformsLegalDocumentDuplicate = "platforms.legal_document_duplicate";
        public const string PlatformsLegalDocumentLocaleRequired = "platforms.legal_document_locale_required";
        public const string PlatformsLegalDocumentNotPublished = "platforms.legal_document_not_published";
        public const string PlatformsLegalDocumentPublished = "platforms.legal_document_published";
        public const string PlatformsLegalDocumentTitleRequired = "platforms.legal_document_title_required";
        public const string PlatformsLegalDocumentWrongPlatform = "platforms.legal_document_wrong_platform";
        public const string PlatformsLegalInfoCompanyNameRequired = "platforms.legal_info_company_name_required";
        public const string PlatformsLegalInfoContactEmailRequired = "platforms.legal_info_contact_email_required";
        public const string PlatformsLegalInfoNotConfigured = "platforms.legal_info_not_configured";
        public const string PlatformsMarketplaceSettingsTypeMismatch = "platforms.marketplace_settings_type_mismatch";
        public const string PlatformsMarketplaceTypeMismatch = "platforms.marketplace_type_mismatch";
        public const string PlatformsPlatformInUse = "platforms.platform_in_use";
        public const string PlatformsPlatformNameDuplicate = "platforms.platform_name_duplicate";

        // === player_accounts ===
        public const string PlayerAccountsAccountDoesNotExist = "player_accounts.account_does_not_exist";
        public const string PlayerAccountsAccountNotFound = "player_accounts.account_not_found";
        public const string PlayerAccountsAuthBelongsToAnotherAccount = "player_accounts.auth_belongs_to_another_account";
        public const string PlayerAccountsAuthIssuerPipelineEmpty = "player_accounts.auth_issuer_pipeline_empty";
        public const string PlayerAccountsAuthRecordNotFound = "player_accounts.auth_record_not_found";
        public const string PlayerAccountsAuthRedirectUrlMissing = "player_accounts.auth_redirect_url_missing";
        public const string PlayerAccountsBranchEnvironmentMissing = "player_accounts.branch_environment_missing";
        public const string PlayerAccountsBranchProjectMismatch = "player_accounts.branch_project_mismatch";
        public const string PlayerAccountsCacheKeyExists = "player_accounts.cache_key_exists";
        public const string PlayerAccountsCacheKeyMissing = "player_accounts.cache_key_missing";
        public const string PlayerAccountsCacheTtlInvalid = "player_accounts.cache_ttl_invalid";
        public const string PlayerAccountsDeviceIdRequired = "player_accounts.device_id_required";
        public const string PlayerAccountsEmailRequired = "player_accounts.email_required";
        public const string PlayerAccountsExternalAuthCertificateInvalid = "player_accounts.external_auth_certificate_invalid";
        public const string PlayerAccountsExternalAuthExchangeFailed = "player_accounts.external_auth_exchange_failed";
        public const string PlayerAccountsExternalAuthInvalidIdToken = "player_accounts.external_auth_invalid_id_token";
        public const string PlayerAccountsExternalAuthInvalidPayload = "player_accounts.external_auth_invalid_payload";
        public const string PlayerAccountsExternalAuthInvalidSignature = "player_accounts.external_auth_invalid_signature";
        public const string PlayerAccountsExternalAuthMisconfigured = "player_accounts.external_auth_misconfigured";
        public const string PlayerAccountsExternalAvatarDomainNotAllowed = "player_accounts.external_avatar_domain_not_allowed";
        public const string PlayerAccountsExternalAvatarMalformed = "player_accounts.external_avatar_malformed";
        public const string PlayerAccountsExternalAvatarPatternInvalid = "player_accounts.external_avatar_pattern_invalid";
        public const string PlayerAccountsExternalUserIdRequired = "player_accounts.external_user_id_required";
        public const string PlayerAccountsGuestIdRequired = "player_accounts.guest_id_required";
        public const string PlayerAccountsImageFetchFailed = "player_accounts.image_fetch_failed";
        public const string PlayerAccountsImageNotFound = "player_accounts.image_not_found";
        public const string PlayerAccountsImageUploadFailed = "player_accounts.image_upload_failed";
        public const string PlayerAccountsInvalidCredentials = "player_accounts.invalid_credentials";
        public const string PlayerAccountsInvalidProjectId = "player_accounts.invalid_project_id";
        public const string PlayerAccountsLastAuthMethod = "player_accounts.last_auth_method";
        public const string PlayerAccountsLoginRequired = "player_accounts.login_required";
        public const string PlayerAccountsMarketplaceSettingsMissing = "player_accounts.marketplace_settings_missing";
        public const string PlayerAccountsMarketplaceUnsupported = "player_accounts.marketplace_unsupported";
        public const string PlayerAccountsNicknamePatternInvalid = "player_accounts.nickname_pattern_invalid";
        public const string PlayerAccountsNicknamePatternMismatch = "player_accounts.nickname_pattern_mismatch";
        public const string PlayerAccountsNicknameProfanity = "player_accounts.nickname_profanity";
        public const string PlayerAccountsNicknameRequired = "player_accounts.nickname_required";
        public const string PlayerAccountsNicknameTaken = "player_accounts.nickname_taken";
        public const string PlayerAccountsUsernamePatternInvalid = "player_accounts.username_pattern_invalid";
        public const string PlayerAccountsUsernamePatternMismatch = "player_accounts.username_pattern_mismatch";
        public const string PlayerAccountsUsernameProfanity = "player_accounts.username_profanity";
        public const string PlayerAccountsUsernameRequired = "player_accounts.username_required";
        public const string PlayerAccountsUsernameTaken = "player_accounts.username_taken";
        public const string PlayerAccountsPlatformDisabled = "player_accounts.platform_disabled";
        public const string PlayerAccountsPlatformIdInvalid = "player_accounts.platform_id_invalid";
        public const string PlayerAccountsPlatformNotFound = "player_accounts.platform_not_found";
        public const string PlayerAccountsPlayerRoleKeyExists = "player_accounts.player_role_key_exists";
        public const string PlayerAccountsPlayerRoleKeyInvalid = "player_accounts.player_role_key_invalid";
        public const string PlayerAccountsPlayerRoleNameRequired = "player_accounts.player_role_name_required";
        public const string PlayerAccountsPlayerRoleNotFound = "player_accounts.player_role_not_found";
        public const string PlayerAccountsProfileNotFound = "player_accounts.profile_not_found";
        public const string PlayerAccountsProviderDocumentTypeMismatch = "player_accounts.provider_document_type_mismatch";
        public const string PlayerAccountsProviderDuplicate = "player_accounts.provider_duplicate";
        public const string PlayerAccountsProviderHandlerMissing = "player_accounts.provider_handler_missing";
        public const string PlayerAccountsProviderMisconfigured = "player_accounts.provider_misconfigured";
        public const string PlayerAccountsProviderNotEnabled = "player_accounts.provider_not_enabled";
        public const string PlayerAccountsProviderNotFound = "player_accounts.provider_not_found";
        public const string PlayerAccountsRandomCountOutOfRange = "player_accounts.random_count_out_of_range";
        public const string PlayerAccountsRefreshTokenRequired = "player_accounts.refresh_token_required";
        public const string PlayerAccountsRepositoryFailure = "player_accounts.repository_failure";
        public const string PlayerAccountsSessionExpired = "player_accounts.session_expired";
        public const string PlayerAccountsSessionMismatch = "player_accounts.session_mismatch";
        public const string PlayerAccountsSessionNotFound = "player_accounts.session_not_found";
        public const string PlayerAccountsSessionProjectMismatch = "player_accounts.session_project_mismatch";
        public const string PlayerAccountsSettingsUniqueRelock = "player_accounts.settings_unique_relock";
        public const string PlayerAccountsSettingsValidation = "player_accounts.settings_validation";
        public const string PlayerAccountsSuccessUrlScheme = "player_accounts.success_url_scheme";
        public const string PlayerAccountsTestAccountCredentialsRequired = "player_accounts.test_account_credentials_required";
        public const string PlayerAccountsTestAccountLimitReached = "player_accounts.test_account_limit_reached";
        public const string PlayerAccountsUnsupportedProviderType = "player_accounts.unsupported_provider_type";
        public const string PlayerAccountsUserIdRequired = "player_accounts.user_id_required";

        // === profanity_filter ===
        public const string ProfanityFilterDefaultGroupNameReserved = "profanity_filter.default_group_name_reserved";
        public const string ProfanityFilterDefaultGroupNotDeletable = "profanity_filter.default_group_not_deletable";
        public const string ProfanityFilterDefaultGroupNotRenamable = "profanity_filter.default_group_not_renamable";
        public const string ProfanityFilterGroupNameRequired = "profanity_filter.group_name_required";
        public const string ProfanityFilterGroupNameTooLong = "profanity_filter.group_name_too_long";
        public const string ProfanityFilterGroupNotFound = "profanity_filter.group_not_found";
        public const string ProfanityFilterWordEmpty = "profanity_filter.word_empty";
        public const string ProfanityFilterWordRequired = "profanity_filter.word_required";
        public const string ProfanityFilterWordTooLong = "profanity_filter.word_too_long";

        // === project_statistics ===
        public const string ProjectStatisticsInvalidOrganizationId = "project_statistics.invalid_organization_id";
        public const string ProjectStatisticsInvalidPeriod = "project_statistics.invalid_period";
        public const string ProjectStatisticsMessageDeserializationFailed = "project_statistics.message_deserialization_failed";
        public const string ProjectStatisticsMessageEmpty = "project_statistics.message_empty";
        public const string ProjectStatisticsMetricsForDateNotFound = "project_statistics.metrics_for_date_not_found";
        public const string ProjectStatisticsMetricsRoutesMissing = "project_statistics.metrics_routes_missing";
        public const string ProjectStatisticsMetricsUpdateFailed = "project_statistics.metrics_update_failed";
        public const string ProjectStatisticsOrganizationProjectsNotFound = "project_statistics.organization_projects_not_found";
        public const string ProjectStatisticsProjectIdRequired = "project_statistics.project_id_required";
        public const string ProjectStatisticsRouteNotFound = "project_statistics.route_not_found";

        // === purchases ===
        public const string PurchasesNonConsumableAlreadyOwned = "purchases.non_consumable_already_owned";
        public const string PurchasesOrderAlreadyCompleted = "purchases.order_already_completed";
        public const string PurchasesOrderIdInvalid = "purchases.order_id_invalid";
        public const string PurchasesOrderInvalidStatus = "purchases.order_invalid_status";
        public const string PurchasesOrderNotFound = "purchases.order_not_found";
        public const string PurchasesPaymentProviderError = "purchases.payment_provider_error";
        public const string PurchasesPlatformIdInvalid = "purchases.platform_id_invalid";
        public const string PurchasesProviderConfigIdInvalid = "purchases.provider_config_id_invalid";
        public const string PurchasesProviderConfigNotActive = "purchases.provider_config_not_active";
        public const string PurchasesProviderConfigNotFound = "purchases.provider_config_not_found";
        public const string PurchasesProviderConfigWrongType = "purchases.provider_config_wrong_type";
        public const string PurchasesProviderMappingAlreadyExists = "purchases.provider_mapping_already_exists";
        public const string PurchasesProviderMappingNotActive = "purchases.provider_mapping_not_active";
        public const string PurchasesProviderMappingNotFound = "purchases.provider_mapping_not_found";
        public const string PurchasesProviderUnsupported = "purchases.provider_unsupported";
        public const string PurchasesPurchaseConfigAlreadyExists = "purchases.purchase_config_already_exists";
        public const string PurchasesPurchaseConfigIdInvalid = "purchases.purchase_config_id_invalid";
        public const string PurchasesPurchaseConfigInvalidType = "purchases.purchase_config_invalid_type";
        public const string PurchasesPurchaseConfigNotActive = "purchases.purchase_config_not_active";
        public const string PurchasesPurchaseConfigNotFound = "purchases.purchase_config_not_found";
        public const string PurchasesPurchaseKeyRequired = "purchases.purchase_key_required";
        public const string PurchasesRedirectUrlsRequired = "purchases.redirect_urls_required";
        public const string PurchasesRedisFailure = "purchases.redis_failure";
        public const string PurchasesSelectedProfileRequired = "purchases.selected_profile_required";
        public const string PurchasesStripeNoActiveProvider = "purchases.stripe_no_active_provider";
        public const string PurchasesStripeSessionMetadataInvalid = "purchases.stripe_session_metadata_invalid";
        public const string PurchasesStripeSessionNoPaymentIntent = "purchases.stripe_session_no_payment_intent";
        public const string PurchasesStripeSignatureInvalid = "purchases.stripe_signature_invalid";
        public const string PurchasesStripeSignatureMissing = "purchases.stripe_signature_missing";
        public const string PurchasesStripeUnexpectedPayload = "purchases.stripe_unexpected_payload";
        public const string PurchasesSubscriptionNotFound = "purchases.subscription_not_found";
        public const string PurchasesYookassaEmptyEvent = "purchases.yookassa_empty_event";
        public const string PurchasesYookassaInvalidPlayerId = "purchases.yookassa_invalid_player_id";
        public const string PurchasesYookassaInvalidResponse = "purchases.yookassa_invalid_response";
        public const string PurchasesYookassaMissingEvent = "purchases.yookassa_missing_event";
        public const string PurchasesYookassaMissingObject = "purchases.yookassa_missing_object";
        public const string PurchasesYookassaNoOrderOrSubscriptionRef = "purchases.yookassa_no_order_or_subscription_ref";
        public const string PurchasesYookassaPaymentCreationFailed = "purchases.yookassa_payment_creation_failed";
        public const string PurchasesYookassaRefundFailed = "purchases.yookassa_refund_failed";

        // === rules_constructor ===
        public const string RulesConstructorBranchIdRequired = "rules_constructor.branch_id_required";
        public const string RulesConstructorInvalidRuleId = "rules_constructor.invalid_rule_id";
        public const string RulesConstructorNodeChildRequired = "rules_constructor.node_child_required";
        public const string RulesConstructorRuleNotFound = "rules_constructor.rule_not_found";
        public const string RulesConstructorRuleNotModified = "rules_constructor.rule_not_modified";
        public const string RulesConstructorTreeDepthExceeded = "rules_constructor.tree_depth_exceeded";
        public const string RulesConstructorTreeNodeInvalid = "rules_constructor.tree_node_invalid";

        // === segments ===
        public const string SegmentsCacheKeyConflict = "segments.cache_key_conflict";
        public const string SegmentsCacheKeyNotFound = "segments.cache_key_not_found";
        public const string SegmentsConfigNotFound = "segments.config_not_found";
        public const string SegmentsInvalidRuleId = "segments.invalid_rule_id";
        public const string SegmentsInvalidSegmentName = "segments.invalid_segment_name";
        public const string SegmentsSegmentNotFound = "segments.segment_not_found";

        // === tariffs ===
        public const string TariffsAccountIdRequired = "tariffs.account_id_required";
        public const string TariffsDefaultPlanProtected = "tariffs.default_plan_protected";
        public const string TariffsInvalidDeltaBytes = "tariffs.invalid_delta_bytes";
        public const string TariffsInvalidOrganizationId = "tariffs.invalid_organization_id";
        public const string TariffsInvalidPlanId = "tariffs.invalid_plan_id";
        public const string TariffsInvalidProjectId = "tariffs.invalid_project_id";
        public const string TariffsInvalidUsageBytes = "tariffs.invalid_usage_bytes";
        public const string TariffsOrganizationIdRequired = "tariffs.organization_id_required";
        public const string TariffsPlanKeyConflict = "tariffs.plan_key_conflict";
        public const string TariffsPlanKeyRequired = "tariffs.plan_key_required";
        public const string TariffsPlanNotFoundById = "tariffs.plan_not_found_by_id";
        public const string TariffsPlanNotFoundByKey = "tariffs.plan_not_found_by_key";
        public const string TariffsPlanParametersInvalid = "tariffs.plan_parameters_invalid";
        public const string TariffsProjectIdRequired = "tariffs.project_id_required";
        public const string TariffsProjectNotFound = "tariffs.project_not_found";
        public const string TariffsProjectsNotFoundForOrganization = "tariffs.projects_not_found_for_organization";
        public const string TariffsTariffCreateFailed = "tariffs.tariff_create_failed";
        public const string TariffsTariffNotFoundForOrganization = "tariffs.tariff_not_found_for_organization";

        // === tournaments ===
        public const string TournamentsCohortSizeInvalid = "tournaments.cohort_size_invalid";
        public const string TournamentsConfigDeleteFailed = "tournaments.config_delete_failed";
        public const string TournamentsConfigNotFound = "tournaments.config_not_found";
        public const string TournamentsConfigUpdateFailed = "tournaments.config_update_failed";
        public const string TournamentsEmptyPlayerName = "tournaments.empty_player_name";
        public const string TournamentsEmptyTables = "tournaments.empty_tables";
        public const string TournamentsInvalidFriendId = "tournaments.invalid_friend_id";
        public const string TournamentsLeagueMetaNotFound = "tournaments.league_meta_not_found";
        public const string TournamentsMissingCountry = "tournaments.missing_country";
        public const string TournamentsMissingTableId = "tournaments.missing_table_id";
        public const string TournamentsParticipationRequired = "tournaments.participation_required";
        public const string TournamentsPersistenceFailed = "tournaments.persistence_failed";
        public const string TournamentsPlayerCohortMissing = "tournaments.player_cohort_missing";
        public const string TournamentsPlayerEntryNotFound = "tournaments.player_entry_not_found";
        public const string TournamentsPlayerNotInCohort = "tournaments.player_not_in_cohort";
    }
}
