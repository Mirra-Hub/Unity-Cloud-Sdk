# Leaderboard

`LeaderboardService` — таблицы лидеров.

## Методы

- `InitializeAsync()` → `LeaderboardConfigDto[]` — загрузка конфигов
- `GetConfigAsync(leaderboardId)` → `LeaderboardConfigDto`

### Submit
- `SubmitScoreAsync(double score, leaderboardId)`
- `SubmitScoreAsync(DateTime score, leaderboardId)` — для time-based лидербордов

### Топ
- `GetLeaderboardTopEntries(leaderboardId, top)` → `LeaderboardEntriesDto` — глобальный топ
- `GetLeaderboardTopEntriesByCountry(leaderboardId, entriesCount)` — топ по стране
- `GetLeaderboardTopEntriesByFriends(leaderboardId, friendIds)` — топ среди друзей

### Позиция игрока
- `GetLeaderboardPlayer(leaderboardId)` → `LeaderboardEntryDto`
- `GetLeaderboardPlayerAroundEntries(leaderboardId, around)` → `LeaderboardAroundEntriesDto`
- `GetLeaderboardEntries(leaderboardId, top, around)` → `LeaderboardTopAndPlayersAroundDto` — топ + вокруг игрока

### Награды
- `GetRewardsAsync(reset)` → `PlayerRewardsDto`
- `SubmitRewardsAsync()` — забрать награды

## Свойства

- `LeaderboardConfigs` — кешированные конфиги

## Code
- `Core/Services/Leaderboard/*`
