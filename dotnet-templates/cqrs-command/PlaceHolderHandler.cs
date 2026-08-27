using MediatR;

namespace $rootnamespace$.$commandNamespace$;

public sealed class PlaceHolderHandler : IRequestHandler<PlaceHolderCommand, PlaceHolderResult>
{
    public Task<PlaceHolderResult> Handle(PlaceHolderCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
