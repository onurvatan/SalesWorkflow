using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

namespace SalesWorkflow.Tools;

/// <summary>
/// Simulates human-in-the-loop escalation.
/// In production this would issue an <c>ExternalRequest</c> on a <c>RequestPort</c> to pause
/// the workflow and await a human response via <c>StreamingRun.SendResponseAsync</c>.
/// For this demo it returns a structured escalation record so the agent can inform the customer.
/// </summary>
public static class EscalateTool
{
    public static AIFunction Create()
    {
        return AIFunctionFactory.Create(
            async ([Description("Brief reason for escalating to a human agent (e.g. 'Refund request over $500', 'Repeated shipping failure')")] string reason,
                   CancellationToken _) =>
            {
                var requestId = $"ESC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
                var result = new
                {
                    escalated = true,
                    requestId,
                    reason,
                    message = $"This case has been escalated to a human agent (request {requestId}). " +
                                  "A support specialist will contact the customer within 1 business day.",
                    // EXTENSION POINT: replace the body above with an ExternalRequest on a
                    // RequestPort to suspend the workflow until a human calls
                    // StreamingRun.SendResponseAsync(ExternalResponse) with their decision.
                    timestamp = DateTime.UtcNow.ToString("o")
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
            },
            name: "escalate_to_human",
            description: "Escalate a customer case to a human support agent when the issue requires manual intervention (e.g. high-value refunds, repeated failures, account disputes). Returns an escalation request ID and ETA.");
    }
}
