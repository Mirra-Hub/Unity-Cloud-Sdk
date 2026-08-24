using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MirraCloud.Core.Chats.Dto;
using MirraCloud.Core.Chats.Events;
using MirraCloud.Core.Chats.Models;
using MirraCloud.Core.Logger;
using MirraCloud.Core.Realtime.Abstractions;
using MirraCloud.Core.Realtime.Auth;
using MirraCloud.Core.Chats.Handlers;
using MirraCloud.Core.Realtime.Connection;
using MirraCloud.Core.Realtime.Dispatching;
using MirraCloud.Core.Realtime.Protocol;
using MirraCloud.Core.Realtime.Transport;
using MirraCloud.Json;
using Plugins.MirraCloud.Core.General.AsyncOperations;
using Plugins.MirraCloud.Core.General.LifeCycle;
using UnityEngine.Networking;

namespace MirraCloud.Core.Chats
{
    public sealed class ChatsService : ICloudSdkService
    {
        private const string ControllerApi = "/chats/v1/projects";
        private const int MaxHistoryPageSize = 100;
        private const int MaxRecoveryPages = 1000;
        private const int HeartbeatIntervalMs = 15000;

        private readonly Configuration _configuration;
        private readonly ILogger _logger;
        private readonly RestApiClient _restApi;
        private readonly IJsonService _jsonService;
        private readonly IRealtimeAuthProvider _realtimeAuthProvider;
        private readonly IRealtimeSubscriptionStore _subscriptions;
        private readonly IRealtimeConnection _connection;
        private readonly RealtimeReconnectPolicy _reconnectPolicy = new RealtimeReconnectPolicy();
        private readonly Dictionary<string, long> _lastSeenMessageByChannel = new Dictionary<string, long>();
        private readonly RealtimeEventDispatcher _eventDispatcher;

        private bool _shouldBeConnected;
        private bool _reconnectInProgress;
        private CancellationTokenSource _heartbeatCts;

        public event Action<RealtimeConnectionState> OnConnectionStateChanged;
        public event Action<string> OnSubscribedChannel;
        public event Action<ChatMessageDto> OnMessageReceived;
        public event Action<ChatMessageDto> OnMessageEdited;
        public event Action<RealtimeDeletePayload> OnMessageDeleted;
        public event Action<ChatMemberEvent> OnMemberAdded;
        public event Action<ChatMemberEvent> OnMemberRemoved;
        public event Action<ChatMemberBannedEvent> OnMemberBanned;
        public event Action<string> OnChannelDeleted;
        public event Action<ChatErrorEvent> OnError;

        public ChatsService(
            Configuration configuration,
            ILogger logger,
            RestApiClient restApi,
            IJsonService jsonService,
            ICoroutineRunner coroutineRunner)
        {
            _configuration = configuration;
            _logger = logger;
            _restApi = restApi;
            _jsonService = jsonService;

            _realtimeAuthProvider = new ChatsRealtimeAuthProvider(_restApi, _configuration);
            _subscriptions = new RealtimeSubscriptionStore();

            _connection = new RealtimeConnection(
                RealtimeTransportFactory.Create(logger, coroutineRunner),
                new JsonRealtimeSerializer(jsonService),
                new RealtimeRequestTracker(),
                logger);

            _eventDispatcher = CreateEventDispatcher();
        }

        public void CloudSdkInitialize()
        {
            _connection.Initialize();

            _connection.OnStateChanged += HandleConnectionStateChanged;
            _connection.OnEvent += HandleRealtimeEvent;
            _connection.OnError += HandleRealtimeError;
        }

        public void CloudSdkDispose()
        {
            _connection.OnStateChanged -= HandleConnectionStateChanged;
            _connection.OnEvent -= HandleRealtimeEvent;
            _connection.OnError -= HandleRealtimeError;

            _connection.Dispose();

            _shouldBeConnected = false;
            StopHeartbeat();
            _connection.Disconnect();
        }

