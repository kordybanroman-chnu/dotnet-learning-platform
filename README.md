# Learning Platform - мікросервісна платформа онлайн-курсів

Навчальна платформа: студенти переглядають каталог курсів, записуються на курси, оплачують навчання та проходять програму; викладачі ведуть курси; студенти залишають відгуки й обговорюють матеріали.
Домен обрано як розширюваний: нові можливості (розклад, платежі, сертифікати, сповіщення, аналітика) додаються новими контекстами без зміни існуючих.

## Bounded Contexts

| Контекст / БД | Сервіс                          | Технологія                        | Сутності                                                                                                                          |
| ------------- | ------------------------------- | --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| Enrollments   | Записи на курси (транзакційний) | SQL Server + ADO.NET / Dapper     | Student, Enrollment, EnrollmentDetail (1:1), Course (локальна копія), EnrollmentItem (M:N)                                        |
| Catalog       | Каталог курсів                  | SQL Server + EF Core (Code First) | Course, Category, CourseCategory (M:N з полем AddedAt), CourseDetail (1:1), Instructor (1:N → Course), CourseImage (N:1 → Course) |
| Feedback      | Відгуки та обговорення          | MongoDB + Clean Architecture      | Review (author VO, comments[], photos[]), Discussion, RatingSummary                                                               |

Правила меж (діють для всіх майбутніх контекстів):

- Кожен контекст - окрема БД, спільних таблиць немає.
- Жодних FOREIGN KEY між базами. Посилання на чужу сутність - звичайний стовпець/поле з її Id.
- Кожна сутність має рівно одного власника. Інші контексти зберігають лише Id + за потреби знімок полів.
- Агрегат `Enrollment` = Enrollment + EnrollmentItem (позиції не існують без запису, змінюються лише через нього).

Статуси Enrollment: `Submitted → AwaitingValidation → SeatsConfirmed → Paid → Completed`; `Cancelled` - лише до `Paid`.
Перехід перевіряє збережувана процедура в явній транзакції, недопустимий перехід - керована помилка.

## Структура репозиторію

```text
/
├── README.md
├── docs/
│   ├── erd-p1.dbml
│   ├── erd-p2.dbml
│   └── feedback-examples.md
├── db/
│   ├── p1/
│   ├── p2/
│   └── p3/
├── Platform.slnx
├── Platform.AppHost/                 оркестрація Aspire: SQL Server, MongoDB, Redis, RabbitMQ, сервіси
├── Platform.ServiceDefaults/         Serilog, OpenTelemetry, CorrelationId, gRPC-інтерсептор, health checks
├── Platform.EventBus/                RabbitMQ: топологія, integration events, publisher, consumer
├── Platform.ApiGateway/              YARP-шлюз на порту 5000
├── Platform.Aggregator/              gRPC-клієнти, агрегація картки запису, кеш відповідей
├── Services/
│   ├── Enrollments/
│   │   ├── Enrollments.Api/          контролери, Protos/, Grpc/, Metrics/, HealthChecks/, IntegrationEvents/, Program.cs
│   │   ├── Enrollments.Bll/
│   │   ├── Enrollments.Dal/
│   │   └── Enrollments.Domain/
│   ├── Catalog/
│   │   ├── Catalog.Api/          контролери, Protos/, Grpc/, Metrics/, HealthChecks/, Program.cs
│   │   ├── Catalog.Bll/          сервіси, DTO, Caching/ (L1 + L2), публікація подій
│   │   ├── Catalog.Dal/
│   │   │   ├── Configurations/
│   │   │   └── Migrations/
│   │   └── Catalog.Domain/
│   └── Reviews/                Feedback: MongoDB + Clean Architecture
│       ├── Reviews.Domain/     сутності, Value Objects, інтерфейси
│       ├── Reviews.Application/ команди, запити, behaviors, DTO
│       ├── Reviews.Infrastructure/ Mongo-контекст, BSON-мапінг, репозиторії, сідинг
│       └── Reviews.WebApi/     контролери, Protos/, Grpc/, Metrics/, HealthChecks/, IntegrationEvents/, Program.cs, ProblemDetails
└── deploy/k8s/
```

Суфікси проєктів (`.Api`, `.Bll`, `.Dal`, `.Domain`, `.Application`, `.Infrastructure`) і папки (`Services/`, `db/`, `deploy/k8s/`, `Platform.*`) - стабільні, не перейменовуються при додаванні сервісів.

## Політика дублювання даних

| Поле                                      | Де зберігається                | Власник                  | Політика                                                  |
| ----------------------------------------- | ------------------------------ | ------------------------ | --------------------------------------------------------- |
| EnrollmentItems.CourseTitle, UnitPrice    | БД Enrollments                 | Course (БД Catalog)      | знімок на момент запису - не оновлюється                  |
| Courses.Id, Title, Price (локальна копія) | БД Enrollments (`dbo.Courses`) | Course (БД Catalog)      | копія для читання, оновлюється подіями з Catalog          |
| reviews.courseTitle                       | БД Feedback (MongoDB)          | Course (БД Catalog)      | денормалізація для читання, оновлюється подіями з Catalog |
| enrollments.studentDisplayName            | БД Feedback                    | Student (БД Enrollments) | копія для читання, оновлюється подіями                    |

