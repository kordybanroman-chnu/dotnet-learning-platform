namespace Reviews.Domain.Exceptions;

public class DomainException(string message) : Exception(message);

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);
