using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;

namespace Reviews.Application.Reviews.Queries.GetReviewById;

public sealed record GetReviewByIdQuery(string Id) : IQuery<ReviewDto>;