        /// <summary>
        /// Creates a chat (a room channel) from a chat template and joins the caller as its first
        /// member. <paramref name="templateKey"/> is required and must reference an existing template.
        /// </summary>
        public AsyncOperation<RestApiResult<ChatChannelDto>> CreateChannelAsync(
            string name, string templateKey, string topic = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return AsyncOperation<RestApiResult<ChatChannelDto>>.CreateCompleted(
                    RestApiResult<ChatChannelDto>.ValidationFail("name is required."));
            }

            if (string.IsNullOrWhiteSpace(templateKey))
            {
                return AsyncOperation<RestApiResult<ChatChannelDto>>.CreateCompleted(
                    RestApiResult<ChatChannelDto>.ValidationFail("templateKey is required."));
            }

            var route = $"{ControllerApi}/{_configuration.ProjectId}/channels";
            return _restApi.PostAsync<ChatChannelDto>(route, new CreateChatChannelDto
            {
                Name = name,
                Topic = topic,
                TemplateKey = templateKey
            });
        }

        public AsyncOperation<RestApiResult<ChatChannelDto>> LookupGroupChannelAsync(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return AsyncOperation<RestApiResult<ChatChannelDto>>.CreateCompleted(
                    RestApiResult<ChatChannelDto>.ValidationFail("groupId is required."));
            }

