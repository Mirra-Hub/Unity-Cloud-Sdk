# Chats

`ChatsService` — чаты с REST API и WebSocket realtime.

## REST API

- `CreateChannelAsync(name, templateKey, topic = null)` → `ChatChannelDto` — создать чат (room-канал) по ключу шаблона; вызывающий автоматически становится первым участником. `templateKey` обязателен и должен ссылаться на существующий шаблон чата.
- `GetMyChannelsAsync(page = 1, pageSize = 20)` → `PaginatedResult<ChatPlayerChannelDto>` — чаты игрока (выбранного профиля): комнаты и чаты групп, новые членства сверху, у каждого `UnreadCount`. Удалённые каналы не приходят, архивные приходят (`Channel.State`). `pageSize` — не больше 50. Свои сообщения считаются непрочитанными, пока не вызван `MarkAsReadAsync`.
- `LookupGroupChannelAsync(groupId)` → `ChatChannelDto` — канал группы
- `GetChannelAsync(channelId)` → `ChatChannelDto`
- `GetMembersAsync(channelId)` → `ChatMemberDto[]`
- `GetMessagesAsync(channelId, before, after, limit, targetMessageId)` → `ChatMessageDto[]`
- `JoinAsync(channelId)` — вступить в канал
- `LeaveAsync(channelId)` — покинуть канал

## Realtime (WebSocket)

### Подключение
- `ConnectAsync()` → `RealtimeResult` — подключение к WebSocket
- `DisconnectAsync()` → `RealtimeResult`
- `SubscribeAsync(channelId)` — подписка на события канала
- `UnsubscribeAsync(channelId)`

### Сообщения
- `SendMessageAsync(channelId, body, metadata, taggedProfileIds, targetMessageId)` → `ChatMessageDto`
- `EditMessageAsync(channelId, messageId, body, metadata, taggedProfileIds)` → `ChatMessageDto`
- `DeleteMessageAsync(channelId, messageId)`
- `MarkAsReadAsync(channelId, messageNumber)`

## События

- `OnConnectionStateChanged` — смена состояния подключения
- `OnSubscribedChannel` — подписка на канал
- `OnMessageReceived` — новое сообщение
- `OnMessageEdited` — сообщение отредактировано
- `OnMessageDeleted` — сообщение удалено
- `OnMemberAdded` / `OnMemberRemoved` / `OnMemberBanned` — события участников
- `OnChannelDeleted` — канал удалён
- `OnError` — ошибка

## Code
- `Core/Services/Chats/*`
