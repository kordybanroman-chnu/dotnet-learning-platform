# Лаб. 05: ручна перевірка

## Запуск

```bash
dotnet user-secrets set "Parameters:sql-password" "Pass@word1" --project Platform.AppHost
dotnet run --project Platform.AppHost
```

Після старту SQL-контейнера застосуйте схему Enrollments один раз:

```bash
docker ps --filter name=sql- --format '{{.Names}}'
docker exec -i <sql-container> /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Pass@word1' -C -d EnrollmentsDb < db/p1/schema.sql
docker exec -i <sql-container> /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Pass@word1' -C -d EnrollmentsDb < db/p1/procedures.sql
docker exec -i <sql-container> /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Pass@word1' -C -d EnrollmentsDb < db/p1/seed.sql
```

Dashboard відкривається за посиланням з токеном, яке AppHost друкує в консолі. Усі 11 ресурсів мають бути в стані Running.

## Запити через Gateway

```bash
curl -i 'http://localhost:5000/api/courses?page=1'
curl -i 'http://localhost:5000/api/reviews?courseId=101'
curl -i 'http://localhost:5000/api/enrollments/1'
curl -i 'http://localhost:5000/api/aggregator/enrollments/1/full'
```

Кожна відповідь містить рівно один заголовок `X-Correlation-Id`. Якщо передати власний:

```bash
curl -i 'http://localhost:5000/api/enrollments/1' -H 'X-Correlation-Id: check-123'
```

той самий `check-123` буде в заголовку відповіді та в усіх логах запиту.

## Часткова відповідь Aggregator

1. У Dashboard зупиніть `reviews-api`.
2. Виконайте `curl 'http://localhost:5000/api/aggregator/enrollments/1/full'`.
3. Очікування: статус `200`, `enrollment` і `courses` заповнені, `reviews` порожній, `warnings` містить `reviews-api is unavailable`.
4. Запустіть `reviews-api` знову; повторний запит повертає відгуки без попереджень.

## Трасування і логи

1. Traces: відкрийте останній trace `gateway: GET /api/aggregator/{**catch-all}`. У дереві мають бути gateway, aggregator, enrollments-api, catalog-api, reviews-api, SQL- та MongoDB-spans.
2. Structured logs: додайте фільтр `CorrelationId == <id з відповіді>`. У списку будуть логи gateway, aggregator і трьох сервісів.
3. Відкрийте будь-який запис: у властивостях є `CorrelationId`, `ServiceName`, `EnvironmentName`, `MachineName`, `ThreadId`, `TraceId`, `SpanId`.

## Обхід Gateway

Прямий запит до внутрішньої адреси сервісу (з Dashboard) не має заголовка `X-Correlation-Id`, доки сервіс не згенерує його сам. Запит через Gateway завжди повертає заголовок і пише маршрут у лог:

```text
"GET" "/api/aggregator/enrollments/1/full" from "::1" -> route "aggregator": 200 in 34 ms
```

Знімки: `dashboard-resources.png`, `trace-aggregator.png`, `structured-logs-correlation.png`, `structured-log-details.png`, `structured-log-trace-context.png`.
