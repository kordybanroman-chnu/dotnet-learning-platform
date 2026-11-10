# Лаб. 07: карта критеріїв і доказів

Цей документ фіксує, де саме в репозиторії реалізований кожен критерій Лаб. 07 і чим його підтвердити. Шляхи вказані від кореня репозиторію. Символи й методи стабільніші за номери рядків.

## Швидка перевірка

```bash
dotnet build Platform.slnx --nologo
dotnet test Services/Reviews/Reviews.Domain.Tests --nologo
```

Очікування: 0 помилок, 0 попереджень, 5/5 тестів.

## Критерії

### 1. Standard Resilience Handler для всіх клієнтів без нашарування

Де:

- `Platform.ServiceDefaults/Extensions.cs`, `AddServiceDefaults`, блок `ConfigureHttpClientDefaults`:
  - `http.AddStandardResilienceHandler(...)`;
  - таймаути 30 с / 10 с;
  - `Retry.DisableForUnsafeHttpMethods()`.
- `Platform.Aggregator/Resilience/GrpcResilience.cs`, `Configure`:
  - `client.RemoveAllResilienceHandlers()`;
  - потім один `AddStandardResilienceHandler(...)`.
- `Platform.Aggregator/Program.cs`: для трьох бізнес-клієнтів викликається `GrpcResilience.ConfigureEnrollments`, `ConfigureCatalog`, `ConfigureReviews`.

Чому не подвійний handler: глобальний handler додається через `ConfigureHttpClientDefaults`, а для клієнтів Aggregator його спочатку знімають `RemoveAllResilienceHandlers`. Другий `AddStandardResilienceHandler` не нашаровується.

### 2. Retry-політика gRPC-клієнтів за gRPC-статусами

Де:

- `Platform.Aggregator/Resilience/GrpcResilience.cs`, `BuildServiceConfig`, `EnrollmentsChannel`, `CatalogChannel`, `ReviewsChannel`.
- `Platform.Aggregator/Program.cs`, `.ConfigureChannel(channel => channel.ServiceConfig = ...)`.

Що є:

- `ServiceConfig` з `MethodConfig` і `RetryPolicy`;
- `MaxAttempts` 3/4/2 для Enrollments/Catalog/Reviews;
- `InitialBackoff` 0.5 с, `MaxBackoff` 1–2 с, `BackoffMultiplier` 2;
- `RetryableStatusCodes`: `Unavailable`, для Catalog також `ResourceExhausted`.

### 3. Налаштовані retry, circuit breaker і таймаути

Де:

- `Platform.Aggregator/Resilience/GrpcResilience.cs`, записи `Enrollments`, `Catalog`, `Reviews`.
- `Platform.Aggregator/Resilience/ResilienceLogger.cs`.
- `Platform.ServiceDefaults/Extensions.cs`, `Retry.DisableForUnsafeHttpMethods`.
- `Platform.Aggregator/Services/EnrollmentAggregator.cs`, константи `EnrollmentsTimeout`, `CatalogTimeout`, `ReviewsTimeout`, метод `CreateCallOptions`.

Що є:

- exponential backoff з jitter;
- `FailureRatio`, `MinimumThroughput`, `SamplingDuration`, `BreakDuration`;
- `OnOpened` = Error, `OnClosed` = Information, `OnHalfOpened` = Warning;
- таймаути узгоджені: `AttemptTimeout × (MaxRetryAttempts + 1) + затримки` не перевищує `TotalTimeout`, а gRPC-дедлайн не менший за бюджет повторів.

Поточні значення:

| Клієнт | HTTP retry | Attempt / Total | gRPC deadline | gRPC retry |
| --- | --- | --- | --- | --- |
| Enrollments | 2 | 5 с / 20 с | 17 с | 3 спроби |
| Catalog | 2 | 6 с / 25 с | 20 с | 4 спроби |
| Reviews | 1 | 2 с / 6 с | 5 с | 2 спроби |

### 4. Різні політики для критичних і некритичних сервісів з обґрунтуванням

Де:

- `Platform.Aggregator/Resilience/GrpcResilience.cs`, три різні `ResilienceProfile`.
- `README.md`, розділ «Резильєнтність, метрики та health checks (Лаб. 07)», таблиця політик.

Обґрунтування в README:

- Catalog і Enrollments критичні: без них картка запису неповна, тому більше спроб і чутливіший breaker;
- Reviews некритичний: Aggregator повертає часткову відповідь, тому менше спроб, коротші таймаути, вищий поріг breaker.

### 5. Graceful degradation кешу Redis

Де:

- `Services/Catalog/Catalog.Bll/Caching/TwoLevelCache.cs`, `GetOrCreateAsync` і `InvalidateAsync`: `try/catch` навколо `IDistributedCache`, лог Warning, далі читання з фабрики (БД).
- `Platform.Aggregator/Services/EnrollmentAggregator.cs`, `TryGetAsync` і `TrySetAsync`: збій Redis не провалює запит.

