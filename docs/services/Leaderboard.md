# Leaderboard

`LeaderboardService` — таблицы лидеров. Все методы принимают **ключ** борда (`LeaderboardConfig.Key`), а не id
конфига в консоли.

## Как играть на борде

1. `JoinAsync(leaderboardKey)` — один раз: очки принимаются только от участников, иначе сервер отвечает
   `403 leaderboards.participation_required`. Повторный join безвреден.
2. `SubmitScoreAsync(score, leaderboardKey)` — очки текущего игрока; как они сочетаются с сохранёнными, решает
   `UpdateStrategy` борда (`Best` / `Latest` / `Total`). Возвращает запись игрока с новым местом.
3. `LeaveAsync(leaderboardKey)` — удаляет игрока и его очки текущей сессии; награды уже закончившейся сессии не
   трогает.

Имя, иконка и страна в записи — из профиля игрока на сервере: клиент их не передаёт.

## Методы

- `InitializeAsync()` → `LeaderboardConfigDto[]` — конфиги борда (кеш — `LeaderboardConfigs`)
- `GetConfigAsync(leaderboardKey)` → `LeaderboardConfigDto`

### Участие и очки
- `JoinAsync(leaderboardKey)` → `LeaderboardEntryDto` — запись игрока, `null` до первых очков
- `SubmitScoreAsync(double score, leaderboardKey)` → `LeaderboardEntryDto`. `NaN` и `±Infinity` отклоняются без
  запроса (`ValidationFail`)
- `SubmitScoreAsync(TimeSpan time, leaderboardKey)` — для `LeaderboardType.Time`: отправляет секунды с дробью
- `SubmitScoreAsync(DateTime score, leaderboardKey)` — `[Obsolete]`: читает `DateTime` как время от
  `DateTime.MinValue` (раньше отправлялась доля суток, а от суток и больше — исключение)
- `LeaveAsync(leaderboardKey)`

### Топ
- `GetLeaderboardTopEntries(leaderboardKey, top)` → `LeaderboardEntriesDto` — таблица игрока: его когорта на
  борде с когортами, весь борд иначе. На борде с когортами до первых очков — пустой список
- `GetLeaderboardGlobalTopEntries(leaderboardKey, top)` — весь борд, все когорты вместе
- `GetLeaderboardTopEntriesByCountry(leaderboardKey, entriesCount)` — среди игроков страны профиля
- `GetLeaderboardTopEntriesByFriends(leaderboardKey, friendIds)` — среди переданных id профилей

### Позиция игрока
- `GetLeaderboardPlayer(leaderboardKey)` → `LeaderboardEntryDto` — без очков в этой сессии
  `404 leaderboards.entry_not_found`
- `GetLeaderboardPlayerAroundEntries(leaderboardKey, around)` → `LeaderboardAroundEntriesDto` — соседи в таблице
  игрока
- `GetLeaderboardEntries(leaderboardKey, top, around)` → `LeaderboardTopAndPlayersAroundDto` — топ всего борда и
  соседи игрока на нём

Порядок мест полный: по очкам, при равенстве — по id игрока, так что равные по очкам получают соседние места.

## Конфиг

`LeaderboardConfig` / `LeaderboardConfigDto`:

- `ResetTimeHour` / `ResetTimeMinute` — время сброса, UTC
- `NextResetDate` — следующий сброс, UTC, со временем суток — для обратного отсчёта; `null`, если авто-сброс
  выключен или на борде ещё не было очков
- `LastResetDate` — день последнего сброса
- `CohortsEnabled` / `CohortSize`
- `RewardsForPlaces[].rewards[]` — `rewardKey` (ключ ресурса экономики; `null`, если ресурса в ветке больше
  нет), `economyResourceKind`, `count`; диапазон `valueMin..valueMax` — места от 1 (`ByPlace`) или очки
  (`ByScore`), концы включены

## Награды

Награды выплачиваются в Economy, когда сессия заканчивается (плановый сброс или ручной из консоли):
`EconomyService.GetPendingRewardsAsync()` показывает их (контейнеры с `SourceType == Leaderboard`; `SourceId` —
id конфига борда на момент выплаты, после правки конфига он может не совпасть с `LeaderboardConfig.Id`),
`EconomyService.ClaimRewardsAsync()` забирает. Методы
`GetRewardsAsync` / `SubmitRewardsAsync` этого сервиса — `[Obsolete]`: такого маршрута на сервере нет.

## Ошибки

`leaderboards.not_found` (404, нет борда с ключом), `leaderboards.entry_not_found` (404),
`leaderboards.participation_required` (403), `leaderboards.invalid_score` (422),
`leaderboards.persistence_failed` (500) — константы в `CloudErrorCodes`.

## Code
- `Core/Services/Leaderboard/*`
