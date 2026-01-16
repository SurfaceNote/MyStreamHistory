using MediatR;
using MyStreamHistory.Shared.Base.Contracts.Users;

namespace MyStreamHistory.Gateway.Application.Queries;

public record GetNewUsersQuery :  IRequest<List<UserDto>>;