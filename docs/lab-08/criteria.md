# Лаб. 08: карта критеріїв і доказів

Цей документ фіксує, де в репозиторії реалізований кожен критерій Лаб. 08 і чим його підтвердити. Шляхи вказані від кореня репозиторію. Символи й методи стабільніші за номери рядків.

## Швидка перевірка

```bash
dotnet build Platform.slnx --nologo
dotnet test Services/Reviews/Reviews.Domain.Tests --nologo
```

Очікування: 0 помилок, 0 попереджень, 5/5 тестів.

## Критерії

### 1. RabbitMQ в AppHost з volume і Management UI

Де:

- `Platform.AppHost/Program.cs`, змінна `rabbit`:
  - `AddRabbitMQ("messaging")`;
  - `.WithDataVolume()`;
  - `.WithManagementPlugin()`;
  - `catalog`, `enrollments`, `reviews` отримують `.WithReference(rabbit)` і `.WaitFor(rabbit)`.
- `Platform.AppHost/Platform.AppHost.csproj`: `Aspire.Hosting.RabbitMQ` 9.5.2.

Перевірка: після `dotnet run --project Platform.AppHost` у Dashboard є ресурс `messaging`, Management UI відкривається на порту 15672.

### 2. Підключення через Aspire.RabbitMQ.Client і RabbitMQ.Client 7.x

Де:

- `Platform.EventBus/Platform.EventBus.csproj`:
  - `Aspire.RabbitMQ.Client` 13.6.5;
  - `RabbitMQ.Client` 7.2.2.
- `Platform.EventBus/EventBusExtensions.cs`, `AddEventBus`:
  - `builder.AddRabbitMQClient("messaging")` реєструє singleton `IConnection` і health check;
  - `IEventPublisher` і `RabbitMqTopologyInitializer` реєструються в DI.
- `Services/Catalog/Catalog.Api/Program.cs`, `Services/Enrollments/Enrollments.Api/Program.cs`, `Services/Reviews/Reviews.WebApi/Program.cs`: виклик `builder.AddEventBus()`.
- `appsettings.json` кожного сервісу: резервний `ConnectionStrings:messaging` для запуску без AppHost.

Перевірка: `GET /health` кожного сервісу містить `RabbitMQ.Client` зі статусом `Healthy`.

### 3. Декларативна топологія при старті

Де: `Platform.EventBus/RabbitMqTopology.cs`, `DeclareAsync` і `DeclareQueueAsync`.

Що оголошується:

- durable topic exchange `catalog-events`;
- durable черги `enrollments.catalog-sync` і `reviews.catalog-sync`;
- binding `course.*` для Enrollments, `course.renamed` для Reviews;
- окремі fanout DLX `enrollments.catalog-sync.dlx`, `reviews.catalog-sync.dlx`;
- DLQ `enrollments.catalog-sync.dlq`, `reviews.catalog-sync.dlq`;
- аргумент `x-dead-letter-exchange` на кожній робочій черзі.

Виклик: `RabbitMqTopologyInitializer.ExecuteAsync` у `Platform.EventBus/EventBusExtensions.cs` і `RabbitMqEventConsumer.ExecuteAsync`.

Перевірка: Management UI, вкладка Exchanges і Queues. `rabbitmqctl list_bindings` показує `catalog-events -> enrollments.catalog-sync` з ключем `course.*` і `catalog-events -> reviews.catalog-sync` з ключем `course.renamed`.

### 4. Незмінні integration events з EventId і CorrelationId

Де:

- `Platform.EventBus/Events/IntegrationEvent.cs`: abstract record з `EventId`, `OccurredAt`, `CorrelationId`.
- `Platform.EventBus/Events/CourseEvents.cs`: `CoursePriceChangedEvent`, `CourseRenamedEvent`.
- `Services/Catalog/Catalog.Bll/CourseService.cs`, `UpdatePriceAsync` і `UpdateTitleAsync`: `CorrelationId` береться з `ICorrelationAccessor`.

Перевірка: у повідомленні Management UI `properties.message_id` дорівнює `eventId`, заголовок `x-correlation-id` дорівнює `correlationId`.