            var route =
                $"{ControllerApi}/{_configuration.ProjectId}/channels/lookup?ownerRefType=group&ownerRefId={UnityWebRequest.EscapeURL(groupId)}";
            return _restApi.GetAsync<ChatChannelDto>(route);
        }

        public AsyncOperation<RestApiResult<ChatChannelDto>> GetChannelAsync(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                return AsyncOperation<RestApiResult<ChatChannelDto>>.CreateCompleted(
                    RestApiResult<ChatChannelDto>.ValidationFail("channelId is required."));
            }

            var route = $"{ControllerApi}/{_configuration.ProjectId}/channels/{channelId}";
            return _restApi.GetAsync<ChatChannelDto>(route);
        }

        public AsyncOperation<RestApiResult<ChatMemberDto[]>> GetMembersAsync(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                return AsyncOperation<RestApiResult<ChatMemberDto[]>>.CreateCompleted(
                    RestApiResult<ChatMemberDto[]>.ValidationFail("channelId is required."));
            }

            var route = $"{ControllerApi}/{_configuration.ProjectId}/channels/{channelId}/members";
            return _restApi.GetAsync<ChatMemberDto[]>(route);
        }

        public AsyncOperation<RestApiResult<ChatMessageDto[]>> GetMessagesAsync(
            string channelId,
            long? before = null,
            long? after = null,
            int limit = 100,
            string targetMessageId = null)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                return AsyncOperation<RestApiResult<ChatMessageDto[]>>.CreateCompleted(
                    RestApiResult<ChatMessageDto[]>.ValidationFail("channelId is required."));
            }

            var safeLimit = limit < 1 ? 1 : limit > MaxHistoryPageSize ? MaxHistoryPageSize : limit;
            var route =
                $"{ControllerApi}/{_configuration.ProjectId}/channels/{channelId}/messages?limit={safeLimit}";

            if (before.HasValue)
                route += $"&before={before.Value}";

            if (after.HasValue)
                route += $"&after={after.Value}";

            if (!string.IsNullOrWhiteSpace(targetMessageId))
                route += $"&targetId={UnityWebRequest.EscapeURL(targetMessageId)}";

            return _restApi.GetAsync<ChatMessageDto[]>(route);
        }

        public AsyncOperation<RealtimeResult> ConnectAsync()
        {
            _shouldBeConnected = true;

            if (_connection.State == RealtimeConnectionState.Connected ||
                _connection.State == RealtimeConnectionState.Connecting)
            {
                return AsyncOperation<RealtimeResult>.CreateCompleted(RealtimeResult.Success());
            }

            if (_reconnectInProgress)
            {
                return AsyncOperation<RealtimeResult>.CreateCompleted(RealtimeResult.Success());
            }

            var op = new AsyncOperation<RealtimeResult>();

            _logger.Log("[Chats] Creating RT session...");
            var sessionOp = _realtimeAuthProvider.CreateSessionAsync();
            sessionOp.UseCompleted(_ =>
            {
                _logger.Log($"[Chats] RT session result: success={sessionOp.Result.IsSuccess}, hasData={sessionOp.Result.Data != null}");

                if (!sessionOp.Result.IsSuccess || sessionOp.Result.Data == null ||
                    string.IsNullOrWhiteSpace(BuildWsUrl(sessionOp.Result.Data)))
                {
                    _shouldBeConnected = false;
                    var error = sessionOp.Result.Error;
                    _logger.Error($"[Chats] RT session failed: {error?.Message}");
                    op.Complete(RealtimeResult.Fail("session_failed", error?.Message ?? "Failed to create realtime session."));
                    return;
                }

                ConnectInternal(sessionOp.Result.Data, op);
            });

            return op;
        }

        public AsyncOperation<RealtimeResult> DisconnectAsync()
        {
            _shouldBeConnected = false;
            _subscriptions.Clear();
            StopHeartbeat();

            var op = new AsyncOperation<RealtimeResult>();
            var disconnectOp = _connection.Disconnect();
            disconnectOp.UseCompleted(_ =>
            {
                if (disconnectOp.Result.IsSuccess)
                    op.Complete(RealtimeResult.Success());
                else
                    op.Complete(RealtimeResult.Fail("disconnect_failed",
                        disconnectOp.Result.Message ?? "Disconnect failed"));
            });

            return op;
        }

        public AsyncOperation<RealtimeResult> SubscribeAsync(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
                return AsyncOperation<RealtimeResult>.CreateCompleted(
                    RealtimeResult.Fail("validation", "channelId is required."));

            if (!_connection.IsConnected)
                return AsyncOperation<RealtimeResult>.CreateCompleted(
                    RealtimeResult.Fail("not_connected", "Realtime connection is not established."));

            var op = new AsyncOperation<RealtimeResult>();
            var commandOp = _connection.SendCommand("subscribe", new RealtimeAckChannelPayload
            {
                RoomId = channelId
            });

            commandOp.UseCompleted(_ =>
            {
                if (!commandOp.Result.IsSuccess)
                {
                    op.Complete(RealtimeResult.Fail(commandOp.Result.Code,
                        commandOp.Result.Message ?? "Subscribe failed"));
                    return;
                }

                _subscriptions.Add(channelId);
                op.Complete(RealtimeResult.Success());
            });

            return op;
        }

        public AsyncOperation<RealtimeResult> UnsubscribeAsync(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
                return AsyncOperation<RealtimeResult>.CreateCompleted(
                    RealtimeResult.Fail("validation", "channelId is required."));

            if (!_connection.IsConnected)
                return AsyncOperation<RealtimeResult>.CreateCompleted(
                    RealtimeResult.Fail("not_connected", "Realtime connection is not established."));

            var op = new AsyncOperation<RealtimeResult>();
            var commandOp = _connection.SendCommand("unsubscribe", new RealtimeAckChannelPayload
            {
                RoomId = channelId
            });

            commandOp.UseCompleted(_ =>
            {
                if (!commandOp.Result.IsSuccess)
                {
                    op.Complete(RealtimeResult.Fail(commandOp.Result.Code,
                        commandOp.Result.Message ?? "Unsubscribe failed"));
                    return;
                }

                _subscriptions.Remove(channelId);
                op.Complete(RealtimeResult.Success());
            });

            return op;
        }

        public AsyncOperation<RestApiResult> JoinAsync(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                return AsyncOperation<RestApiResult>.CreateCompleted(
                    RestApiResult.ValidationFail("channelId is required."));
            }

            var route = $"{ControllerApi}/{_configuration.ProjectId}/channels/{channelId}/members";
            return _restApi.PostAsync(route);
        }

        public AsyncOperation<RestApiResult> LeaveAsync(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                return AsyncOperation<RestApiResult>.CreateCompleted(
                    RestApiResult.ValidationFail("channelId is required."));
            }

            var profileId = "self";
            var route = $"{ControllerApi}/{_configuration.ProjectId}/channels/{channelId}/members/{profileId}";
            return _restApi.DeleteAsync(route);
        }

        public AsyncOperation<RealtimeResult<ChatMessageDto>> SendMessageAsync(
            string channelId,
            string body,
            object metadata = null,
            string[] taggedProfileIds = null,
            string targetMessageId = null)
        {
            if (string.IsNullOrWhiteSpace(channelId))
                return AsyncOperation<RealtimeResult<ChatMessageDto>>.CreateCompleted(
                    RealtimeResult<ChatMessageDto>.Fail("validation", "channelId is required."));

            if (!_connection.IsConnected)
                return AsyncOperation<RealtimeResult<ChatMessageDto>>.CreateCompleted(
                    RealtimeResult<ChatMessageDto>.Fail("not_connected", "Realtime connection is not established."));

            var op = new AsyncOperation<RealtimeResult<ChatMessageDto>>();
            var commandOp = _connection.SendCommand("sendMessage", new
            {
                roomId = channelId,
                Body = body ?? string.Empty,
                Metadata = ToJsonValue(metadata),
                TaggedProfileIds = taggedProfileIds ?? Array.Empty<string>(),
                TargetMessageId = targetMessageId ?? string.Empty
            });

            commandOp.UseCompleted(_ =>
            {
                if (!commandOp.Result.IsSuccess)
                {
                    op.Complete(RealtimeResult<ChatMessageDto>.Fail(commandOp.Result.Code,
                        commandOp.Result.Message ?? "Send failed"));
                    return;
                }

                var dto = Deserialize<ChatMessageDto>(commandOp.Result.Payload);
                TrackLastSeen(dto);
                op.Complete(RealtimeResult<ChatMessageDto>.Success(dto));
            });

            return op;
        }

        public AsyncOperation<RealtimeResult<ChatMessageDto>> EditMessageAsync(
            string channelId,
            string messageId,
            string body,
            object metadata = null,
            string[] taggedProfileIds = null)
        {
            if (string.IsNullOrWhiteSpace(channelId))
                return AsyncOperation<RealtimeResult<ChatMessageDto>>.CreateCompleted(
                    RealtimeResult<ChatMessageDto>.Fail("validation", "channelId is required."));

            if (string.IsNullOrWhiteSpace(messageId))
                return AsyncOperation<RealtimeResult<ChatMessageDto>>.CreateCompleted(
                    RealtimeResult<ChatMessageDto>.Fail("validation", "messageId is required."));

            if (!_connection.IsConnected)
                return AsyncOperation<RealtimeResult<ChatMessageDto>>.CreateCompleted(
                    RealtimeResult<ChatMessageDto>.Fail("not_connected", "Realtime connection is not established."));

            var op = new AsyncOperation<RealtimeResult<ChatMessageDto>>();
            var commandOp = _connection.SendCommand("editMessage", new
            {
                roomId = channelId,
                MessageId = messageId,
                Body = body ?? string.Empty,
                Metadata = ToJsonValue(metadata),
                TaggedProfileIds = taggedProfileIds ?? Array.Empty<string>()
            });

            commandOp.UseCompleted(_ =>
            {
                if (!commandOp.Result.IsSuccess)
                {
                    op.Complete(RealtimeResult<ChatMessageDto>.Fail(commandOp.Result.Code,
                        commandOp.Result.Message ?? "Edit failed"));
                    return;
                }

                var dto = Deserialize<ChatMessageDto>(commandOp.Result.Payload);
                TrackLastSeen(dto);
                op.Complete(RealtimeResult<ChatMessageDto>.Success(dto));
            });

            return op;
        }

        public AsyncOperation<RealtimeResult> DeleteMessageAsync(string channelId, string messageId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
                return AsyncOperation<RealtimeResult>.CreateCompleted(
                    RealtimeResult.Fail("validation", "channelId is required."));

            if (string.IsNullOrWhiteSpace(messageId))
                return AsyncOperation<RealtimeResult>.CreateCompleted(
                    RealtimeResult.Fail("validation", "messageId is required."));

            if (!_connection.IsConnected)
                return AsyncOperation<RealtimeResult>.CreateCompleted(
                    RealtimeResult.Fail("not_connected", "Realtime connection is not established."));

            var op = new AsyncOperation<RealtimeResult>();
            var commandOp = _connection.SendCommand("deleteMessage", new
            {
                roomId = channelId,
                MessageId = messageId
            });

            commandOp.UseCompleted(_ =>
            {
                if (!commandOp.Result.IsSuccess)
                {
                    op.Complete(RealtimeResult.Fail(commandOp.Result.Code,
                        commandOp.Result.Message ?? "Delete failed"));
                    return;
                }

                op.Complete(RealtimeResult.Success());
            });

            return op;
        }

        public AsyncOperation<RealtimeResult> MarkAsReadAsync(string channelId, long messageNumber)
        {
            if (string.IsNullOrWhiteSpace(channelId))
                return AsyncOperation<RealtimeResult>.CreateCompleted(
                    RealtimeResult.Fail("validation", "channelId is required."));

            if (messageNumber <= 0)
                return AsyncOperation<RealtimeResult>.CreateCompleted(
                    RealtimeResult.Fail("validation", "messageNumber must be greater than 0."));

            if (!_connection.IsConnected)
                return AsyncOperation<RealtimeResult>.CreateCompleted(
                    RealtimeResult.Fail("not_connected", "Realtime connection is not established."));

            var op = new AsyncOperation<RealtimeResult>();
            var commandOp = _connection.SendCommand("markAsRead", new
            {
                roomId = channelId,
                MessageNumber = messageNumber
            });

            commandOp.UseCompleted(_ =>
            {
                if (!commandOp.Result.IsSuccess)
                {
                    op.Complete(RealtimeResult.Fail(commandOp.Result.Code,
                        commandOp.Result.Message ?? "Mark as read failed"));
                    return;
                }

                op.Complete(RealtimeResult.Success());
            });

            return op;
        }

        private void ConnectInternal(RealtimeSessionInfo sessionInfo, AsyncOperation<RealtimeResult> operation)
        {
            var wsUrl = BuildWsUrl(sessionInfo);
            if (string.IsNullOrWhiteSpace(wsUrl))
            {
                operation.Complete(RealtimeResult.Fail("validation", "Realtime wsUrl is empty."));
                return;
            }

            var connectOp = _connection.Connect(wsUrl);
            connectOp.UseCompleted(_ =>
            {
                if (connectOp.Result.IsSuccess)
                    operation.Complete(RealtimeResult.Success());
                else
                {
                    _logger.Error($"Chats realtime connect failed: {connectOp.Result.Message}");
                    _shouldBeConnected = false;
                    operation.Complete(RealtimeResult.Fail("connect_failed", "Realtime connect failed."));
                }
            });
        }

        private void HandleConnectionStateChanged(RealtimeConnectionState state)
        {
            OnConnectionStateChanged?.Invoke(state);

            if (state == RealtimeConnectionState.Connected)
            {
                StartHeartbeat();
                return;
            }

            if (state == RealtimeConnectionState.Disconnected && _shouldBeConnected)
            {
                StopHeartbeat();
                _ = StartReconnectLoopAsync();
            }
        }

        private async Task StartReconnectLoopAsync()
        {
            if (_reconnectInProgress)
                return;

            _reconnectInProgress = true;
            OnConnectionStateChanged?.Invoke(RealtimeConnectionState.Reconnecting);
            var reconnected = false;

            try
            {
                for (var attempt = 1; attempt <= _reconnectPolicy.MaxAttempts && _shouldBeConnected; attempt++)
                {
                    var sessionResult = await AwaitOperation(_realtimeAuthProvider.CreateSessionAsync());
                    if (!sessionResult.IsSuccess || sessionResult.Data == null)
                    {
                        await Task.Delay(_reconnectPolicy.GetDelayMs(attempt));
                        continue;
                    }

                    var wsUrl = BuildWsUrl(sessionResult.Data);
                    if (string.IsNullOrWhiteSpace(wsUrl))
                    {
                        await Task.Delay(_reconnectPolicy.GetDelayMs(attempt));
                        continue;
                    }

                    if (!_shouldBeConnected)
                        return;

                    var connectResult = await AwaitOperation(_connection.Connect(wsUrl));
                    if (!connectResult.IsSuccess)
                    {
                        await Task.Delay(_reconnectPolicy.GetDelayMs(attempt));
                        continue;
                    }

                    if (!_shouldBeConnected)
                        return;

                    await RestoreSubscriptionsAndHistoryAsync();
                    reconnected = true;
                    return;
                }
            }
            finally
            {
                _reconnectInProgress = false;

                if (!reconnected && _shouldBeConnected)
                {
                    OnError?.Invoke(new ChatErrorEvent
                    {
                        Code = "reconnect_failed",
                        Message = "Realtime reconnect failed."
                    });
                }
            }
        }

        private async Task RestoreSubscriptionsAndHistoryAsync()
        {
            var channels = _subscriptions.GetAll();
            foreach (var channelId in channels)
            {
                var subscribeResult = await AwaitOperation(
                    _connection.SendCommand("subscribe", new RealtimeAckChannelPayload
                    {
                        RoomId = channelId
                    }));

                if (!subscribeResult.IsSuccess)
                    continue;

                if (TryGetLastSeen(channelId, out var lastSeenNumber) && lastSeenNumber > 0)
                    await RecoverChannelHistoryAsync(channelId, lastSeenNumber);
            }
        }

        private async Task RecoverChannelHistoryAsync(string channelId, long startAfter)
        {
            var after = startAfter;
            for (var page = 0; page < MaxRecoveryPages; page++)
            {
                if (!_shouldBeConnected || !_connection.IsConnected)
                    return;

                var history =
                    await AwaitOperation(GetMessagesAsync(channelId, after: after, limit: MaxHistoryPageSize));
                if (!history.IsSuccess || history.Data == null || history.Data.Length == 0)
                    return;

                var maxSeen = after;
                foreach (var message in history.Data)
                {
                    TrackLastSeen(message);
                    if (message != null && message.Number > maxSeen)
                        maxSeen = message.Number;

                    OnMessageReceived?.Invoke(message);
                }

                if (maxSeen <= after || history.Data.Length < MaxHistoryPageSize)
                    return;

                after = maxSeen;
            }
        }

        private void HandleRealtimeEvent(RealtimeEvent realtimeEvent)
        {
            _eventDispatcher.Dispatch(realtimeEvent);
        }

        private void HandleRealtimeError(RealtimeError realtimeError)
        {
            OnError?.Invoke(new ChatErrorEvent
            {
                Code = realtimeError.Code,
                Message = realtimeError.Message
            });
        }

        private RealtimeEventDispatcher CreateEventDispatcher()
        {
            var dispatcher = new RealtimeEventDispatcher();

            dispatcher.Register("subscribed", new SubscribedEventHandler(_jsonService, _subscriptions, channelId =>
            {
                OnSubscribedChannel?.Invoke(channelId);
            }));

            dispatcher.Register("messageCreated", new MessageCreatedEventHandler(_jsonService, dto =>
            {
                TrackLastSeen(dto);
                OnMessageReceived?.Invoke(dto);
            }));

            dispatcher.Register("messageEdited", new MessageEditedEventHandler(_jsonService, dto =>
            {
                TrackLastSeen(dto);
                OnMessageEdited?.Invoke(dto);
            }));

            dispatcher.Register("messageDeleted", new MessageDeletedEventHandler(_jsonService, payload =>
            {
                OnMessageDeleted?.Invoke(payload);
            }));

            dispatcher.Register("memberAdded", new MemberEventHandler(_jsonService, member =>
            {
                OnMemberAdded?.Invoke(member);
            }));

            dispatcher.Register("memberRemoved", new MemberEventHandler(_jsonService, member =>
            {
                OnMemberRemoved?.Invoke(member);
            }));

            dispatcher.Register("memberBanned", new MemberBannedEventHandler(_jsonService, member =>
            {
                OnMemberBanned?.Invoke(member);
            }));

            dispatcher.Register("channelDeleted", new ChannelDeletedEventHandler(channelId =>
            {
                OnChannelDeleted?.Invoke(channelId);
            }));

            return dispatcher;
        }

        private void TrackLastSeen(ChatMessageDto message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.ChannelId))
                return;

            if (!_lastSeenMessageByChannel.TryGetValue(message.ChannelId, out var existing) ||
                message.Number > existing)
            {
                _lastSeenMessageByChannel[message.ChannelId] = message.Number;
            }
        }

        private bool TryGetLastSeen(string channelId, out long number)
        {
            return _lastSeenMessageByChannel.TryGetValue(channelId, out number);
        }

        private T Deserialize<T>(JsonValue value)
        {
            if (value == null) return default;
            var json = _jsonService.ToJson(value);
            return _jsonService.FromJson<T>(json);
        }

        private JsonValue ToJsonValue(object value)
        {
            if (value == null)
                return new JsonValue(JsonValueType.Null);

            var json = _jsonService.ToJson(value);
            return _jsonService.FromJson<JsonValue>(json);
        }

        private static Task<RealtimeCommandResult> AwaitOperation(
            AsyncOperation<RealtimeCommandResult> operation)
        {
            var tcs = new TaskCompletionSource<RealtimeCommandResult>();
            if (operation.IsDone)
            {
                tcs.TrySetResult(operation.Result);
                return tcs.Task;
            }

            operation.UseCompleted(_ => tcs.TrySetResult(operation.Result));
            return tcs.Task;
        }

        private static Task<RestApiResult<T>> AwaitOperation<T>(AsyncOperation<RestApiResult<T>> operation)
        {
            var tcs = new TaskCompletionSource<RestApiResult<T>>();
            if (operation.IsDone)
            {
                tcs.TrySetResult(operation.Result);
                return tcs.Task;
            }

            operation.UseCompleted(_ => tcs.TrySetResult(operation.Result));
            return tcs.Task;
        }

        private string BuildWsUrl(RealtimeSessionInfo sessionInfo)
        {
            if (sessionInfo == null)
                return string.Empty;

            if (string.IsNullOrWhiteSpace(sessionInfo.WsUrl))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(sessionInfo.RtToken))
                return sessionInfo.WsUrl;

            if (sessionInfo.WsUrl.IndexOf("token=", StringComparison.OrdinalIgnoreCase) >= 0)
                return sessionInfo.WsUrl;

            var separator = sessionInfo.WsUrl.Contains("?") ? "&" : "?";
            return $"{sessionInfo.WsUrl}{separator}token={UnityWebRequest.EscapeURL(sessionInfo.RtToken)}";
        }

        private void StartHeartbeat()
        {
            if (_heartbeatCts != null)
                return;

            _heartbeatCts = new CancellationTokenSource();
            _ = RunHeartbeatLoopAsync(_heartbeatCts.Token);
        }

        private void StopHeartbeat()
        {
            if (_heartbeatCts == null)
                return;

            var cts = _heartbeatCts;
            _heartbeatCts = null;

            try
            {
                cts.Cancel();
            }
            catch
            {
            }
            finally
            {
                cts.Dispose();
            }
        }

        private async Task RunHeartbeatLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _shouldBeConnected)
            {
                try
                {
                    await Task.Delay(HeartbeatIntervalMs, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!_connection.IsConnected)
                    return;

                try
                {
                    var result = await AwaitOperation(
                        _connection.SendCommand("ping", new JsonValue(JsonValueType.Object), 5000));
                    if (!result.IsSuccess &&
                        string.Equals(result.Code, "not_connected", StringComparison.Ordinal))
                    {
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(new ChatErrorEvent
                    {
                        Code = "heartbeat_failed",
                        Message = ex.Message
                    });
                }
            }
        }
    }
}
