# Learning Platform - мікросервісна платформа онлайн-курсів

Навчальна платформа: студенти переглядають каталог курсів, записуються на курси, оплачують навчання та проходять програму; викладачі ведуть курси; студенти залишають відгуки й обговорюють матеріали.
Домен обрано як розширюваний: нові можливості (розклад, платежі, сертифікати, сповіщення, аналітика) додаються новими контекстами без зміни існуючих.

## Bounded Contexts

| Контекст / БД | Сервіс                          | Технологія                        | Сутності                                                                                                                          |
| ------------- | ------------------------------- | --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| Enrollments   | Записи на курси (транзакційний) | SQL Server + ADO.NET / Dapper     | Student, Enrollment, EnrollmentDetail (1:1), Course (локальна копія), EnrollmentItem (M:N)                                        |
| Catalog       | Каталог курсів                  | SQL Server + EF Core (Code First) | Course, Category, CourseCategory (M:N з полем AddedAt), CourseDetail (1:1), Instructor (1:N → Course), CourseImage (N:1 → Course) |
| Feedback      | Відгуки та обговорення          | MongoDB                           | reviews, discussions, ratings                                                                                                     |

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
├── Platform.AppHost/
├── Services/
│   ├── Enrollments/
│   │   ├── Enrollments.Api/
│   │   ├── Enrollments.Bll/
│   │   ├── Enrollments.Dal/
│   │   └── Enrollments.Domain/
│   ├── Catalog/
│   │   ├── Catalog.Api/
│   │   ├── Catalog.Bll/
│   │   ├── Catalog.Dal/
│   │   │   ├── Configurations/
│   │   │   └── Migrations/
│   │   └── Catalog.Domain/
│   └── Feedback/
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

mongosh feedbackdb db/p3/collections.js
mongosh feedbackdb db/p3/seed.js
```

`CatalogDb` створюється міграцією `InitialCreate` з моделей EF Core. `db/p2/schema.sql` і `db/p2/seed.sql` - SQL-версія Лаб. 01, `db/p2/ef-initial.sql` - ідемпотентний скрипт, згенерований з міграції (`dotnet ef migrations script --idempotent`). Курси наповнюються `DbInitializer` під час старту API, довідники - через `HasData`.

Повторний запуск `seed` не створює дублікатів (`IF NOT EXISTS` / `find-or-create` / перевірка наявності курсів). ERD: `docs/erd-p1.svg`, `docs/erd-p2.svg` (джерела для dbdiagram.io: `docs/erd-p1.dbml`, `docs/erd-p2.dbml`).

## Запуск сервісів (Aspire)

Перед першим запуском встановіть Aspire workload: `sudo dotnet workload install aspire`.

```bash
dotnet run --project Platform.AppHost
```

Dashboard показує ресурси (`enrollments-api`, `catalog-api` + БД) у статусі Running. URL API - з Dashboard; Swagger - `<api-url>/swagger`. Обидва сервіси читають рядки підключення з `Platform.AppHost/appsettings.json`.

Залежності: `Api → Bll → Dal`, `Domain` - спільний. Рядки підключення - в `appsettings.json`, перевизначення - `ConnectionStrings__CatalogDb=...` або `ConnectionStrings__EnrollmentsDb=...`. Сервіс Catalog застосовує міграції під час старту (прапорець `Catalog:MigrateAndSeed`), у Production це вимкнено.

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
curl '<catalog-api-url>/api/courses/1'
curl '<catalog-api-url>/api/courses/by-instructor/1'
curl '<catalog-api-url>/api/categories/1'
curl '<catalog-api-url>/api/categories/web/recent-courses?since=2026-01-01'

curl -X POST '<catalog-api-url>/api/courses' \
  -H 'Content-Type: application/json' \
  -d '{"title":"Новий курс","slug":"new-course","price":900,"instructorId":1,"syllabus":"Програма","durationHours":12,"categoryIds":[1]}'

curl -X PUT '<catalog-api-url>/api/courses/1/price' \
  -H 'Content-Type: application/json' -d '{"price":990}'

curl -X DELETE '<catalog-api-url>/api/courses/1'
```

Пагінація повертає `items`, `totalCount`, `page`, `pageSize`, `totalPages`, `hasNext`, `hasPrevious`. Фільтри, сортування (дозволені поля: `title`, `price`, `createdAt`) і `Skip/Take` виконуються в SQL (`OFFSET ... FETCH`). Складні вибірки описані специфікаціями `CourseFilterSpec` і `CoursesByInstructorSpec`; репозиторій застосовує їх через `WithSpecification`. Валідація: DataAnnotations на DTO + FluentValidation в окремих класах. Логи Serilog містять `CorrelationId`, `RequestPath` і SQL-команди EF Core: eager-репозиторій виконує один запит, explicit - два.

## Розвиток (наступні лаби)

Нові контексти й можливості додаються за тією ж схемою: новий каталог у `Services/`, скрипти в `db/pN/`, ресурс в `Platform.AppHost`, рядок у таблиці дублювання вище. Існуючі БД і контракти зворотно сумісні.
