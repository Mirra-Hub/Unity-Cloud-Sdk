# Challenges

`ChallengesService` — челленджи (гонка к цели).

## Методы

- `InitializeAsync()` → `ChallengeConfigDto[]` — загрузка конфигов
- `GetConfigAsync(challengeId)` → `ChallengeConfigDto`

### Участие
- `JoinAsync(challengeId)` — присоединиться
- `LeaveAsync(challengeId)` — покинуть

### Очки
- `SubmitScoreAsync(challengeId, score, playerName)` → `SubmitScoreResponseDto` — submit значения

### Топ
- `GetTopAsync(challengeId, entriesCount)` → `ChallengeEntriesDto` — глобальный топ
- `GetMyTopAsync(challengeId, entriesCount)` → `ChallengeEntriesDto` — топ когорты
- `GetAroundPlayerAsync(challengeId, entriesRange)` → `ChallengeEntriesDto` — вокруг игрока
- `GetPlayerAsync(challengeId)` → `ChallengeEntryDto` — запись игрока

### Награды
- `ClaimRewardAsync(challengeId)` → `SubmitScoreResponseDto` — забрать награду (только для `AllFinishers`)

## Свойства

- `ChallengeConfigs` — кешированные конфиги
- `IsInitialized` — инициализирован ли сервис

## Code
- `Core/Services/Challenges/*`
