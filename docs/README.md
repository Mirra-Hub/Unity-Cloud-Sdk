# Cloud Unity SDK — Документация

Unity SDK для интеграции с MirraCloud.

## Архитектура

```
Packages/com.mirrahub.cloud-sdk/
├── Runtime/              — сборка MirraCloudSDK
│   ├── General/          — инициализация, async operations
│   ├── RestApiClient/    — HTTP клиент, ISessionRefresher
│   ├── Realtime/         — WebSocket (чаты)
│   ├── Storage/          — локальное хранилище: IStorage (PlayerPrefs) + Blob (IBlobStorage)
│   ├── Logger/           — логирование
│   ├── Json/             — сериализация
│   ├── Services/         — сервисы (см. ниже)
│   └── External/         — вендоренный SimpleWebTransport
├── Editor/               — SA авторизация, управление проектом
├── Tests/                — edit-mode тесты
└── Samples~/Showcase/    — пример на все сервисы
```

Все сервисы используют паттерн `AsyncOperation<RestApiResult<T>>` для асинхронных вызовов.

## Сервисы

### Authentication & Accounts
- [Auth](services/Auth.md) — аутентификация (guest, email, device, OAuth, OpenID)
- [PlayerAccount](services/PlayerAccount.md) — управление аккаунтом и профилями

### Game Config
- [Economy](services/Economy.md) — валюты, предметы, энергии, инвентарь
- [Entities](services/Entities.md) — конфигурации сущностей (templates, components)
- [RemoteConfig](services/RemoteConfig.md) — удалённая конфигурация
- [Segments](services/Segments.md) — сегменты игроков
- [Deployment](services/Deployment.md) — резолв веток для окружения

### Player Data
- [CloudSave](services/CloudSave.md) — облачные сохранения (player, global, custom)
- [DailyRewards](services/DailyRewards.md) — ежедневные награды

### Competitive
- [Leaderboard](services/Leaderboard.md) — таблицы лидеров
- [Tournaments](services/Tournaments.md) — турниры с лигами
- [Challenges](services/Challenges.md) — челленджи (гонка к цели)

### Social
- [Friends](services/Friends.md) — друзья, заявки, баны
- [Groups](services/Groups.md) — группы/кланы с ролями и правами
- [Chats](services/Chats.md) — чаты (REST + WebSocket realtime)

### Content & Code
- [AssetsStorage](services/AssetsStorage.md) — загрузка ассетов (текстуры, аудио, бандлы) с локальным кешем
- [CloudCode](services/CloudCode.md) — выполнение серверных скриптов
- [Analytics](services/Analytics.md) — аналитика и метрики

## Локальное хранилище

- [Storage](Storage.md) — `IBlobStorage` (SQLite / IndexedDB / File) для durable локальных данных; используется кешем ассетов

## Editor Tools

Окно `Tools > Mirra Cloud > Manager` для настройки SDK в Unity Editor.

### Архитектура

```
MirraCloudEditorWindow          — оркестратор (OnGUI, переключение views)
├── LoginView                   — авторизация по SA ключу
├── ProjectSettingsView         — проект, ветка, токен, создание токенов
└── EditorApiService            — API клиент + ISessionRefresher
```

### Авторизация (Service Account)

Авторизация через обмен SA ключа на JWT:

```
SA Key → POST /api/cloud/public/auth/service-account/token → JWT + OrgId
```

- SA ключ сохраняется в `EditorPrefs` (`MirraCloud_SA_Key`)
- JWT + expiry кешируются в `EditorPrefs`
- **Авто-коннект**: при открытии окна, если SA ключ сохранён, автоматически обменивается на JWT
- **Авто-рефреш**: `EditorApiService` реализует `ISessionRefresher` — при 401 автоматически переобменивает SA ключ на новый JWT и повторяет запрос

### Управление проектом

- **Project** — выбор проекта из списка организации
- **Branch** — выбор ветки (авто-выбор первой доступной при смене проекта)
- **API Token** — выбор токена для SDK (авто-выбор первого доступного)
- **Create Token** — создание Game-токена прямо из редактора (`POST /organizations/{orgId}/projects/{projectId}/tokens`)

Выбранные значения сохраняются в `Configuration` ScriptableObject (`Resources/Configuration.asset`).

### EditorApiService API

| Метод | Описание |
|-------|----------|
| `ExchangeKeyAsync(saKey)` | Обмен SA ключа на JWT |
| `GetProjectsAsync(orgId)` | Список проектов организации |
| `GetBranchesAsync(projectId)` | Список веток проекта |
| `GetTokensAsync(orgId, projectId)` | Список API токенов |
| `CreateTokenAsync(orgId, projectId, name)` | Создание Game-токена |
| `RefreshSessionAsync()` | ISessionRefresher — переобмен SA ключа |

### Дополнительные инструменты

- **RestApiInspectorWindow** (`Tools > Mirra Cloud > REST Inspector`) — трассировка HTTP запросов SDK для отладки
- **DeveloperSettings** — переопределение Editor API URL для локальной разработки
