# Friends

`FriendsService` — друзья, заявки, баны.

## Друзья

- `GetFriendsAsync(getProfilesInfo)` → `GetPlayerDto[]` — список друзей
- `RemoveFriendAsync(targetPlayerId)` — удалить друга
- `BanAsync(targetPlayerId)` / `BanManyAsync(targetPlayerIds)` — забанить

## Заявки в друзья

- `SendAsync(targetPlayerId)` / `SendManyAsync(targetPlayerIds)` — отправить заявку
- `RevokeAsync(targetPlayerId)` / `RevokeManyAsync(targetPlayerIds)` — отозвать
- `AcceptAsync(sourcePlayerId)` / `AcceptManyAsync(sourcePlayerIds)` — принять
- `RejectAsync(sourcePlayerId)` / `RejectManyAsync(sourcePlayerIds)` — отклонить
- `DeleteAsync(targetPlayerId)` / `DeleteManyAsync(targetPlayerIds)` — удалить
- `GetRequestsAsync()` — все заявки
- `GetOutgoingAsync()` — исходящие
- `GetIncomingAsync()` — входящие

## Code
- `Core/Services/Friends/*`
