# Лаб. 08: ручні сценарії

Команди наведені для локального запуску без AppHost. Замініть `<catalog-api-url>`, `<enrollments-api-url>`, `<reviews-api-url>` на фактичні адреси, якщо використовуєте Aspire Dashboard.

Самоперевірка коду:

```bash
dotnet build Platform.slnx --nologo
dotnet test Platform.slnx --nologo
```

Очікування: 0 помилок, 0 попереджень, 10/10 тестів.

## 1. Запуск інфраструктури

```bash
docker run -d --name lab8-sql \
  -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Pass@word1' \
  -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest

docker run -d --name lab8-mongo -p 27017:27017 mongo:8.0
docker run -d --name lab8-redis -p 6379:6379 redis:8.2

docker run -d --name lab8-rabbit \
  -p 5672:5672 -p 15672:15672 rabbitmq:4-management
```

Для перевірки транзакції Reviews запустіть `Platform.AppHost`: він піднімає Mongo як replica set `rs0` і передає `MongoDb__ReplicaSet=rs0`. Перед першим запуском задайте `Parameters:sql-password`, `Parameters:mongo-user` і `Parameters:mongo-password` у user-secrets AppHost. Локальний `mongo:8.0` без AppHost працює standalone, тому Reviews використовує ідемпотентний fallback без транзакції.

`EnrollmentsDb`:

```bash
docker exec lab8-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Pass@word1' -C \
  -Q "IF DB_ID('EnrollmentsDb') IS NULL CREATE DATABASE EnrollmentsDb"

docker exec -i lab8-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Pass@word1' -C -d EnrollmentsDb < db/p1/schema.sql
docker exec -i lab8-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Pass@word1' -C -d EnrollmentsDb < db/p1/procedures.sql
docker exec -i lab8-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Pass@word1' -C -d EnrollmentsDb < db/p1/seed.sql
```

Сервіси:

```bash
dotnet run --project Services/Catalog/Catalog.Api
dotnet run --project Services/Enrollments/Enrollments.Api
dotnet run --project Services/Reviews/Reviews.WebApi
```

Перевірка health:

```bash
curl -s http://localhost:5081/health | jq '.checks[] | select(.name == "RabbitMQ.Client")'
curl -s http://localhost:5082/health | jq '.checks[] | select(.name == "RabbitMQ.Client")'
curl -s http://localhost:5084/health | jq '.checks[] | select(.name == "RabbitMQ.Client")'
```

Очікування: `"status": "Healthy"` у всіх трьох відповідях.

## 2. Топологія в Management UI

Відкрийте `http://localhost:15672`, увійдіть `guest` / `guest`.

Exchanges:

- `catalog-events`, тип `topic`, durable;
- `enrollments.catalog-sync.dlx`, тип `fanout`, durable;
- `reviews.catalog-sync.dlx`, тип `fanout`, durable.

Queues:

- `enrollments.catalog-sync`, binding `course.*`, аргумент `x-dead-letter-exchange = enrollments.catalog-sync.dlx`;
- `enrollments.catalog-sync.dlq`, binding до свого DLX;
- `reviews.catalog-sync`, binding `course.renamed`, аргумент `x-dead-letter-exchange = reviews.catalog-sync.dlx`;
- `reviews.catalog-sync.dlq`, binding до свого DLX.

Знімки: `docs/lab-08/management-exchanges.png` і `docs/lab-08/management-queues.png`.

## 3. Зміна ціни

Початковий стан:

```sql
SELECT Id, Title, Price FROM dbo.Courses WHERE Id = 101;
SELECT CourseId, CourseTitle, UnitPrice FROM dbo.EnrollmentItems WHERE CourseId = 101;
```

Запит:

```bash
curl -i -X PUT '<catalog-api-url>/api/courses/101/price' \
  -H 'Content-Type: application/json' \
  -H 'X-Correlation-Id: demo-price-1' \
  -d '{"price":1350}'
```

Очікування: `204 No Content`, у логах Catalog `Published CoursePriceChangedEvent ... CorrelationId "demo-price-1"`, у логах Enrollments `Applied CoursePriceChangedEvent ... price 1200 -> 1350`, потім `Acked`.

SQL:

```sql
SELECT Id, Title, Price FROM dbo.Courses WHERE Id = 101;
SELECT CourseId, CourseTitle, UnitPrice FROM dbo.EnrollmentItems WHERE CourseId = 101;
SELECT EventId, EventType FROM dbo.ProcessedEvents ORDER BY ProcessedAt;
```

`dbo.Courses.Price` = 1350. `dbo.EnrollmentItems.UnitPrice` = 1200, тобто знімок не змінився.

## 4. Зміна назви

```bash
curl -i -X PUT '<catalog-api-url>/api/courses/101/title' \
  -H 'Content-Type: application/json' \
  -H 'X-Correlation-Id: demo-rename-1' \
  -d '{"title":"Основи C# 12"}'
```

SQL:

```sql
SELECT Id, Title FROM dbo.Courses WHERE Id = 101;
SELECT CourseTitle, UnitPrice FROM dbo.EnrollmentItems WHERE CourseId = 101;
```

MongoDB:

```bash
docker exec lab8-mongo mongosh --quiet FeedbackDb \
  --eval 'db.reviews.find({courseId:101},{courseTitle:1}).toArray()'
```