### 5. IEventPublisher з persistent-публікацією в JSON

Де:

- `Platform.EventBus/IEventPublisher.cs`, метод `PublishAsync`.
- `Platform.EventBus/RabbitMqEventPublisher.cs`, метод `PublishAsync`:
  - `Persistent = true`, `ContentType = "application/json"`;
  - `MessageId = @event.EventId`;
  - `Type = typeof(T).Name`;
  - заголовки `x-correlation-id`, `x-event-type`, `x-schema-version`;
  - `JsonSerializer.SerializeToUtf8Bytes` з `EventBusJson.Options`.
- `Platform.EventBus/RabbitMqTrace.cs`, `Inject`: заголовок `traceparent`.

Перевірка: лог `Published {EventType} {EventId} to {Exchange}/{RoutingKey} CorrelationId ...` у `catalog-api`.

### 6. Події публікуються лише після збереження в БД

Де: `Services/Catalog/Catalog.Bll/CourseService.cs`.

Послідовність у `UpdatePriceAsync` і `UpdateTitleAsync`:

1. `uow.Courses.GetByIdAsync`;
2. зміна доменної сутності;
3. `uow.SaveChangesAsync`;
4. інвалідація кешу;
5. `publisher.PublishAsync`.

Якщо `SaveChangesAsync` кидає виняток, виклик publisher не виконується.

### 7. Consumer як BackgroundService з ручним підтвердженням

Де:

- `Platform.EventBus/RabbitMqEventConsumer.cs`, `ExecuteAsync`:
  - `CreateChannelAsync`, `RabbitMqTopology.DeclareAsync`;
  - `BasicQosAsync(0, 10, false)`;
  - `AsyncEventingBasicConsumer`, `BasicConsumeAsync(autoAck: false)`;
  - перезапуск каналу після `ChannelShutdownAsync`.
- `Services/Enrollments/Enrollments.Api/IntegrationEvents/CatalogEventsConsumer.cs`.
- `Services/Reviews/Reviews.WebApi/IntegrationEvents/CatalogEventsConsumer.cs`.
- Реєстрація: `AddHostedService<CatalogEventsConsumer>()` у `Program.cs` обох сервісів.

### 8. Ack після успіху, Nack без requeue для постійних збоїв

Де: `Platform.EventBus/RabbitMqEventConsumer.cs`, `OnReceivedAsync`.

- успіх: `BasicAckAsync(args.DeliveryTag, multiple: false)`;
- `JsonException` або невідомий тип: `BasicNackAsync(..., requeue: false)`, повідомлення йде в DLX;
- тимчасовий збій: `RetryOrDeadLetterAsync`;
- вичерпані повтори: `BasicNackAsync(..., requeue: false)`.

Логи: `Acked ...`, `Malformed ... sending to DLQ`, `... failed after 3 attempts; sending to DLQ`.

### 9. Ідемпотентні обробники з таблицею оброблених подій

Enrollments, SQL Server:

- `db/p1/schema.sql`: `dbo.ProcessedEvents (EventId UNIQUEIDENTIFIER PRIMARY KEY, EventType, ProcessedAt)`.
- `Services/Enrollments/Enrollments.Dal/ProcessedEventRepository.cs`, `TryAddAsync`: `INSERT`, помилки 2601/2627 означають дублікат.
- `Services/Enrollments/Enrollments.Dal/UnitOfWork.cs`: `ProcessedEvents` доступний у спільній транзакції.
- `CoursePriceChangedHandler.HandleAsync` і `CourseRenamedHandler.HandleAsync`: `BeginTransactionAsync`, `TryAddAsync`, `UpdateSnapshotAsync`, `CommitAsync`; дублікат відкочується й логується.

Reviews, MongoDB:

- `Services/Reviews/Reviews.WebApi/IntegrationEvents/CourseRenamedHandler.cs`, `HandleAsync`: колекція `processedEvents`, `_id` = `EventId`; перевірка перед `UpdateManyAsync`, запис після оновлення. MongoDB в Aspire працює як standalone, тому транзакції між колекціями немає; `$set` ідемпотентний, повторна доставка не змінює `courseTitle`.