Дві копії ніколи не редагуються незалежно з двох боків без визначеного власника (dual write заборонено).

## Бази даних

### P1 - Enrollments (SQL Server)

- `Students (Id, Email UNIQUE, FullName)` 1:N → `Enrollments (Id, StudentId FK, Status CHECK+DEFAULT, EnrolledAt DEFAULT)`.
- `EnrollmentDetails (EnrollmentId PK+FK → Enrollments, Note, PreferredSchedule)` - 1:1.
- `Courses (Id, Title, Price CHECK > 0, SeatsAvailable CHECK >= 0)` - локальна копія довідника, той самий Id, що в Catalog, без FK назовні.
- `EnrollmentItems (EnrollmentId FK, CourseId FK → локальна копія, CourseTitle + UnitPrice знімки, Units)` - M:N.
- Аудит: `CreatedAt/CreatedBy/UpdatedAt/UpdatedBy/IsDeleted` + `rowversion`; фільтрований індекс `WHERE IsDeleted = 0`; індекси під запити (напр. `IX_Enrollments_StudentId_Status`).
- Процедури (4–6): CRUD + `usp_ConfirmEnrollment` (транзакція, `UPDLOCK`, `THROW 50001` not found / `50002` недопустимий перехід).

### P2 - Catalog (SQL Server, Code First)

- `Instructor 1:N Course N:1 Category` через `CourseCategory (CourseId, CategoryId, AddedAt)` - M:N з власним полем.
- `Course 1:1 CourseDetail`; `Course 1:N CourseImage`.
- UNIQUE на `Category.Slug`, `Course.Slug`; CHECK `Price > 0`; індекси під пошук (`Title`, `CategoryId`); seed довідників (категорії, інструктори, курси).

### P3 - Feedback (MongoDB)

