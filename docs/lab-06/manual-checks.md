# Лаб. 06: ручна перевірка

## Запуск

```bash
dotnet user-secrets set "Parameters:sql-password" "Pass@word1" --project Platform.AppHost
dotnet run --project Platform.AppHost
```

У Dashboard має бути 12 ресурсів: `sql`, `mongo`, `redis`, три мікросервіси, `aggregator`, `gateway` і `aspire-dashboard`. Ресурс `redis` має статус Running, його дані лежать у volume `platform.apphost-*-redis-data`.

`Catalog.Api` і `Aggregator` отримують `ConnectionStrings__redis` від AppHost. Для запуску без AppHost використовується `localhost:6379` з `appsettings.json`.

gRPC-сервери слухають HTTPS-endpoint з launch profile (`https://localhost:7255`, `https://localhost:7245`, `https://localhost:7084`). Unit-тести та збірка не потребують сертифіката; для прямого gRPC-виклику поза Aggregator виконайте `dotnet dev-certs https --trust` або передайте `-k` у `grpcurl`.

## Кеш каталогу

Двічі поспіль запросіть один курс через Gateway:

```bash
curl -s 'http://localhost:5000/api/courses/101'
curl -s 'http://localhost:5000/api/courses/101'
```

У Traces другого запиту немає SQL-span: перший запит виконує `SELECT`, другий бере дані з L1. У Structured logs другого запиту є `Cache hit "catalog:course:101" at "L1"`.

Змініть ціну і перевірте інвалідацію:

```bash
curl -s -X PUT 'http://localhost:5000/api/courses/101/price' \
  -H 'Content-Type: application/json' -d '{"price":990}'
curl -s 'http://localhost:5000/api/courses/101'
```

Очікування: у логах `Cache invalidated "catalog:course:101", reason "CoursePriceUpdated"`, наступний GET повертає `990`. Поверніть ціну:

```bash
curl -s -X PUT 'http://localhost:5000/api/courses/101/price' \
  -H 'Content-Type: application/json' -d '{"price":1000}'
```

Список категорій кешується окремо (`catalog:categories:all`, L1 5 хв, L2 30 хв):

```bash
curl -s 'http://localhost:5000/api/categories'
curl -s 'http://localhost:5000/api/categories'
```

## Redis: L2 і деградація

1. У Dashboard зупиніть `redis`.
2. Виконайте `curl -s 'http://localhost:5000/api/courses/102'`.
3. Очікування: статус `200`, дані з SQL, у логах `Redis read failed for "catalog:course:102"` зі стеком, сервіс не падає.
4. Запустіть `redis` знову; наступний запит заповнює L2.

Після перезапуску Catalog.Api перший GET курсу читає L2 (Redis) і пише L1. У логах: `Cache hit "catalog:course:101" at "L2"`.

## gRPC і Aggregator

```bash
curl -s 'http://localhost:5000/api/aggregator/enrollments/1/full'
```

У Traces відкрийте trace запиту: gateway → aggregator → gRPC client span `catalog.CatalogService/GetCoursesByIds` → gRPC server span `Catalog.Api`, аналогічно для `enrollments` і `reviews`. У Structured logs є записи `gRPC "/catalog.CatalogService/GetCoursesByIds" from ... finished with OK in ... ms` з тим самим `CorrelationId`, що й у заголовку відповіді.

Повторіть запит протягом 30 секунд:

```bash
curl -s 'http://localhost:5000/api/aggregator/enrollments/1/full'
```

Другий запит не робить gRPC-викликів: у логах `Cache hit "aggregator:enrollment:1:full"`. Кешуються лише відповіді без `warnings`.

Часткова відмова:

1. Зупиніть `catalog-api` у Dashboard.
2. `curl -s 'http://localhost:5000/api/aggregator/enrollments/1/full'`.
3. Очікування: `200`, `enrollment` і `reviews` заповнені, `courses` порожній, `warnings` містить `catalog-api is unavailable`.
4. Запустіть `catalog-api`; наступний запит повертає курси без попереджень.

## Метрики

У Dashboard → Metrics -> `cache.hits` і `cache.misses`. Фільтр за `cache.level` показує окремо L1 і L2. `cache.invalidations` зростає після PUT/DELETE. Meter `Catalog.Cache` доданий у `ServiceDefaults`.

## Порівняння HTTP і gRPC

До переходу на gRPC агрегатор робив один HTTP-запит на кожен курс і на кожен курс відгуків. У Лаб. 06 ті самі дані приходять двома gRPC-викликами (`GetCoursesByIds`, `GetForCourses`) паралельно.

| Запит | Транспорт | Тривалість у Traces |
| --- | --- | --- |
| `GET /api/aggregator/enrollments/1/full` | HTTP, Лаб. 05 | 350 ms (`docs/lab-05/trace-aggregator.png`) |
| `GET /api/aggregator/enrollments/1/full` | gRPC, холодний кеш | 58.44 ms (`docs/lab-06/trace-grpc-aggregator.png`) |
| `GET /api/aggregator/enrollments/1/full` | gRPC, кеш агрегатора | 21.88 ms |

Вимірювання: відкрийте однаковий запит у Traces, порівняйте `Duration` кореневого span Aggregator. Кеш агрегатора на момент вимірювання має бути холодним (зачекайте 30 с або перезапустіть Aggregator). HTTP-вимір узятий зі знімка Лаб. 05, gRPC-виміри зняті на цій машині 7 листопада 2026.

## Знімки

- `docs/lab-06/trace-cache-hit.png`: другий GET курсу без SQL-span.
- `docs/lab-06/trace-grpc-aggregator.png`: trace з gRPC client/server spans.
- `docs/lab-06/metrics-cache-hits.png`: `cache.hits` з тегами `cache.level` L1/L2.
- `docs/lab-06/structured-logs-cache.png`: hit/miss та інвалідація з ключами.
- `docs/lab-06/dashboard-resources.png`: Redis, SQL, Mongo і сервіси в Dashboard.
