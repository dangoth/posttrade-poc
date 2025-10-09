using Microsoft.AspNetCore.Mvc;
using PostTradeSystem.Infrastructure.Services;

namespace PostTradeSystem.Api.Endpoints;

public static class OutboxAdminEndpoints
{
    public static void MapOutboxAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/outbox")
            .WithTags("Outbox Administration")
            .WithOpenApi();

        // Get dead lettered events
        group.MapGet("/dead-letters", async (
            IOutboxService outboxService, 
            int? limit,
            CancellationToken cancellationToken) =>
        {
            var batchSize = Math.Min(limit ?? 100, 1000);
            var result = await outboxService.GetDeadLetteredEventsAsync(batchSize, cancellationToken);
            
            if (result.IsSuccess)
            {
                return Results.Ok(new 
                { 
                    Events = result.Value,
                    TotalReturned = result.Value!.Count(),
                    RequestedLimit = batchSize
                });
            }
            else
            {
                return Results.Problem($"Error retrieving dead lettered events: {result.Error}");
            }
        })
        .WithName("GetDeadLetteredEvents")
        .WithSummary("Get all dead lettered events")
        .WithDescription("Retrieves events that have been moved to the dead letter queue after exceeding retry limits. Use ?limit=N to control batch size (max 1000)");

        // Get dead lettered event count
        group.MapGet("/dead-letters/count", async (IOutboxService outboxService, CancellationToken cancellationToken) =>
        {
            var result = await outboxService.GetDeadLetteredEventCountAsync(cancellationToken);
            
            if (result.IsSuccess)
            {
                return Results.Ok(new { Count = result.Value });
            }
            else
            {
                return Results.Problem($"Error getting dead lettered event count: {result.Error}");
            }
        })
        .WithName("GetDeadLetteredEventCount")
        .WithSummary("Get count of dead lettered events")
        .WithDescription("Returns the total number of events currently in the dead letter queue");

        // Reprocess a dead lettered event
        group.MapPost("/dead-letters/{eventId}/reprocess", async (
            long eventId, 
            IOutboxService outboxService, 
            CancellationToken cancellationToken) =>
        {
            var result = await outboxService.ReprocessDeadLetteredEventAsync(eventId, cancellationToken);
            
            if (result.IsSuccess)
            {
                return Results.Ok(new { Message = $"Event {eventId} has been reset for reprocessing" });
            }
            else
            {
                return Results.Problem($"Error reprocessing event {eventId}: {result.Error}");
            }
        })
        .WithName("ReprocessDeadLetteredEvent")
        .WithSummary("Reprocess a dead lettered event")
        .WithDescription("Resets a dead lettered event back to unprocessed state for retry");

        // Bulk reprocess dead lettered events
        group.MapPost("/dead-letters/reprocess-all", async (
            IOutboxService outboxService, 
            int? limit,
            CancellationToken cancellationToken) =>
        {
            var batchSize = Math.Min(limit ?? 1000, 1000); // Default and max 1000 for bulk operations
            var deadLettersResult = await outboxService.GetDeadLetteredEventsAsync(batchSize, cancellationToken);
            
            if (deadLettersResult.IsFailure)
            {
                return Results.Problem($"Error retrieving dead lettered events: {deadLettersResult.Error}");
            }
            
            var deadLetters = deadLettersResult.Value!;
            var reprocessedCount = 0;
            var failedCount = 0;
            var failures = new List<object>();
            
            foreach (var deadLetter in deadLetters)
            {
                var reprocessResult = await outboxService.ReprocessDeadLetteredEventAsync(deadLetter.Id, cancellationToken);
                if (reprocessResult.IsSuccess)
                {
                    reprocessedCount++;
                }
                else
                {
                    failedCount++;
                    failures.Add(new { 
                        EventId = deadLetter.Id, 
                        Error = reprocessResult.Error 
                    });
                }
            }
            
            var response = new { 
                Message = $"Processed {deadLetters.Count()} events: {reprocessedCount} succeeded, {failedCount} failed",
                ReprocessedCount = reprocessedCount,
                FailedCount = failedCount,
                Failures = failures
            };
            
            return Results.Ok(response);
        })
        .WithName("BulkReprocessDeadLetteredEvents")
        .WithSummary("Reprocess all dead lettered events")
        .WithDescription("Resets all dead lettered events back to unprocessed state for retry");

        // Get outbox statistics
        group.MapGet("/statistics", async (IOutboxService outboxService, CancellationToken cancellationToken) =>
        {
            var result = await outboxService.GetDeadLetteredEventCountAsync(cancellationToken);
            
            if (result.IsSuccess)
            {
                // for now just basic stats, extendable
                var statistics = new
                {
                    DeadLetteredCount = result.Value,
                    Timestamp = DateTime.UtcNow
                };
                
                return Results.Ok(statistics);
            }
            else
            {
                return Results.Problem($"Error retrieving outbox statistics: {result.Error}");
            }
        })
        .WithName("GetOutboxStatistics")
        .WithSummary("Get outbox processing statistics")
        .WithDescription("Returns various statistics about outbox event processing including dead letter counts");
    }
}