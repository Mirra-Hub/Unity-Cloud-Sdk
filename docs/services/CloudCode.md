# CloudCode

`CloudCodeService` — выполнение серверных скриптов.

## Методы

- `ExecuteAsync(scriptId, input)` → `ExecuteCloudCodeResponseDto` — выполнить скрипт (сырой результат)
- `ExecuteAsync<T>(scriptId, input)` → `T` — выполнить скрипт с типизированным ответом

Параметр `input` — `Dictionary<string, object>` с входными данными для скрипта.

## Code
- `Core/Services/Cloud Code/*`
