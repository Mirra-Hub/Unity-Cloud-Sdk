# DailyRewards

`DailyRewardsService` — ежедневные награды.

## Методы

- `GetCalendarsAsync()` → `DailyRewardCalendarDto[]` — список календарей
- `GetStatusAsync()` → `DailyRewardStatusDto[]` — статус по всем календарям
- `GetStatusAsync(calendarId)` → `DailyRewardStatusDto` — статус по конкретному календарю
- `ClaimAsync(calendarId, dayNumber?)` → `ClaimDailyRewardResponseDto` — получить награду (dayNumber для catch-up)

## Code
- `Core/Services/DailyRewards/*`
