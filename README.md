# Test Task: HTML Parser & Decryptor API

REST API сервис на **.NET 10** для парсинга HTML-страниц, поиска элементов по CSS-селекторам, извлечения email-адресов и расшифровки AES-256 текста. Всё приложение упаковано в Docker Compose вместе с PostgreSQL 18 и pgAdmin.

## 🚀 Быстрый старт

Для запуска требуется только установленный **Docker Desktop**.

    docker compose up --build

После запуска сервисы будут доступны по адресам:

| Сервис | URL | Доступ |
|--------|-----|--------|
| **API & Swagger UI** | http://localhost:8090/api/swagger | Без аутентификации |
| **pgAdmin** | http://localhost:8080 | Desktop Mode (без мастер-пароля) |
| **PostgreSQL** | localhost:5432 | user: `postgres`, pass: `postgres`, db: `testdb` |

> ⚠️ **Важно:** При первом подключении к серверу `TestDB` в pgAdmin будет запрошен пароль от БД. Введите `postgres` и **обязательно поставьте галочку "Save password"**. Пароль сохранится в Docker-volume `pgadmin_data` и больше не будет запрашиваться при последующих запусках.

### Остановка и полный сброс

    docker compose down -v

Флаг `-v` удалит все volume'ы (БД и pgAdmin), что позволит запустить проект с абсолютно чистого листа.

## 🏗 Архитектура проекта

    HTTP-запрос
        ↓
    [ProcessingController]   ← принимает POST /api/processing
        ↓
    [FluentValidation]       ← проверка структуры (пустые поля, обязательные параметры)
        ↓
    [ProcessingService]      ← вся бизнес-логика
        ↓
        ├── Decode Base64 (URL, Page)
        ├── AngleSharp (парсинг HTML → DOM)
        ├── CSS-селектор (выбор элементов)
        ├── Regex (поиск email-адресов)
        ├── AES-256-ECB (расшифровка)
        └── Dapper → PostgreSQL (запись в elements)
        ↓
    [JSON-ответ с отступами]

## 🧠 Ключевое архитектурное решение: Async vs Sync

В ТЗ была подсказка: *"Использовать доступные асинхронные функции внутри (парсинг HTML, применение регулярных выражений, сериализация...)"*.

Я **намеренно отступил** от этой подсказки в части CPU-Bound операций, следуя лучшим практикам ASP.NET Core. Это осознанное решение, основанное на понимании разницы между I/O-Bound и CPU-Bound задачами.

### ✅ Где используется `await` (I/O-Bound операции)

| Операция | Код |
|----------|-----|
| Подключение к PostgreSQL | `await connection.OpenAsync()` |
| Запись данных через Dapper | `await connection.ExecuteAsync(...)` |

**Обоснование:** При I/O-Bound операциях поток ASP.NET Core **блокируется** в ожидании ответа от сети/БД. Использование `await` освобождает поток и возвращает его в `ThreadPool`, позволяя серверу обслуживать другие HTTP-запросы. Это критически важно для масштабируемости (RPS) и предотвращает Thread Starvation при высокой нагрузке.

### ❌ Где НЕ используется `async` (CPU-Bound операции)

| Операция | Почему синхронно |
|----------|------------------|
| `Convert.FromBase64String()` | Декодирование в памяти |
| `HtmlParser.ParseDocument()` | Парсинг HTML-строки в DOM |
| `document.QuerySelectorAll()` | Обход уже построенного DOM |
| `Regex.Matches()` | Скомпилированный Regex работает в CPU |
| `aes.CreateDecryptor().TransformFinalBlock()` | Криптография = чистый CPU |

**Обоснование:** Эти операции **не ждут сеть**, а нагружают процессор. Оборачивание их в `await Task.Run(...)` **не освобождает** поток — оно лишь отдаёт вычисления другому потоку из того же ThreadPool. При этом:

- Создаётся лишняя State Machine для `async/await`
- Выделяется память под объект `Task`
- Происходит переключение контекста потока
- Потребление CPU не уменьшается

**Итог:** Обёртывание CPU-Bound операций в `async` **снижает** пропускную способность API и **увеличивает** потребление памяти без каких-либо преимуществ.

### 📚 Ссылки на официальную документацию

