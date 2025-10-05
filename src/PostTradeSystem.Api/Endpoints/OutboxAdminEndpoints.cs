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
            try
            {
                var batchSize = Math.Min(limit ?? 100, 1000);
                var deadLetters = await outboxService.GetDeadLetteredEventsAsync(batchSize, cancellationToken);
                return Results.Ok(new 
                { 
                    Events = deadLetters,
                    TotalReturned = deadLetters.Count(),
                    RequestedLimit = batchSize
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error retrieving dead lettered events: {ex.Message}");
            }
        })
        .WithName("GetDeadLetteredEvents")
        .WithSummary("Get all dead lettered events")
        .WithDescription("Retrieves events that have been moved to the dead letter queue after exceeding retry limits. Use ?limit=N to control batch size (max 1000)");

        // Get dead lettered event count
        group.MapGet("/dead-letters/count", async (IOutboxService outboxService, CancellationToken cancellationToken) =>
        {
            try
            {
                var count = await outboxService.GetDeadLetteredEventCountAsync(cancellationToken);
                return Results.Ok(new { Count = count });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error getting dead lettered event count: {ex.Message}");
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
            try
            {
                await outboxService.ReprocessDeadLetteredEventAsync(eventId, cancellationToken);
                return Results.Ok(new { Message = $"Event {eventId} has been reset for reprocessing" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error reprocessing event {eventId}: {ex.Message}");
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
            try
            {
                var batchSize = Math.Min(limit ?? 1000, 1000); // Default and max 1000 for bulk operations
                var deadLetters = await outboxService.GetDeadLetteredEventsAsync(batchSize, cancellationToken);
                var reprocessedCount = 0;
                var failedCount = 0;
                var failures = new List<object>();
                
                foreach (var deadLetter in deadLetters)
                {
                    try
                    {
                        await outboxService.ReprocessDeadLetteredEventAsync(deadLetter.Id, cancellationToken);
                        reprocessedCount++;
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        failures.Add(new { 
                            EventId = deadLetter.Id, 
                            Error = ex.Message 
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
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error bulk reprocessing dead lettered events: {ex.Message}");
            }
        })
        .WithName("BulkReprocessDeadLetteredEvents")
        .WithSummary("Reprocess all dead lettered events")
        .WithDescription("Resets all dead lettered events back to unprocessed state for retry");

        // Get outbox statistics
        group.MapGet("/statistics", async (IOutboxService outboxService, CancellationToken cancellationToken) =>
        {
            try
            {
                var deadLetterCount = await outboxService.GetDeadLetteredEventCountAsync(cancellationToken);
                
                // for now just basic stats, extendable
                var statistics = new
                {
                    DeadLetteredCount = deadLetterCount,
                    Timestamp = DateTime.UtcNow
                };
                
                return Results.Ok(statistics);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error retrieving outbox statistics: {ex.Message}");
            }
        })
        .WithName("GetOutboxStatistics")
        .WithSummary("Get outbox processing statistics")
        .WithDescription("Returns various statistics about outbox event processing including dead letter counts");
    }
}