`dbo.Courses.Title` = `Основи C# 12`. `dbo.EnrollmentItems.CourseTitle` = `Основи C#`, знімок не змінився. `reviews.courseTitle` = `Основи C# 12` у всіх документах курсу 101. З `AppHost` Reviews працює з replica set `rs0`, тому `processedEvents` і `UpdateManyAsync` виконуються в одній транзакції.

## 4.1. Створення, видалення та out-of-order

Створення курсу:

```bash
curl -s -X POST '<catalog-api-url>/api/courses' \
  -H 'Content-Type: application/json' \
  -H 'X-Correlation-Id: demo-create-1' \
  -d '{"title":"Подієвий курс","slug":"demo-event-course","price":777,"instructorId":1,"categoryIds":[1]}'
```

Очікування: у `dbo.Courses` з'являється рядок з `SeatsAvailable = 0` і `SourceUpdatedAt`, у `dbo.ProcessedEvents` є `CourseCreatedEvent`.

Видалення:

```bash
curl -i -X DELETE '<catalog-api-url>/api/courses/<new-id>' \
  -H 'X-Correlation-Id: demo-delete-1'
```

Очікування: якщо на курс немає `EnrollmentItems`, рядок `dbo.Courses` зникає, у `ProcessedEvents` є `CourseDeletedEvent`. Якщо позиції є, рядок лишається для історії, а в логах буде `was retained`.

Out-of-order: опублікуйте через Management UI `CourseRenamedEvent` для курсу 101 з `occurredAt` раніше за поточний `SourceUpdatedAt`.

Очікування: `dbo.Courses.Title` і `reviews.courseTitle` не змінюються, у логах `stale event`. Подія з новішим `occurredAt` змінює обидві копії.

## 5. Ідемпотентність

Повторіть публікацію тієї самої події з Management UI:

1. Exchanges → `catalog-events` → Publish message.
2. Routing key `course.renamed`.
3. Properties: `message_id` = EventId попередньої події, `type` = `CourseRenamedEvent`, header `x-correlation-id` = `demo-duplicate`.
4. Payload: JSON події з тим самим `eventId`.

Очікування: `Duplicate CourseRenamedEvent ... skipped`, далі `Acked`. `ProcessedEvents` не отримує нового рядка, `reviews.courseTitle` не змінюється вдруге.

## 6. Повтори та DLQ

Постійний збій, некоректний JSON:

```bash
curl -u guest:guest -X POST \
  'http://localhost:15672/api/exchanges/%2F/catalog-events/publish' \
  -H 'content-type: application/json' \
  -d '{
    "properties": {
      "type": "CourseRenamedEvent",
      "message_id": "malformed-1",
      "headers": {"x-correlation-id": "demo-malformed"}
    },
    "routing_key": "course.renamed",
    "payload": "this is not json",
    "payload_encoding": "string"
  }'
```

Очікування: у логах обох consumer-ів `Malformed ... sending to DLQ`. У кожному DLQ рівно одне повідомлення, у заголовках `x-death` з `reason: rejected` і назвою своєї черги. Знімок: `docs/lab-08/dlq-message.png`.

Тимчасовий збій і три повтори:

```bash
docker exec lab8-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Pass@word1' -C -d EnrollmentsDb \
  -Q "EXEC sp_rename 'dbo.ProcessedEvents', 'ProcessedEvents_off'"
```

Опублікуйте коректну `CourseRenamedEvent` для курсу 102 з новим `eventId` і routing key `course.renamed`.

Очікування в логах Enrollments: `Retry 1/3`, `Retry 2/3`, `Retry 3/3` із затримкою 1, 2 і 4 с, потім `failed after 3 attempts; sending to DLQ`. У DLQ видно `x-retry-count = 3` і `x-death`.

Поверніть таблицю:

```bash
docker exec lab8-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Pass@word1' -C -d EnrollmentsDb \
  -Q "EXEC sp_rename 'dbo.ProcessedEvents_off', 'ProcessedEvents'"
```

## 7. Обробка після перезапуску

1. Зупиніть `enrollments-api`.
2. Змініть ціну двох курсів через Catalog API.
3. Перевірте, що `enrollments.catalog-sync` має `messages > 0`, а `consumers = 0`.
4. Запустіть `enrollments-api` знову.

Очікування: логи `Applied ...` для кожної події, черга порожня, `dbo.Courses` містить обидві нові ціни.

## 8. Трасування

У логах знайдіть `TraceId` рядка `Published` у `catalog-api` і порівняйте з `TraceId` рядків `Applied` та `Acked` у `enrollments-api` або `reviews-api`. Ідентифікатор має збігатися, а `ParentId` consumer-а має вказувати на span publish.

В Aspire Dashboard: Traces → відкрити trace запиту `PUT /api/courses/101/price`. Очікувані span-и: ASP.NET Core request, publish, consumer Enrollments, SQL `UPDATE dbo.Courses`, consumer Reviews для rename. Знімок: `docs/lab-08/trace-publish-consume.png`.

## 9. Логи, які підтверджують критерії

```bash
grep 'Published' <catalog-log>
grep 'Applied\|Acked\|Duplicate\|Retry\|DLQ' <enrollments-log>
grep 'Applied\|Acked\|Duplicate\|Malformed' <reviews-log>
```

Кожен рядок містить `EventType`, `EventId`, `CorrelationId`; помилки містять `Queue`, `MessageId` і повний стек.
