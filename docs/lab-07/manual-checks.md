# Лаб. 07: ручна перевірка

## Запуск

```bash
dotnet user-secrets set "Parameters:sql-password" "Pass@word1" --project Platform.AppHost
dotnet run --project Platform.AppHost
```

У Dashboard має бути 12 ресурсів: `sql`, `mongo`, `redis`, `enrollments-api`, `catalog-api`, `reviews-api`, `aggregator`, `gateway` і `aspire-dashboard`. Кожен проєкт має статус Healthy, бо в AppHost доданий `WithHttpHealthCheck("/health")`.

Порти мікросервісів динамічні. Далі `<catalog-url>`, `<reviews-url>`, `<enrollments-url>` означають HTTP-адресу відповідного ресурсу з вкладки Resources.

## Health endpoints

```bash
curl -i '<catalog-url>/health/live'
curl -i '<catalog-url>/health/ready'
curl -i '<catalog-url>/health/startup'
```

Очікування: `/health/live` повертає 200 і не звертається до SQL чи Redis. `/health/ready` повертає 200 і JSON:

```json
{
  "status": "Healthy",
  "totalDurationMs": 12.34,
  "checks": [
    { "name": "self", "status": "Healthy" },
    { "name": "catalog-db", "status": "Healthy" },
    { "name": "redis-cache", "status": "Healthy" }
  ]
}
```

У Reviews аналогічно перевіряється `reviews-db`, в Enrollments - `enrollments-db`.

## Redis: Degraded і робота без кешу

1. У Dashboard зупиніть `redis`.
2. Виконайте:

```bash
curl -i '<catalog-url>/health/ready'
curl -s '<catalog-url>/api/courses/102'
```

3. Очікування: `/health/ready` повертає 200, `status` = `Degraded`, `redis-cache` = `Degraded`; курс повертається з SQL, у логах `Redis read failed for "catalog:course:102"`.
4. Запустіть `redis` знову.

Якщо Redis недоступний, сервіс не падає і не повертає 503 на читання. Це graceful degradation з Лаб. 06, тепер вона видима в health check. Агрегатор у цей час показує `Degraded`, але його перевірка `catalog-api` залишається `Healthy`: gRPC health не включає Redis.

## SQL: liveness і readiness

1. Зупиніть `sql` у Dashboard.
2. Перевірте Enrollments і Catalog:

```bash
curl -i '<enrollments-url>/health/live'
curl -i '<enrollments-url>/health/ready'
curl -i '<catalog-url>/health/live'
curl -i '<catalog-url>/health/ready'
```

3. Очікування: `/health/live` = 200, `/health/ready` = 503, у JSON `enrollments-db` або `catalog-db` = `Unhealthy`.
4. Запустіть `sql` знову; readiness повертається до 200.

## Повтори при зупиненому reviews-api

1. Зупиніть `reviews-api` у Dashboard.
2. Дочекайтеся, поки спливе 30-секундний кеш Aggregator, або перезапустіть `aggregator`.
3. Виконайте:

```bash
curl -s 'http://localhost:5000/api/aggregator/enrollments/1/full'
```

4. Очікування: 200, `enrollment` і `courses` заповнені, `reviews` порожній, `warnings` містить `reviews-api is unavailable`. У Structured logs є `OnRetry` з категорії `Polly` і gRPC-повтори; у Metrics - `resilience.polly.strategy.events` з `event.name = OnRetry`.
5. Запустіть `reviews-api`; наступний запит повертає відгуки без `warnings`.

## Circuit breaker

1. Зупиніть `reviews-api`.
2. Виконайте 10–15 запитів до Aggregator у межах 30 с:

```bash
for i in $(seq 1 15); do
  curl -s -o /dev/null -w '%{http_code} %{time_total}\n' \
    'http://localhost:5000/api/aggregator/enrollments/1/full'
done
```

3. Очікування: спочатку запити повільні через повтори, після `MinimumThroughput` breaker розмикається. У логах `Aggregator.Resilience` є `Circuit opened for 00:00:05`, у Metrics - `event.name = OnCircuitOpened`, наступні запити відхиляються миттєво.
4. Запустіть `reviews-api`; після `BreakDuration` (5 с) з'являється `OnHalfOpened`, потім `OnCircuitClosed`, відповіді знову містять відгуки.

## Бізнес-метрики

Створіть запис, підтвердіть його, змініть ціну курсу і створіть відгук:

```bash
curl -s -X POST 'http://localhost:5000/api/enrollments' \
  -H 'Content-Type: application/json' \
  -d '{"studentId":1,"items":[{"courseId":101,"units":1}]}'

curl -s -X POST 'http://localhost:5000/api/enrollments/1/confirm'

curl -s -X PUT 'http://localhost:5000/api/courses/101/price' \
  -H 'Content-Type: application/json' -d '{"price":990}'

curl -s -X POST 'http://localhost:5000/api/reviews' \
  -H 'Content-Type: application/json' \
  -d '{"courseId":102,"courseTitle":"ASP.NET Core API","authorId":"7","authorName":"Ірина","rating":5,"text":"Курс для перевірки метрик."}'
```

У Dashboard → Metrics:

- `enrollments.created` і `enrollments.confirmed` зросли, тег `status` = `Submitted` і `SeatsConfirmed`;
- `catalog.courses.price_updated` має тег `price.band`;
- `reviews.created` має тег `rating`;
- гістограми `*.operation.duration` показують розподіл, у тому числі невдалі операції;
- gauge `enrollments.awaiting_validation`, `catalog.courses.total`, `reviews.total` показують поточні значення після 15 с.

Поверніть ціну курсу:

```bash
curl -s -X PUT 'http://localhost:5000/api/courses/101/price' \
  -H 'Content-Type: application/json' -d '{"price":1000}'
```

## Знімки для здачі

- `onretry-metric.png`: Metrics, `resilience.polly.strategy.events`, фільтр `event.name = OnRetry`.
- `circuit-opened.png`: Structured logs з `Circuit opened` і метрика `OnCircuitOpened`.
- `business-metrics.png`: `enrollments.created` або `reviews.created` з тегами.
- `health-ready-degraded.png`: `/health/ready` Catalog зі статусом `Degraded` при зупиненому Redis.
- `resources-health.png`: Resources зі статусами всіх ресурсів.

## Очікувані логи

- `Aggregator.Resilience`: `Circuit opened`, `Circuit half-opened`, `Circuit closed`.
- `Polly`: `OnRetry`, `OnCircuitOpened`, `OnCircuitClosed`.
- `Catalog.Bll.Caching.TwoLevelCache`: `Redis read failed`, `Redis write failed`.
- `Microsoft.Extensions.Diagnostics.HealthChecks`: статуси `catalog-db`, `redis-cache`, `reviews-db`.
