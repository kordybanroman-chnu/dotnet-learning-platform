if (db.discussions.countDocuments() === 0) {
  db.discussions.insertMany([
    { courseId: 101, title: 'Питання до модуля 1',
      messages: [{ author: 'Андрій', text: 'Коли дедлайн?', createdAt: new Date('2026-09-01') }] },
    { courseId: 102, title: 'Обговорення проєкту', messages: [] }
  ]);
}

if (db.reviews.countDocuments() === 0) {
  const d1 = db.discussions.findOne({ courseId: 101 });
  db.reviews.insertMany([
    {
      courseId: 101, courseTitle: 'Основи C#',
      author: { userId: '1', displayName: 'Олена' },
      rating: 5, text: 'Чудовий старт',
      photos: ['img1.jpg', 'img2.jpg'],
      comments: [{ author: 'Андрій', text: 'Згоден', createdAt: new Date('2026-09-01') }],
      discussionId: d1._id
    },
    {
      courseId: 103, courseTitle: 'MongoDB для .NET',
      author: { userId: '3', displayName: "O'Brien" },
      rating: 4, text: 'Корисно', comments: []
    }
  ]);
}

db.reviews.aggregate([
  { $group: { _id: '$courseId', avgRating: { $avg: '$rating' }, count: { $sum: 1 } } },
  { $project: { _id: 0, courseId: '$_id', avgRating: 1, count: 1 } },
  { $merge: { into: 'ratings', on: 'courseId',
    whenMatched: 'replace',
    whenNotMatched: 'insert' } }
]);