Перевірка: зупинити `redis`, виконати `GET /api/courses/102`, отримати 200 з SQL. `/health/ready` Catalog показує `Degraded`, не 503.

### 6. Клас бізнес-метрик з лічильниками через IMeterFactory

Де:

- `Services/Enrollments/Enrollments.Api/Metrics/EnrollmentMetrics.cs`, `EnrollmentMetrics`, `MeterName = "Enrollments.Api"`.
- `Services/Catalog/Catalog.Api/Metrics/CatalogMetrics.cs`, `CatalogMetrics`, `MeterName = "Catalog.Api"`.
- `Services/Reviews/Reviews.WebApi/Metrics/ReviewMetrics.cs`, `ReviewMetrics`, `MeterName = "Reviews.WebApi"`.
- `Services/*/Program.cs`: `AddSingleton<...Metrics>()`.
- `Platform.ServiceDefaults/Extensions.cs`, `ConfigureOpenTelemetry`:
  `.AddMeter("Catalog.Cache", "Enrollments.Api", "Catalog.Api", "Reviews.WebApi", "Polly")`.

Лічильники:

- `enrollments.created` (`status`), `enrollments.confirmed` (`status`), `enrollments.cancelled` (`status`);
- `catalog.courses.created` (`has.detail`, `category.bucket`), `catalog.courses.price_updated` (`price.band`), `catalog.courses.deleted` (`source`);
- `reviews.created` (`rating`), `reviews.updated` (`rating`), `reviews.deleted` (`source`).

### 7. Гістограми тривалості та gauge-метрики з низькою кардинальністю

Де:

- `Metrics`-класи трьох сервісів, методи `RecordDuration` і `CreateObservableGauge`.
- Контролери: `EnrollmentsController`, `CoursesController`, `ReviewsController`. Гістограма пишеться в `finally`, тому фіксується і `success`, і `failure`.
- Фонові poller-и: `EnrollmentMetricsPoller`, `CatalogMetricsPoller`, `ReviewMetricsPoller`, період 15 с.

Gauge:

- `enrollments.awaiting_validation`;
- `catalog.courses.total`;
- `reviews.total`.

Тегів з `studentId`, `courseId`, `reviewId` немає. Це перевіряється пошуком по `Metrics/`.

### 8. Health checks SQL Server і MongoDB з іменами, тегами й таймаутом

Де:

- `Services/Enrollments/Enrollments.Api/Program.cs`: `AddSqlServer`, name `enrollments-db`, tags `database`, `sql`, `ready`, `startup`, timeout 5 с.
- `Services/Catalog/Catalog.Api/Program.cs`: `AddSqlServer`, name `catalog-db`, ті самі теги, timeout 5 с.
- `Services/Reviews/Reviews.WebApi/Program.cs`: `AddMongoDb`, name `reviews-db`, tags `database`, `mongodb`, `ready`, `startup`, timeout 5 с.

`AddSqlServer` виконує `SELECT 1`, `AddMongoDb` виконує `ping` через зареєстрований `IMongoClient`.

### 9. Health check Redis зі статусом Degraded

Де:

- `Services/Catalog/Catalog.Api/Program.cs`: `AddRedis`, name `redis-cache`, `failureStatus: HealthStatus.Degraded`, tags `cache`, `redis`, `ready`, timeout 3 с.
- `Platform.Aggregator/Program.cs`: та сама перевірка.
- `AddRedisDistributedCache("redis", settings => settings.DisableHealthChecks = true)` вимикає дубль автоматичної перевірки Aspire.

Перевірка: зупинити `redis`, `/health/ready` Catalog = 200 зі статусом `Degraded`, `redis-cache` = `Degraded`.

### 10. gRPC Health Checking Protocol і перевірка залежностей в Aggregator

Де:

- `Services/Enrollments/Enrollments.Api/Program.cs`, `Services/Catalog/Catalog.Api/Program.cs`, `Services/Reviews/Reviews.WebApi/Program.cs`:
  - `AddGrpcHealthChecks(options => options.Services.Map("", registration => registration.Tags.Contains("ready") && !registration.Tags.Contains("cache")))`;
  - `MapGrpcHealthChecksService()`.
- `Platform.Aggregator/HealthChecks/DownstreamGrpcHealthCheck.cs`:
  - `GrpcClientFactory.CreateClient<Health.HealthClient>(ClientName)`;
  - `CheckAsync` з дедлайном 2 с;
  - `RpcException` → `Unhealthy`.
- `Platform.Aggregator/Program.cs`: три health-клієнти `enrollments-health`, `catalog-health`, `reviews-health`; для них `RemoveAllResilienceHandlers`, щоб перевірка падала швидко.
- `reviews-api` має `failureStatus: HealthStatus.Degraded`, бо Reviews некритичний.

Чому Redis виключений з gRPC health: предикат `!registration.Tags.Contains("cache")`. Тому Degraded Redis не робить Catalog `NotServing`.

