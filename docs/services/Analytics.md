# Analytics

`AnalyticsService` — аналитика и метрики.

## Методы

### Кастомные события
- `SendEventAsync(metricId)` — отправить событие без параметров
- `SendEventAsync(metricId, parameters)` — с параметрами (`Dictionary<string, string>`)
- `EnqueueEvent(eventName, parameters, tags)` — добавить в очередь для batch-отправки

### Batch
- `SendBatchAsync(events)` — отправка пакета событий (`List<BatchEventItemDto>`)

### Системные метрики
- `SendSessionStartedAsync()` — начало сессии
- `SendPlaytimeAsync(playTimeInMinutes)` — время игры

## Code
- `Core/Services/Analytics/*`
