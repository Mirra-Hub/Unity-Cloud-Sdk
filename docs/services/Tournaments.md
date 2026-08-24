# Tournaments

`TournamentsService` — турниры с лигами.

## Методы

- `InitializeAsync()` → `TournamentConfigDto[]` — загрузка конфигов
- `GetConfigAsync(tournamentId)` → `TournamentConfigDto`
- `SubmitScoreAsync(tournamentId, score, playerName)` — submit очков

### Топ
- `GetTopAsync(tournamentId, tableId, entriesCount)` → `TournamentEntriesDto` — глобальный топ
- `GetTopByCountryAsync(tournamentId, tableId, entriesCount)` — топ по стране
- `GetTopByFriendsAsync(tournamentId, tableId, friendIds)` — топ среди друзей

### Позиция игрока
- `GetPlayerAsync(tournamentId, tableId)` → `TournamentEntryDto`
- `GetAroundAsync(tournamentId, tableId, entriesRange)` → `TournamentPlayersAroundDto`
- `GetTopAndAroundAsync(tournamentId, tableId, topCount, aroundRange)` → `TournamentTopAndPlayersAroundDto`

### Лиги
- `GetPlayerLeagueMetaAsync(tournamentId)` → `PlayerLeagueMetaDto`

### Награды
- `GetRewardsAsync(reset)` → `PlayerRewardsDto`
- `SubmitRewardsAsync()` — забрать награды

## Свойства

- `TournamentConfigs` — кешированные конфиги
- `IsInitialized` — инициализирован ли сервис

## Code
- `Core/Services/Tournaments/*`