### 11. Розділені liveness і readiness endpoints з деталями

Де:

- `Platform.ServiceDefaults/Extensions.cs`, `MapDefaultEndpoints` і `WriteHealthResponse`.
- `Platform.ServiceDefaults/Extensions.cs`, `AddDefaultHealthChecks`: `self` з тегом `live`.

Endpoints:

- `/health/live`: тільки `live`, без зовнішніх залежностей;
- `/health/ready`: тільки `ready`, JSON з `status`, `totalDurationMs`, `checks[]`;
- `/health/startup`: тільки `startup`;
- `/health`: усі перевірки;
- `/alive`: аліас liveness.

Усі мапляться в усіх середовищах, не лише Development. У Development `checks[].exception` містить текст винятку; поза Development поле `exception` = `null`.

Перевірено окремим запуском Catalog.Api з `ASPNETCORE_ENVIRONMENT=Production` і недоступним Redis: `/health/ready` повернув `Degraded`, а `exception` для `redis-cache` був `null`.

### 12. AppHost показує стан ресурсів через WithHttpHealthCheck

Де: `Platform.AppHost/Program.cs`.

- `WithHttpHealthCheck("/health", endpointName: "http")` для enrollments, catalog, reviews, aggregator, gateway.
- `WaitFor` залишений лише для критичних залежностей: БД, Enrollments, Catalog.
- Redis і Reviews підключені через `WithReference` без `WaitFor`, тому їх недоступність на старті не блокує сервіси.
- Gateway не має `WaitFor`, тому стартує незалежно від бекендів.

## Runtime-докази

Порти динамічні. `<catalog-url>`, `<reviews-url>`, `<enrollments-url>`, `<aggregator-url>` беруться з Dashboard.

```bash
curl -i '<catalog-url>/health/live'
curl -i '<catalog-url>/health/ready'
curl -i '<catalog-url>/health/startup'

curl -s 'http://localhost:5000/api/aggregator/enrollments/1/full'
```

Очікування:

- `/health/live` = 200;
- `/health/ready` = 200, JSON з `catalog-db` і `redis-cache`;
- `GET /health/ready` при зупиненому Redis = 200, `status: Degraded`;
- `GET /health/ready` при зупиненому SQL = 503;
- Aggregator при зупиненому Reviews = 200 з `warnings`, у логах `OnRetry`;
- після серії збоїв Aggregator відповідає за 10–20 мс, у логах `Circuit opened`, `Circuit half-opened`, `Circuit closed`.

Знімки:

- `docs/lab-07/onretry-metric.png`;
- `docs/lab-07/circuit-opened.png`;
- `docs/lab-07/circuit-opened-metric.png`;
- `docs/lab-07/business-metrics.png`;
- `docs/lab-07/health-ready-degraded.png`;
- `docs/lab-07/resources-health.png`;
- `docs/lab-07/resources-health-details.png`.

Повні команди: `docs/lab-07/manual-checks.md`.

## Рішення, які не є дефектами

1. Health-клієнти Aggregator не мають resilience. Це навмисно: перевірка має завершуватися за 2 с, а не повторюватися.
2. gRPC health не включає Redis. Кеш необов'язковий, тому Degraded Redis не повинен робити сервіс `NotServing`.
3. Redis health check має `Degraded`, а не `Unhealthy`. Це вимога graceful degradation.
4. `reviews-api` в Aggregator має `Degraded`, бо Reviews некритичний. Catalog і Enrollments залишаються `Unhealthy`.
5. Health endpoints відкриті поза Development. Це вимога лабораторної; текст винятків поза Development прихований.
6. `WaitFor` не стоїть на Redis, Reviews і Gateway. Це свідоме рішення для resilience: необов'язкові залежності не блокують старт.
7. `MinimumThroughput` 10/5 і `SamplingDuration` 30 с підібрані під малий трафік навчального стенду.
8. Gauge оновлюється раз на 15 с. Це компроміс між навантаженням на БД і актуальністю; для лабораторного стенду достатньо.

## Додаткові зміни в межах ЛР7

- `Platform.ApiGateway/Program.cs`: доступ до `IReverseProxyFeature` через `context.Features.Get<IReverseProxyFeature>()`. Попередній `GetReverseProxyFeature()` кидав виняток на `/health`, бо health-запит не проходить через YARP.
- `Services/Reviews/Reviews.Infrastructure/HealthChecks/MongoHealthCheck.cs` видалений, замість нього в `Reviews.WebApi/Program.cs` зареєстрований `AddMongoDb`. Функціональність перевірки MongoDB збережена, з'явилися ім'я, теги й таймаут.

## Обмеження

- Окремих unit-тестів для resilience, метрик і health checks немає. Вони перевіряються живим прогоном за `manual-checks.md`.
- Скріншот Resources у Dashboard 9.5 показує `Running`, а не `Healthy`; статус health підтверджується `/health` і карткою ресурсу.
