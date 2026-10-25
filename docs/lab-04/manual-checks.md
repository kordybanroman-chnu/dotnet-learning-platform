# Лаб. 04: ручна перевірка та скріншоти

Усі сім знімків лежать у `docs/lab-04/`: `swagger.png`, `pagination.png`, `validation-400.png`, `mongodb-document-indexes.png`, `compass-document.png`, `logs-validation.png`, `conflict-409.png`. Нижче команди, якими їх отримано, якщо треба повторити перевірку на іншій машині.

## Запуск

MongoDB і API вже запущені на `localhost:27017` і `http://localhost:5084`. Якщо їх треба підняти заново:

```bash
docker start feedback-mongo
dotnet run --project Services/Reviews/Reviews.WebApi
```

Swagger: `http://localhost:5084/swagger`. Health check: `http://localhost:5084/health`, очікується `Healthy`.

## 1. Документ reviews у Compass

1. У Compass створіть підключення `mongodb://localhost:27017`.
2. Відкрийте базу `FeedbackDb`, колекцію `reviews`.
3. Відкрийте документ з `courseId: 101` і `author.displayName: "Олена"`.
4. Перевірте поля: `courseId` типу `Int32`, `courseTitle`, вкладений `author {userId, displayName}`, `rating: 5`, масив `photos: ["csharp-cover.png"]`, масив `comments` з полями `author`, `text`, `createdAt` (плюс службові `_id`, `version`, `updatedAt`), `discussionId` на верхньому рівні.
5. Знімок збережіть як `docs/lab-04/compass-document.png`.

## 2. Індекси

```bash
docker exec feedback-mongo mongosh FeedbackDb --eval "db.reviews.getIndexes()"
```

У виводі мають бути `ix_reviews_course_created`, `ix_reviews_text` (text) і `ux_reviews_course_author` (unique). Готовий знімок виводу вже є, повторювати не обов'язково.

## 3. Валідація до виклику обробника

У першому терміналі залиште лог API відкритим. У другому виконайте запит з `rating: 10`:

```bash
curl -i -X POST 'http://localhost:5084/api/reviews' \
  -H 'Content-Type: application/json' \
  -d '{"courseId":999,"courseTitle":"Тест","authorId":"t-1","authorName":"Тест","rating":10,"text":"Перевірка валідації"}'
```

Очікується `400` і `errors.Rating`. У логу API буде `Handling CreateReviewCommand` і `Request /api/reviews failed with status 400`; рядка `Handled CreateReviewCommand` бути не повинно. Це доказ, що обробник не викликався. Знімок логу збережіть як `docs/lab-04/logs-validation.png`.

## 4. Конфлікт версій (409)

Отримайте `ETag` будь-якого відгуку:

```bash
curl -i 'http://localhost:5084/api/reviews?pageSize=1'
curl -i 'http://localhost:5084/api/reviews/<id>'
```

Скопіюйте `ETag` (наприклад `"0"`) і виконайте оновлення:

```bash
curl -i -X PUT 'http://localhost:5084/api/reviews/<id>' \
  -H 'Content-Type: application/json' \
  -H 'If-Match: "0"' \
  -d '{"rating":4,"text":"Оновлення з правильним ETag"}'
```

Повторіть той самий запит із тим самим `If-Match: "0"`. Другий запит поверне `409 Conflict` з текстом `was modified by another request`. Знімок збережіть як `docs/lab-04/conflict-409.png`.

## 5. Повторний відгук

Надішліть двічі однаковий POST з `authorId`, який уже залишив відгук на цей курс:

```bash
curl -i -X POST 'http://localhost:5084/api/reviews' \
  -H 'Content-Type: application/json' \
  -d '{"courseId":101,"courseTitle":"Основи C#","authorId":"1","authorName":"Олена","rating":5,"text":"Другий відгук"}'
```

Другий запит поверне `400` з `You have already reviewed this course`. Це асинхронне правило FluentValidation з `ExistsAsync`, а не `500`.

## 6. Ідемпотентність сідингу

```bash
docker exec feedback-mongo mongosh FeedbackDb --eval "db.reviews.countDocuments()"
```

Зупиніть API (`Ctrl+C`) і запустіть знову. Повторіть команду, кількість має залишитися `3`.

## Після здачі

```bash
docker stop feedback-mongo
```

Гілка `lab-04` запушена в `origin`. У `main` вона не змерджена.
