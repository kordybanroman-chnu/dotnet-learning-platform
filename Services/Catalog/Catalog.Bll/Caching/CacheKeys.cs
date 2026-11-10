namespace Catalog.Bll.Caching;

public static class CacheKeys
{
    public const string Categories = "catalog:categories:all";

    public static string Course(long id) => $"catalog:course:{id}";

    public static string InstructorCourses(long instructorId) => $"catalog:instructor:{instructorId}:courses";
}
