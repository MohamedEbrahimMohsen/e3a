using E3A.Application.Publishing.ProcessPublishJob;
using E3A.Application.Publishing.RegenerateMarketplace;
using E3A.Domain.Publishing;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace E3A.Jobs.Functions;

public class ProcessPublishJobFunction(ISender mediator, ILogger<ProcessPublishJobFunction> logger)
{
    private static readonly Action<ILogger, Guid, Exception?> LogDequeued = LoggerMessage.Define<Guid>(LogLevel.Information, new EventId(1, nameof(ProcessPublishJob)), "Processing publish job for version {VersionId}.");

    [Function("ProcessPublishJob")]
    public async Task ProcessPublishJob([QueueTrigger("%Azure:PublishQueueName%", Connection = "StorageAccountConnection")] PublishRequestedDomainEvent publishRequested, CancellationToken cancellationToken)
    {
        LogDequeued(logger, publishRequested.VersionId, null);

        await mediator.Send(new ProcessPublishJobCommand(publishRequested.VersionId), cancellationToken).ConfigureAwait(false);
        await mediator.Send(new RegenerateMarketplaceCommand(), cancellationToken).ConfigureAwait(false);
    }
}
