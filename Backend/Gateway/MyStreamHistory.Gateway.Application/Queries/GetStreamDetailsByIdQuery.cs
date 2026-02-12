using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetStreamDetailsByIdQuery : IRequest<StreamDetailsDto>
{
    public Guid StreamSessionId { get; set; }
}

