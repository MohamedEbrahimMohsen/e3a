using MediatR;

namespace $rootnamespace$.$queryNamespace$;

public sealed class PlaceHolderQueryHandler : IRequestHandler<PlaceHolderQuery, PlaceHolderResult>
{
    public Task<PlaceHolderResult> Handle(PlaceHolderQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
