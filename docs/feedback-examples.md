# Feedback (MongoDB): приклади документів і запитів

Колекції: `reviews`, `discussions`, `ratings` (див. `db/p3/collections.js`).
Прийоми: embed (`author`, `comments[]`), reference (`courseId`, `discussionId`),
гібрид (обидва в одному `reviews`), гнучка схема (`photos` лише в частини документів),
денормалізація (`courseTitle` з політики дублювання).

## Приклад review (повний)

```json
{
  "courseId": 101,
  "courseTitle": "Основи C#",
  "author": { "userId": "1", "displayName": "Олена" },
  "rating": 5,
  "text": "Чудовий старт",
  "photos": ["img1.jpg"],
  "comments": [{ "author": "Андрій", "text": "Згоден" }],
  "discussionId": { "$oid": "66f0c3aabbccddeeff001122" }
}
```

## Приклад review (мінімальний, інший набір полів)

```json
{
  "courseId": 103,
  "courseTitle": "MongoDB для .NET",
  "author": { "userId": "3", "displayName": "O'Brien" },
  "rating": 4,
  "text": "Корисно",
  "comments": []
}
```

## Запити

```js
db.reviews.find({ courseId: 101 }).sort({ _id: -1 }).limit(20);

db.reviews.aggregate([
  { $match: { courseId: 101 } },
  { $group: { _id: '$courseId', avgRating: { $avg: '$rating' }, count: { $sum: 1 } } }
]);

db.ratings.find().sort({ avgRating: -1 }).limit(10);
```
