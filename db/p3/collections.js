db.createCollection('reviews', {
  validator: { $jsonSchema: {
    bsonType: 'object',
    required: ['courseId', 'rating', 'author'],
    properties: {
      courseId:    { bsonType: 'int' },
      courseTitle: { bsonType: 'string' },
      rating:      { bsonType: 'int', minimum: 1, maximum: 5 },
      author: { bsonType: 'object', required: ['userId', 'displayName'],
        properties: {
          userId:      { bsonType: 'string' },
          displayName: { bsonType: 'string' }
        } },
      discussionId: { bsonType: 'objectId' }
    }
  } },
  validationLevel: 'moderate'
});

db.createCollection('discussions', {
  validator: { $jsonSchema: {
    bsonType: 'object',
    required: ['courseId'],
    properties: { courseId: { bsonType: 'int' } }
  } }
});

db.createCollection('ratings', {
  validator: { $jsonSchema: {
    bsonType: 'object',
    required: ['courseId', 'avgRating', 'count'],
    properties: {
      courseId:  { bsonType: 'int' },
      avgRating: { bsonType: 'double', minimum: 1, maximum: 5 },
      count:     { bsonType: 'int', minimum: 0 }
    }
  } }
});

db.reviews.createIndex({ courseId: 1 });
db.reviews.createIndex({ rating: -1 });
db.discussions.createIndex({ courseId: 1 });
db.ratings.createIndex({ courseId: 1 }, { unique: true });