- `reviews`: embed `author {userId, displayName}`, масив `comments[]`, масив `photos` (є лише в частини документів), `courseId` (reference без FK) + `courseTitle` (денормалізація), `discussionId` (reference).
- `discussions`: довгі гілки винесено посиланням (необмежено великі підколекції не вбудовуються).
- `ratings`: агрегати для швидкого читання середнього бала.
- JSON Schema Validation (`rating 1..5`, обов'язкові `courseId, rating, author`); індекси (`courseId`, `rating`); приклади aggregation pipeline в `docs/`.
- Сервіс `Reviews.WebApi` читає колекцію `reviews` і сам створює індекси `{courseId, createdAt}`, text-індекс на `text` та unique `{courseId, author.userId}`; сідинг ідемпотентний (`IDataSeeder` + `InsertManyAsync`).

## Розгортання БД з нуля

```bash
sqlcmd -Q "CREATE DATABASE EnrollmentsDb;"
sqlcmd -d EnrollmentsDb -i db/p1/schema.sql
sqlcmd -d EnrollmentsDb -i db/p1/procedures.sql
sqlcmd -d EnrollmentsDb -i db/p1/seed.sql

dotnet tool restore
dotnet ef database update \
    --project Services/Catalog/Catalog.Dal \
    --startup-project Services/Catalog/Catalog.Api

mongosh FeedbackDb db/p3/collections.js
mongosh FeedbackDb db/p3/seed.js

docker run -d --name feedback-mongo -p 27017:27017 mongo:7
dotnet run --project Services/Reviews/Reviews.WebApi
```

`CatalogDb` створюється міграцією `InitialCreate` з моделей EF Core. `db/p2/schema.sql` і `db/p2/seed.sql` - SQL-версія Лаб. 01, `db/p2/ef-initial.sql` - ідемпотентний скрипт, згенерований з міграції (`dotnet ef migrations script --idempotent`). Курси наповнюються `DbInitializer` під час старту API, довідники - через `HasData`.

Повторний запуск `seed` не створює дублікатів (`IF NOT EXISTS` / `find-or-create` / перевірка наявності курсів). ERD: `docs/erd-p1.svg`, `docs/erd-p2.svg` (джерела для dbdiagram.io: `docs/erd-p1.dbml`, `docs/erd-p2.dbml`).

## Запуск платформи (Aspire)

Потрібні Docker і .NET 9 SDK. Aspire AppHost підключається як NuGet-пакет, окремий workload не потрібен.

```bash
dotnet user-secrets set "Parameters:sql-password" "Pass@word1" --project Platform.AppHost
dotnet run --project Platform.AppHost
```

`AppHost` створює контейнери SQL Server і MongoDB з іменованими volume (`WithDataVolume`), тому дані переживають перезапуск. Порядок запуску заданий `WaitFor`: БД → `enrollments-api`, `catalog-api`, `reviews-api` → `aggregator` → `gateway`. Gateway слухає `http://localhost:5000`; посилання на Dashboard з токеном друкується в консолі.

`CatalogDb` створюється міграцією `InitialCreate`, Catalog і Reviews наповнюють свої колекції під час старту. `EnrollmentsDb` створює Aspire, а таблиці треба застосувати один раз:

```bash
docker ps --filter name=sql- --format '{{.Names}}'
docker exec -i <sql-container> /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Pass@word1' -C -d EnrollmentsDb < db/p1/schema.sql
docker exec -i <sql-container> /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Pass@word1' -C -d EnrollmentsDb < db/p1/procedures.sql
docker exec -i <sql-container> /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Pass@word1' -C -d EnrollmentsDb < db/p1/seed.sql
```

Після старту перевірте Gateway на порту 5000:

```bash
curl -i 'http://localhost:5000/api/courses?page=1'
curl -i 'http://localhost:5000/api/enrollments/1'
curl -i 'http://localhost:5000/api/reviews?courseId=101'
curl -i 'http://localhost:5000/api/aggregator/enrollments/1/full'
```

Залежності: `Api → Bll → Dal`, `Domain` - спільний; у Reviews напрямок `WebApi → Application → Domain`, `Infrastructure → Application/Domain`. Усі сервіси, Gateway і Aggregator посилаються лише на `Platform.ServiceDefaults`; `AppHost` посилається на них. Рядки підключення передає Aspire через `ConnectionStrings__EnrollmentsDb`, `ConnectionStrings__CatalogDb`, `ConnectionStrings__ReviewsMongo`; резервні значення для запуску без AppHost лежать в `appsettings.json`. Catalog застосовує міграції під час старту (прапорець `Catalog:MigrateAndSeed`), у Production це вимкнено.

## Приклади запитів (Enrollments)

```bash
curl -X POST <api-url>/api/enrollments \
  -H 'Content-Type: application/json' \
  -d '{"studentId": 1, "items": [{"courseId": 101, "units": 1}]}'

curl <api-url>/api/enrollments/1

curl -X POST <api-url>/api/enrollments/1/confirm

curl <api-url>/api/courses

curl <api-url>/api/courses/101
```

Помилки - `ProblemDetails`: неіснуючий Id → `404`, недопустимий перехід статусу / брак місць → `409`, валідація → `400`.
Самоперевірка: ім'я зі спецсимволом (`O'Brien`) не ламає SQL (параметризація); помилка в другому репозиторії транзакції відкочує перший.

## Приклади запитів (Catalog)

```bash
curl '<catalog-api-url>/api/courses?page=1&pageSize=2'
curl '<catalog-api-url>/api/courses?category=web&minPrice=1000&sortBy=price&sortDir=desc&page=1&pageSize=5'
curl '<catalog-api-url>/api/courses/101'
curl '<catalog-api-url>/api/courses/by-instructor/1'
curl '<catalog-api-url>/api/categories/1'
curl '<catalog-api-url>/api/categories/web/recent-courses?since=2026-01-01'

curl -X POST '<catalog-api-url>/api/courses' \
  -H 'Content-Type: application/json' \
  -d '{"title":"Новий курс","slug":"new-course","price":900,"instructorId":1,"syllabus":"Програма","durationHours":12,"categoryIds":[1]}'

curl -X PUT '<catalog-api-url>/api/courses/101/price' \
  -H 'Content-Type: application/json' -d '{"price":990}'

curl -X DELETE '<catalog-api-url>/api/courses/101'
```

Пагінація повертає `items`, `totalCount`, `page`, `pageSize`, `totalPages`, `hasNext`, `hasPrevious`. Фільтри, сортування (дозволені поля: `title`, `price`, `createdAt`) і `Skip/Take` виконуються в SQL (`OFFSET ... FETCH`). Складні вибірки описані специфікаціями `CourseFilterSpec` і `CoursesByInstructorSpec`; репозиторій застосовує їх через `WithSpecification`. Валідація: DataAnnotations на DTO + FluentValidation в окремих класах. Логи Serilog містять `CorrelationId`, `RequestPath` і SQL-команди EF Core: eager-репозиторій виконує один запит, explicit - два.

Виняток логує лише `ApiExceptionHandler` (один ERR-рядок зі стеком). `CorrelationIdMiddleware` стоїть зовні `UseExceptionHandler`, тому підсумковий рядок запиту містить реальний статус відповіді (`404`, `409`), а не `500`. Вбудований лог `ExceptionHandlerMiddleware` вимкнено через `MinimumLevel.Override` у `Platform.ServiceDefaults`, щоб виняток не дублювався.

## Сервіс Reviews (Clean Architecture + CQRS)

| Шар            | Проєкт                   | Залежить від                | Вміст                                                                                    |
| -------------- | ------------------------ | --------------------------- | ---------------------------------------------------------------------------------------- |
| Domain         | `Reviews.Domain`         | нічого                      | `BaseEntity`, `ValueObject`, `Review`, `Comment`, `Author`, `Rating`, `IReviewRepository` |
| Application    | `Reviews.Application`    | Domain                      | команди, запити, валідатори, behaviors, `IReviewQueries`, `ReviewProfile`                |
| Infrastructure | `Reviews.Infrastructure` | Application, Domain         | `MongoDbContext`, `BsonClassMap`, `ReviewRepository`, `ReviewQueries`, сідинг та індекси  |
| WebApi         | `Reviews.WebApi`         | Application, Infrastructure | контролери, ProblemDetails, DI, Swagger                                                   |

`Reviews.Domain` посилається лише на `MongoDB.Bson` (тип `ObjectId`), драйвера доступу до БД у ньому немає.

Одна операція API = одна команда або запит MediatR. Обробники не працюють з `IMongoCollection`: записи йдуть через `IReviewRepository`, читання - через `IReviewQueries` (проєкція одразу в `ReviewDto` з масивами `photos` і `comments`). Ланцюжок behaviors: `LoggingBehavior` → `ValidationBehavior` → `PerformanceBehavior`; валідатор кидає `ValidationException` до виклику обробника. Правило `CreateReviewCommandValidator` робить асинхронний запит `ExistsAsync`, тому повторний відгук того самого автора на курс відхиляється з `400`; unique-індекс `{courseId, author.userId}` повертає `409` у разі гонки.

BSON-мапінг заданий у `MongoMappings` без атрибутів у домені: camelCase, `IgnoreExtraElements`, приватні поля `_photos`/`_comments`, `Rating` через власний `RatingBsonSerializer` (у документі лишається `int`), `Author` через конструктор. Оновлення документа виконується `ReplaceOne` з фільтром за поточною версією, видалення - `DeleteOne` з тим самим фільтром; розбіжність версій дає `409`. GET повертає `ETag` з версією, PUT приймає `If-Match` і відхиляє застарілу версію.

Мапінг помилок: `NotFoundException` → `404`, `ValidationException` і `DomainException` → `400`, `ConflictException` і `MongoWriteException` з `DuplicateKey` → `409`, інші `MongoWriteException` → `400`, `MongoConnectionException` і таймаут вибору сервера → `503`. Тіло - `ProblemDetails`; у Development додається `exception`, у логах - `CorrelationId` і `RequestPath`. `GET /health/ready` виконує ping MongoDB через зареєстрований `AddMongoDb` з `IMongoDatabase` і повертає `200 Healthy`, `200 Degraded` або `503 Unhealthy`.

Конфігурація - секція `MongoDb` (`ConnectionString`, `DatabaseName`). `ReviewsMongo` в AppHost - ім'я ресурсу й connection string; фактична назва бази задається `MongoDb:DatabaseName`. Development використовує `FeedbackDb`, Staging - `FeedbackDb_Staging`, Production - `FeedbackDb` з вимкненим сідингом (`Reviews:Seed = false`); рядок підключення перевизначається `ConnectionStrings__ReviewsMongo`. Старт:

```bash
docker run -d --name feedback-mongo -p 27017:27017 mongo:7
dotnet run --project Services/Reviews/Reviews.WebApi
```

Swagger доступний на `http://localhost:5084/swagger`, порт заданий у `Properties/launchSettings.json`.

```bash
curl 'http://localhost:5084/api/reviews?page=2&pageSize=2'
curl 'http://localhost:5084/api/reviews?courseId=101&search=LINQ'
curl 'http://localhost:5084/api/reviews/<id>'

curl -X POST 'http://localhost:5084/api/reviews' \
  -H 'Content-Type: application/json' \
  -d '{"courseId":102,"courseTitle":"ASP.NET Core API","authorId":"7","authorName":"Ірина","rating":5,"text":"Найкращий курс на потоці."}'

curl -X PUT 'http://localhost:5084/api/reviews/<id>' \
  -H 'Content-Type: application/json' -H 'If-Match: "0"' \
  -d '{"rating":4,"text":"Оновлений текст"}'

curl -X DELETE 'http://localhost:5084/api/reviews/<id>'
```

Пагінація і фільтрація виконуються в MongoDB (`$match`, `$sort`, `$skip`, `$limit`, `$project`), `totalCount` рахує `countDocuments`. Сідинг ідемпотентний: повторний запуск не додає документів, а `db.reviews.getIndexes()` показує складені, text- та unique-індекси. Доменні правила перевіряються `dotnet test Services/Reviews/Reviews.Domain.Tests` (5 тестів, зокрема `Rating = 10` кидає `DomainException` без БД). Знімки: `docs/lab-04/swagger.png`, `docs/lab-04/pagination.png`, `docs/lab-04/validation-400.png`, `docs/lab-04/mongodb-document-indexes.png`, `docs/lab-04/compass-document.png`, `docs/lab-04/logs-validation.png`, `docs/lab-04/conflict-409.png`. Команди для повторного зняття описані в `docs/lab-04/manual-checks.md`.

## API Gateway і Aggregator (Лаб. 05)

`Platform.ApiGateway` маршрутизує запити за catch-all правилами YARP. Адреси кластерів записані логічними іменами (`http://enrollments-api`, `http://catalog-api`, `http://reviews-api`, `http://aggregator`), фізичні порти підставляє service discovery Aspire. Маршрут Aggregator має `Order: 1`, решта - `Order: 2`. Gateway генерує `X-Correlation-Id`, якщо клієнт його не надіслав, повертає заголовок у відповіді та логує метод, шлях, маршрут, статус і тривалість.

`Platform.Aggregator` збирає картку запису на курс: запис з Enrollments, актуальні дані курсів з Catalog і відгуки з Reviews. Виклики Catalog і Reviews виконуються паралельно через `Task.WhenAll`; спільний `CancellationToken` передається в усі запити. Якщо сервіс недоступний, відповідь містить доступні дані й `warnings`, а не `500`. Приклади запитів через Gateway наведені в розділі запуску платформи.

Зупиніть `reviews-api` у Dashboard і повторіть запит до Aggregator: `warnings` міститиме `reviews-api is unavailable`, поля `enrollment` і `courses` залишаться заповненими. Прямий запит до `<reviews-api-url>/api/reviews` в обхід Gateway не містить `X-Correlation-Id` на вході; сервіс згенерує власний id і поверне його у відповіді. Через Gateway клієнтський id зберігається в усьому ланцюжку.

`ServiceDefaults` підключає Serilog (`CompactJsonFormatter`, enrichers `ServiceName`, `EnvironmentName`, `MachineName`, `ThreadId`, `TraceId`, `SpanId`), OpenTelemetry для ASP.NET Core, `HttpClient`, `SqlClient` і MongoDB, а також `CorrelationIdMiddleware` та `CorrelationIdHandler`. Логи, трасування й метрики всіх сервісів видно в Aspire Dashboard: пошук за `CorrelationId` показує ланцюжок Gateway → Aggregator → сервіси → БД. Знімки: `docs/lab-05/dashboard-resources.png`, `docs/lab-05/trace-aggregator.png`, `docs/lab-05/structured-logs-correlation.png`; команди перевірки - `docs/lab-05/manual-checks.md`.

## Кешування та gRPC (Лаб. 06)

`AppHost` додає контейнер `redis` з іменованим volume (`WithDataVolume`) і передає `ConnectionStrings__redis` у `catalog-api` та `aggregator` через `WithReference`; обидва ресурси чекають `WaitFor(redis)`. Для запуску без AppHost у `appsettings.json` лежить `localhost:6379`.

### Дворівневий кеш каталогу

`Catalog.Bll/Caching/TwoLevelCache` працює за схемою cache-aside: L1 (`IMemoryCache`) → L2 (`IDistributedCache` поверх Redis) → EF Core. L1 обмежений `SizeLimit = 10_000`, кожен запис має `Size = 1`; для L1 задані sliding expiration та absolute expiration (не більше двох sliding-періодів), для L2 - абсолютний TTL. Після запису в БД ключі видаляються з обох рівнів у тій самій операції, причина пише в лог.

| Ключ | Дані | L1 sliding / максимум | L2 |
| --- | --- | --- | --- |
| `catalog:course:{id}` | картка курсу | 1 хв / 2 хв | 10 хв |
| `catalog:instructor:{id}:courses` | курси інструктора | 1 хв / 2 хв | 5 хв |
| `catalog:categories:all` | довідник категорій | 5 хв / 10 хв | 30 хв |

`CacheMetrics` створює Meter `Catalog.Cache` з лічильниками `cache.hits`, `cache.misses` (тег `cache.level` = `L1`/`L2`) та `cache.invalidations`. Meter підключений у `ServiceDefaults`, тому метрики видні в Dashboard.

### gRPC-контракти

`*.proto` лежать у `Protos/` кожного API. У сервісах вони підключені з `GrpcServices="Server"`, в `Platform.Aggregator` ті самі файли підключені з `GrpcServices="Client"`. Сервіси працюють поверх HTTPS-endpoint з ALPN, тому Kestrel одночасно віддає REST на `http://` і gRPC на `https://`.

| Контракт | RPC | Реалізація |
| --- | --- | --- |
| `catalog.proto` | `GetCourse`, `GetCoursesByIds` | `Catalog.Api/Grpc/CatalogGrpcService` |
| `enrollments.proto` | `GetEnrollment` | `Enrollments.Api/Grpc/EnrollmentsGrpcService` |
| `reviews.proto` | `GetForCourses` | `Reviews.WebApi/Grpc/ReviewsGrpcService` |

`GetCourse` і `GetEnrollment` кидають `RpcException` зi статусом `NotFound` для відсутнього Id та `InvalidArgument` для невалідного. `GrpcLoggingInterceptor` з `ServiceDefaults` логує метод, peer, статус і тривалість, а `CorrelationIdHandler` додає `X-Correlation-Id` у метадані кожного виклику.

### Aggregator

`Platform.Aggregator` реєструє три типізовані gRPC-клієнти через `AddGrpcClient` з адресами `https://enrollments-api`, `https://catalog-api`, `https://reviews-api`; service discovery підставляє порти з AppHost. Запит до `catalog` і `reviews` виконується паралельно через `Task.WhenAll`, кожен виклик має дедлайн 3 с. `RpcException` зі статусом `Unavailable` або `DeadlineExceeded` перетворюється на `warnings`, а не на `500`; інші помилки не маскуються.

Складена відповідь кешується в Redis за ключем `aggregator:enrollment:{id}:full` на 30 с. Кеш перевіряється до gRPC-викликів; відповіді з `warnings` не кешуються. Якщо Redis недоступний, Aggregator логує попередження і збирає дані напряму.

Перевірка:

```bash
curl -s 'http://localhost:5000/api/courses/101'
curl -s 'http://localhost:5000/api/courses/101'
curl -s 'http://localhost:5000/api/aggregator/enrollments/1/full'
```

Другий GET курсу не має SQL-span у Traces. Trace агрегувального запиту містить gRPC client/server spans у одному ланцюжку Gateway → Aggregator → сервіси → БД. Вимірювання: HTTP-агрегація Лаб. 05 - 350 ms, gRPC з холодним кешем Catalog - 50.53 ms. В обох трейсах присутній SQL-запит Catalog, тому різниця показує транспорт, а не ефект кешу.

Знімки: `docs/lab-06/dashboard-resources.png`, `docs/lab-06/trace-cache-hit.png`, `docs/lab-06/trace-grpc-aggregator.png`, `docs/lab-06/metrics-cache-hits.png`, `docs/lab-06/structured-logs-cache.png`. Повні команди й очікувані логи описані в `docs/lab-06/manual-checks.md`.

## Резильєнтність, метрики та health checks (Лаб. 07)

### Політики resilience для клієнтів Aggregator

Усі клієнти створюються фабрикою `HttpClient`, тому глобальний Standard Resilience Handler з `ServiceDefaults` діє для HTTP- і gRPC-клієнтів. Для Aggregator політики задані окремо: спочатку `RemoveAllResilienceHandlers`, потім власний `AddStandardResilienceHandler`. Обробники не нашаровуються.

| Клієнт | Роль | Retry | Затримка | Attempt / Total | Circuit breaker | gRPC retry |
| --- | --- | --- | --- | --- | --- | --- |
| Enrollments | критичний, джерело картки | 2 | 0.5 с, exponential + jitter | 5 с / 20 с | 0.5 / 10 запитів / 30 с / 10 с | 3 спроби, backoff 0.5–1 с, `Unavailable` |
| Catalog | критичний, без курсів картка неповна | 2 | 0.5 с, exponential + jitter | 6 с / 25 с | 0.3 / 10 / 30 с / 15 с | 4 спроби, backoff 0.5–2 с, `Unavailable`, `ResourceExhausted` |
| Reviews | некритичний, Aggregator повертає часткову відповідь | 1 | 0.5 с | 2 с / 6 с | 0.7 / 5 / 30 с / 5 с | 2 спроби, backoff 0.5–1 с, `Unavailable` |

Значення `FailureRatio / MinimumThroughput / SamplingDuration / BreakDuration`. `MinimumThroughput` зменшено для навчального стенду: 10 для Enrollments і Catalog, 5 для Reviews, інакше breaker не встигає набрати статистику. `SamplingDuration = 30 с` більший за подвоєний найдовший `AttemptTimeout`.

HTTP-повтори для небезпечних методів вимкнені глобально через `Retry.DisableForUnsafeHttpMethods()`. gRPC-виклики Aggregator є read-only RPC, тому для них діє окрема retry-політика за статусами `Unavailable` і `ResourceExhausted`.

Переходи breaker пишуть `OnOpened` (Error), `OnClosed` (Information) і `OnHalfOpened` (Warning) через `ILogger`. Polly публікує метрики в Meter `Polly`, він доданий у `ServiceDefaults` разом з Meter-ами сервісів.

### Бізнес-метрики

Кожен сервіс має Singleton-клас у `Metrics/` з `IMeterFactory`. Meter названий за сервісом, усі інструменти мають префікс простору імен.

| Сервіс | Meter | Лічильники | Гістограма | Gauge |
| --- | --- | --- | --- | --- |
| Enrollments | `Enrollments.Api` | `enrollments.created` (`status`), `enrollments.confirmed` (`status`), `enrollments.cancelled` (`status`) | `enrollments.operation.duration` (`operation`, `result`) | `enrollments.awaiting_validation` |
| Catalog | `Catalog.Api` | `catalog.courses.created` (`has.detail`, `category.bucket`), `catalog.courses.price_updated` (`price.band`), `catalog.courses.deleted` (`source`) | `catalog.operation.duration` (`operation`, `result`) | `catalog.courses.total` |
| Reviews | `Reviews.WebApi` | `reviews.created` (`rating`), `reviews.updated` (`rating`), `reviews.deleted` (`source`) | `reviews.operation.duration` (`operation`, `result`) | `reviews.total` |

`result` має значення `success` або `failure`, тривалість записується в обох випадках. Gauge оновлюється раз на 15 с фоновим poller-ом і показує поточний стан бази. Теги з Id студента, курсу чи відгуку не використовуються: вони дали б високу кардинальність.

Значення тегів:

| Тег | Значення |
| --- | --- |
| `status` | `Submitted`, `SeatsConfirmed`, `Cancelled` |
| `operation` | `create`, `confirm`, `cancel`, `update-price`, `update`, `delete` |
| `result` | `success`, `failure` |
| `has.detail` | `yes`, `no` |
| `category.bucket` | `0`, `1`, `2+` |
| `price.band` | `low`, `medium`, `high` |
| `rating` | `1`..`5` |
| `source` | `api` |

### Health checks

`ServiceDefaults` публікує endpoints у всіх середовищах:

| Endpoint | Що перевіряє | Відповідь |
| --- | --- | --- |
| `/health/live` | лише процес, тег `live` | 200, без зовнішніх залежностей |
| `/health/ready` | тег `ready`, JSON з деталями | 200, `Degraded` або 503 |
| `/health/startup` | тег `startup` | 200 або 503 |
| `/health` | усі перевірки, JSON | використовує AppHost |
| `/alive` | аліас liveness | 200 |

| Перевірка | Сервіс | Теги | Збій |
| --- | --- | --- | --- |
| `enrollments-db` | Enrollments | `database`, `sql`, `ready`, `startup` | `Unhealthy` |
| `catalog-db` | Catalog | `database`, `sql`, `ready`, `startup` | `Unhealthy` |
| `redis-cache` | Catalog, Aggregator | `cache`, `redis`, `ready` | `Degraded` |
| `reviews-db` | Reviews | `database`, `mongodb`, `ready`, `startup` | `Unhealthy` |
| `enrollments-api`, `catalog-api` | Aggregator | `grpc`, `downstream`, `ready` | `Unhealthy` |
| `reviews-api` | Aggregator | `grpc`, `downstream`, `ready` | `Degraded` |

SQL перевіряється запитом `SELECT 1` з таймаутом 5 с, MongoDB - `ping` з таймаутом 5 с, Redis - `PING` з таймаутом 3 с. Автоматичний Redis health check від Aspire вимкнений (`DisableHealthChecks = true`), щоб не було двох перевірок з різними статусами. Сервіси реалізують стандартний gRPC Health Checking Protocol, Aggregator викликає `Check` кожного downstream з дедлайном 2 с.

gRPC health service віддає лише перевірки з тегом `ready` без `cache`. Тому Degraded Redis не перетворюється на `NotServing` і не робить Catalog недоступним для Aggregator. Перевірка каталогу в Aggregator залишається Healthy, поки живий SQL.

Health-клієнти Aggregator не використовують resilience: перевірка має падати швидко, тому для них `RemoveAllResilienceHandlers` і явний дедлайн 2 с. Це не стосується бізнес-клієнтів, які мають повний набір політик.

`Platform.AppHost` додає `.WithHttpHealthCheck("/health")` кожному проєкту. `WaitFor` залишений лише для критичних залежностей: БД, Enrollments і Catalog. Redis і Reviews підключаються через `WithReference` без `WaitFor`, тому їх недоступність на старті не блокує Catalog, Aggregator чи Gateway. Dashboard показує стан ресурсу, а деталі health checks доступні в його картці.

Health endpoints відкриті й поза Development, як вимагає лабораторна. У Development JSON містить `exception.Message` і `description`; поза Development обидва поля дорівнюють `null`, щоб не віддавати текст винятків і внутрішні деталі. У production endpoints треба закрити на ingress або дозволити лише службі моніторингу.

## Подієва архітектура з RabbitMQ (Лаб. 08)

### Брокер і топологія

`Platform.AppHost` додає контейнер `messaging` через `AddRabbitMQ("messaging")` з `.WithDataVolume()` і `.WithManagementPlugin()`. Management UI відкривається на порту 15672, облікові дані генерує Aspire. `catalog-api`, `enrollments-api` і `reviews-api` отримують `ConnectionStrings__messaging` через `WithReference` і чекають брокер через `WaitFor`.

Клієнт RabbitMQ підключений пакетом `Aspire.RabbitMQ.Client` 13.6.5 і `RabbitMQ.Client` 7.2.2. `builder.AddEventBus()` у `Platform.EventBus` викликає `AddRabbitMQClient("messaging")`, реєструє singleton `IConnection`, health check `RabbitMQ.Client`, publisher і hosted service, що оголошує топологію. Канали `IChannel` створюються на кожну операцію або consumer.

Топологію описано в `Platform.EventBus/RabbitMqTopology.cs`:

| Об'єкт | Ім'я | Призначення |
| --- | --- | --- |
| topic exchange | `catalog-events` | події Каталогу |
| routing key | `course.price_changed` | зміна ціни |
| routing key | `course.renamed` | зміна назви |
| queue | `enrollments.catalog-sync` | оновлення локального довідника курсів |
| queue | `reviews.catalog-sync` | оновлення `courseTitle` у відгуках |
| DLX + DLQ | `enrollments.catalog-sync.dlx` + `enrollments.catalog-sync.dlq` | необроблені повідомлення Enrollments |
| DLX + DLQ | `reviews.catalog-sync.dlx` + `reviews.catalog-sync.dlq` | необроблені повідомлення Reviews |

`enrollments.catalog-sync` прив'язана шаблоном `course.*`, тому отримує обидві події. `reviews.catalog-sync` прив'язана лише до `course.renamed`: ціна у відгуках не зберігається. Кожна черга має власний DLX, тому відхилене повідомлення не дублюється в чужий DLQ.

### Події та публікація

`Platform.EventBus/Events/IntegrationEvent.cs` містить базовий незмінний record з `EventId`, `OccurredAt` і `CorrelationId`. `CourseEvents.cs` описує `CoursePriceChangedEvent` і `CourseRenamedEvent`; обидва самодостатні й не потребують звернення до Каталогу.

`Catalog.Bll/CourseService` зберігає зміни через `SaveChangesAsync`, інвалідує кеш і лише потім викликає `IEventPublisher.PublishAsync`. `RabbitMqEventPublisher` серіалізує подію в camelCase JSON і виставляє `Persistent`, `ContentType`, `MessageId = EventId`, `Type`, `CorrelationId`, `x-correlation-id`, `x-event-type`, `x-schema-version`. Заголовок `traceparent` додає `RabbitMqTrace.Inject` через стандартний propagator OpenTelemetry.

Зміна ціни: `PUT /api/courses/{id}/price`. Зміна назви: `PUT /api/courses/{id}/title` з тілом `{"title":"..."}`.

### Споживачі, ідемпотентність і помилки

`RabbitMqEventConsumer` у `Platform.EventBus` створює канал, оголошує топологію, виставляє `BasicQos(0, 10, false)`, підписується через `AsyncEventingBasicConsumer` з `autoAck: false` і перезапускає канал після `ChannelShutdownAsync`. Похідні класи лежать у `Enrollments.Api/IntegrationEvents/` і `Reviews.WebApi/IntegrationEvents/`.

Після успішного обробника викликається `BasicAckAsync`. Помилка JSON або невідомий тип одразу відправляє повідомлення в DLQ через `BasicNackAsync(requeue: false)`. Інші винятки перепублікуються з заголовком `x-retry-count`; після трьох повторів повідомлення також іде в DLQ. Кожна спроба логується з `MessageId`, `EventId`, `EventType`, `CorrelationId` і повним стеком.

Ідемпотентність:

- SQL Server: таблиця `dbo.ProcessedEvents (EventId PRIMARY KEY, EventType, ProcessedAt)`; `INSERT` EventId і оновлення `dbo.Courses` виконуються в одній транзакції `UnitOfWork`. Дублікат ключа повертає `false`, транзакція відкочується, подія підтверджується без повторної зміни даних.
- MongoDB: колекція `processedEvents`, де `_id` = `EventId`; обробник перевіряє `_id` перед `UpdateManyAsync` і записує його після оновлення. Повторна доставка не змінює `courseTitle` удруге. MongoDB в Aspire працює як standalone, тому транзакції між колекціями немає; `$set` ідемпотентний.

Реплікація за політикою Лаб. 01:

- Enrollments оновлює `dbo.Courses.Title` і `dbo.Courses.Price`; `EnrollmentItems.CourseTitle` і `EnrollmentItems.UnitPrice` лишаються знімком на момент запису.
- Reviews оновлює `courseTitle` в усіх документах `reviews` з тим самим `courseId`.

### Перевірка

```bash
dotnet build Platform.slnx --nologo
dotnet test Services/Reviews/Reviews.Domain.Tests --nologo

curl -X PUT '<catalog-api-url>/api/courses/101/price' \
  -H 'Content-Type: application/json' -H 'X-Correlation-Id: demo-price-1' \
  -d '{"price":1350}'

curl -X PUT '<catalog-api-url>/api/courses/101/title' \
  -H 'Content-Type: application/json' -H 'X-Correlation-Id: demo-rename-1' \
  -d '{"title":"Основи C# 12"}'
```

Очікування: `dbo.Courses` містить нову ціну й назву, `dbo.EnrollmentItems` зберігає стару ціну, `reviews.courseTitle` містить нову назву. Логи publisher і consumer мають однаковий `TraceId`; у Traces Aspire видно HTTP → publish → consume → SQL/Mongo. Повторна публікація того самого `EventId` пише `Duplicate ... skipped`. Некоректний JSON через Management UI збільшує `messages` у відповідному DLQ. Зупинка `enrollments-api`, зміна ціни й повторний старт обробляють накопичені повідомлення.

Локальний запуск без AppHost:

```bash
docker run -d --name messaging -p 5672:5672 -p 15672:15672 rabbitmq:4-management
dotnet run --project Services/Catalog/Catalog.Api
dotnet run --project Services/Enrollments/Enrollments.Api
dotnet run --project Services/Reviews/Reviews.WebApi
```

Для існуючої бази `EnrollmentsDb` таблицю `ProcessedEvents` створює `db/p1/schema.sql`; на вже розгорнутій базі виконайте `CREATE TABLE dbo.ProcessedEvents (...)` з цього файлу вручну. Для зміни аргументів уже створеної черги видаліть її в Management UI перед перезапуском: RabbitMQ не дозволяє змінити `x-dead-letter-exchange` на льоту.

Карта критеріїв з точними файлами, символами й командами перевірки: `docs/lab-08/criteria.md`. Додаткові ручні сценарії та знімки: `docs/lab-08/manual-checks.md`.

## Розвиток (наступні лаби)

Нові контексти й можливості додаються за тією ж схемою: новий каталог у `Services/`, скрипти в `db/pN/`, ресурс в `Platform.AppHost`, рядок у таблиці дублювання вище. Існуючі БД і контракти зворотно сумісні.
