using AppTemplate.Domain.Samples;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using MediatR;

namespace AppTemplate.Application.Samples.AddSample;

public sealed class AddSampleHandler(ISampleRepository sampleRepository, ICurrentUserService currentUserService) : IRequestHandler<AddSampleCommand, AddSampleResult>
{
    public async Task<AddSampleResult> Handle(AddSampleCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(Exceptions.ErrorCodes.UserNotAuthenticated);
        }

        var existing = await sampleRepository.GetByCodeAsync(request.Code, cancellationToken).ConfigureAwait(false);

        if (existing != null)
        {
            throw new ConflictCoreException(SampleApplicationErrorCodes.SampleCodeAlreadyUsed);
        }

        var sample = Sample.Create(request.Name, request.Code, currentUserService.UserId);

        await sampleRepository.AddAsync(sample, cancellationToken).ConfigureAwait(false);
        await sampleRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AddSampleResult(sample.Id);
    }
}
