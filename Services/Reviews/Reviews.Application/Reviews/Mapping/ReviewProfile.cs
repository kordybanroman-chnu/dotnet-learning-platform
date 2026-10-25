using AutoMapper;
using Reviews.Application.Common.Models;
using Reviews.Domain.Entities;

namespace Reviews.Application.Reviews.Mapping;

public sealed class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        CreateMap<Comment, CommentDto>()
            .ForCtorParam(nameof(CommentDto.Id), o => o.MapFrom(s => s.Id.ToString()));

        CreateMap<Review, ReviewDto>()
            .ForCtorParam(nameof(ReviewDto.Id), o => o.MapFrom(s => s.Id.ToString()))
            .ForCtorParam(nameof(ReviewDto.AuthorId), o => o.MapFrom(s => s.Author.UserId))
            .ForCtorParam(nameof(ReviewDto.AuthorName), o => o.MapFrom(s => s.Author.DisplayName))
            .ForCtorParam(nameof(ReviewDto.Rating), o => o.MapFrom(s => s.Rating.Value))
            .ForCtorParam(nameof(ReviewDto.CommentsCount), o => o.MapFrom(s => s.Comments.Count))
            .ForCtorParam(nameof(ReviewDto.Comments), o => o.MapFrom(s => s.Comments))
            .ForCtorParam(nameof(ReviewDto.Photos), o => o.MapFrom(s => s.Photos.ToList()))
            .ForCtorParam(nameof(ReviewDto.DiscussionId),
                o => o.MapFrom(s => s.DiscussionId == null ? null : s.DiscussionId.ToString()));
    }
}
