using MediatR;

namespace Reviews.Application.Common.Messaging;

public interface ICommand : IRequest { }

public interface ICommand<TResponse> : IRequest<TResponse> { }

public interface IQuery<TResponse> : IRequest<TResponse> { }