- [Async/Await Best Practices in ASP.NET Core](https://learn.microsoft.com/ru-ru/dotnet/csharp/programming-guide/concepts/async/)
- [Task.Run vs Task.FromResult](https://learn.microsoft.com/ru-ru/dotnet/csharp/async)

## 🛡 Обработка ошибок

API возвращает унифицированный JSON-объект. При ошибке флаг `is_error` становится равным `1`.

| Код ошибки | Описание |
|-----------|----------|
| `EMPTY_SELECTOR` | Пустой CSS-селектор |
| `EMPTY_ATTRIBUTE` | Пустое имя атрибута |
| `MISSING_PARAMETER` | Отсутствует обязательный параметр |
| `INVALID_URL_BASE64` | Ошибка декодирования URL из Base64 |
| `INVALID_PAGE_BASE64` | Ошибка декодирования HTML-страницы из Base64 |
| `AES_DECRYPT_FAILED` | Ошибка расшифровки AES (неверный ключ, некорректный размер блока) |
| `DB_ERROR` | Ошибка подключения или записи в PostgreSQL |
| `INTERNAL_ERROR` | Необработанное исключение |

## 🔧 Стек технологий

- **.NET 10** — ASP.NET Core Web API
- **PostgreSQL 18** — хранилище данных
- **Docker Compose** — контейнеризация (API + PostgreSQL + pgAdmin)
- **AngleSharp** — парсинг HTML и работа с DOM
- **FluentValidation** — декларативная валидация входных данных
- **Dapper** — micro-ORM для работы с PostgreSQL
- **Npgsql** — ADO.NET-провайдер для PostgreSQL
- **System.Text.Json** — JSON-сериализация с отступами (`WriteIndented = true`)
- **System.Security.Cryptography** — AES-256-ECB (стандартные библиотеки .NET)

## 📦 Структура проекта

    .
    ├── TestTask/                        # Исходный код ASP.NET Core
    │   ├── Controllers/
    │   │   └── ProcessingController.cs  # API-контроллер (POST /api/processing)
    │   ├── Services/
    │   │   ├── IProcessingService.cs
    │   │   └── ProcessingService.cs     # Вся бизнес-логика
    │   ├── Models/
    │   │   ├── ProcessRequestDto.cs     # Входящее DTO + [JsonPropertyName]
    │   │   ├── ProcessResponseDto.cs    # Исходящее DTO
    │   │   └── ElementEntity.cs         # Сущность для БД
    │   ├── Validators/
    │   │   └── ProcessRequestValidator.cs
    │   └── Program.cs                   # DI, Swagger, JSON-конфигурация
    │
    ├── compose.yml                      # Docker Compose (3 контейнера)
    ├── init.sql                         # SQL-скрипт создания таблицы elements
    ├── servers.json                     # Автоподключение БД в pgAdmin
    │
    ├── json_payload_1.txt               # Тестовый запрос №1 (входные данные)
    ├── json_payload_2.txt               # Тестовый запрос №2 (входные данные)
    ├── json_result_1.txt                # Результат обработки payload_1
    ├── json_result_2.txt                # Результат обработки payload_2
    │
    ├── README.md                        # Этот файл
    └── .gitignore

## 🗄 База данных

### Таблица `elements`

Создаётся автоматически при первом запуске PostgreSQL через скрипт `init.sql`.

| Колонка | Тип | Описание |
|---------|-----|----------|
| `id` | BIGSERIAL (PK) | Автоинкрементный идентификатор |
| `attribute_value` | TEXT | Значение HTML-атрибута найденного элемента |
| `html_code` | TEXT | Полный HTML-код элемента (OuterHtml) |

### Проверка данных через SQL

    SELECT * FROM elements ORDER BY id;
    SELECT COUNT(*) FROM elements;

## 🧪 Тестирование

### Сценарий проверки (как у ревьюеров)

1. Клонировать репозиторий
2. Выполнить `docker compose up --build`
3. Открыть Swagger: http://localhost:8090/api/swagger
4. Отправить POST-запрос `application/json` с содержимым `json_payload_1.txt`
5. Убедиться, что ответ соответствует `json_result_1.txt`
6. Открыть pgAdmin: http://localhost:8080
7. Подключиться к серверу `TestDB` (пароль `postgres`, галочка "Save password")
8. Проверить таблицу `elements` — там должны появиться записи

### Пример запроса (упрощённо)

    {
      "selector": "script[src]",
      "attribute": "src",
      "url_b64": "aHR0cHM6Ly90ZXN0LmNvbS9wYWdlMTIz",
      "encrypted_text_bytes_b64": "hXeVCcIEyC/5ovf4eyJCo...",
      "key_bytes_b64": "SGVsbG8gVGVzdEpvYiAyNTYgYml0IHNlY3JldCBrZXk=",
      "page_b64": "PCFET0NUWVBFIGh0bWw+..."
    }

### Пример ответа

    {
      "is_error": 0,
      "error_code": null,
      "error_message": null,
      "elements_count": 9,
      "emails_count": 5,
      "url": "https://test.com/page123",
      "decrypted_plain_text": "...",
      "elements_attr_list": ["...", "..."],
      "emails_list": ["webmaster@rbc.ru", "..."]
    }

## 🔐 Особенности реализации

### Контейнер API: билд на лету

Согласно требованию ТЗ, контейнер API использует официальный **SDK-образ** .NET 10 и монтирует исходный код через `volumes`. При каждом запуске `docker compose up` происходит:

1. `dotnet restore` — восстановление NuGet-пакетов
2. `dotnet run` — компиляция и запуск

Это позволяет менять код без пересборки Docker-образа.

### pgAdmin в Desktop Mode

Контейнер pgAdmin настроен на Desktop Mode через переменную `PGADMIN_CONFIG_SERVER_MODE: "False"`, что отключает необходимость ввода мастер-пароля веб-интерфейса. Конфигурация сервера PostgreSQL предзаполнена через `servers.json`.

### PostgreSQL 18

Для совместимости с новой структурой хранения данных в PostgreSQL 18 volume монтируется в `/var/lib/postgresql` (без `/data` в конце), что соответствует официальным рекомендациям [docker-library/postgres#1259](https://github.com/docker-library/postgres/pull/1259).

## 📝 Комментарии по коду

### FluentValidation

Все валидаторы регистрируются автоматически через `AddValidatorsFromAssemblyContaining<Program>()`. Это позволяет добавлять новые валидаторы без правки DI-регистраций.

### Скомпилированный Regex

Regex для поиска email-адресов создан как `static readonly` с флагом `RegexOptions.Compiled`. Это требование ТЗ, и оно даёт прирост производительности при многократном вызове (компиляция происходит один раз).

### JSON-именование

Использованы атрибуты `[JsonPropertyName]` для явного маппинга полей DTO к snake_case в JSON (как в ТЗ: `url_b64`, `elements_count` и т.д.). Это надёжнее глобальных политик именования.

---

**Автор:** Vladimir Ganaga
**Дата:** Сентябрь 2026
**Стек:** .NET 10 · ASP.NET Core · PostgreSQL 18 · Docker Compose