Перевірка: повторна публікація події з тим самим `EventId` пише `Duplicate ... skipped`, `SELECT COUNT(*) FROM dbo.ProcessedEvents` не змінюється.

### 10. Реплікація копій даних за політикою Лаб. 01

Де:

- Enrollments:
  - `Services/Enrollments/Enrollments.Dal/CourseRepository.cs`, `UpdateSnapshotAsync`: `UPDATE dbo.Courses SET Title = COALESCE(@Title, Title), Price = COALESCE(@Price, Price)`;
  - `CoursePriceChangedHandler` передає нову ціну й назву;
  - `CourseRenamedHandler` передає нову назву.
- Reviews:
  - `CourseRenamedHandler.HandleAsync`: `context.Reviews.UpdateManyAsync` з фільтром `CourseId` і `Set(r => r.CourseTitle, @event.NewTitle)`.

Що не змінюється: `dbo.EnrollmentItems.CourseTitle` і `dbo.EnrollmentItems.UnitPrice` лишаються знімком на момент запису.

Перевірка:

```sql
SELECT Id, Title, Price FROM dbo.Courses WHERE Id = 101;
SELECT CourseId, CourseTitle, UnitPrice FROM dbo.EnrollmentItems WHERE CourseId = 101;
```

```javascript
db.reviews.find({ courseId: 101 }, { courseTitle: 1 })
```

### 11. Повтори з лічильником x-retry-count і максимумом спроб

Де: `Platform.EventBus/RabbitMqEventConsumer.cs`.

- `MaxRetries = 3`;
- `GetRetryCount` читає `x-retry-count` з `int`, `long`, `byte[]` або `string`;
- `RetryOrDeadLetterAsync` перепублікує повідомлення в той самий exchange/routing key з `x-retry-count = attempt + 1`, потім підтверджує оригінал;
- після трьох повторів `BasicNackAsync(..., requeue: false)`.

Перевірка: зупинити SQL або перейменувати `dbo.ProcessedEvents`, опублікувати подію, побачити три логи `Retry N/3` і повідомлення в DLQ з `x-retry-count` та `x-death`.

### 12. Передача контексту трасування через брокер

Де:

- `Platform.EventBus/RabbitMqTrace.cs`, `Inject`: `Propagators.DefaultTextMapPropagator.Inject` у заголовки `BasicProperties`.
- `RabbitMqTrace.StartConsumerActivity`: `Extract` заголовків, `ActivityKind.Consumer`, теги `messaging.system`, `messaging.destination.name`, `messaging.message.id`, `messaging.message.type`, `correlation.id`.
- `Platform.ServiceDefaults/Extensions.cs`, `ConfigureOpenTelemetry`: `.AddSource("Platform.Messaging")`.

Перевірка: `TraceId` у логах `Published` і `Applied`/`Acked` збігається. У Dashboard один trace містить HTTP-запит Catalog, publish span, consumer span Enrollments і SQL-запит.

### 13. Структуроване логування збоїв з бізнес-контекстом

Де: `Platform.EventBus/RabbitMqEventConsumer.cs`, `RetryOrDeadLetterAsync` і `OnReceivedAsync`.

Поля логів: `MessageId`, `EventId`, `EventType`, `Queue`, `CorrelationId`, повний `@x` зі стеком. Обробники додають `CourseId`, `OldPrice`, `NewPrice`, `OldTitle`, `NewTitle`, `MatchedCount`.

## Знімки для здачі

- `docs/lab-08/management-exchanges.png`: Management UI, exchanges `catalog-events`, `enrollments.catalog-sync.dlx`, `reviews.catalog-sync.dlx`.
- `docs/lab-08/management-queues.png`: черги `enrollments.catalog-sync`, `reviews.catalog-sync` з `DLX` і `Args`, обидві DLQ.
- `docs/lab-08/trace-publish-consume.png`: Aspire Dashboard, один trace HTTP → publish → consume → SQL/Mongo.
- `docs/lab-08/dlq-message.png`: повідомлення в DLQ з `x-death` і `x-retry-count = 3`.

Команди для повторного зняття: `docs/lab-08/manual-checks.md`.
