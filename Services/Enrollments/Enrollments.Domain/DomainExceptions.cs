namespace Enrollments.Domain;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class BusinessConflictException(string message) : Exception(message);

public sealed class ValidationException(string message) : Exception(message);
