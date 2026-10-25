using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;

namespace Reviews.Application.Reviews.Queries.GetReviewsList;

public sealed class GetReviewsListQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetReviewsListQuery, PagedResult<ReviewDto>>
{
    public Task<PagedResult<ReviewDto>> Handle(GetReviewsListQuery query, CancellationToken ct)
        => queries.GetListAsync(
            new ReviewFilter(
                query.CourseId,
                query.Search,
                Math.Max(query.Page, 1),
                Math.Clamp(query.PageSize, 1, 100)),
            ct);
}
