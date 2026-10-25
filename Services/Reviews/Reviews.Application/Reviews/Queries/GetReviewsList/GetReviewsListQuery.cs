using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;

namespace Reviews.Application.Reviews.Queries.GetReviewsList;

public sealed record GetReviewsListQuery(
    int? CourseId,
    string? Search,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ReviewDto>>;
