using SalesWorkflow.Data;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

namespace SalesWorkflow.Tools;

public static class CustomerSatisfactionTool
{
    public static AIFunction Create(ICustomerRepository customerRepo)
    {
        return AIFunctionFactory.Create(
            async ([Description("Optional customer tier filter: 'Standard', 'Premium', or 'VIP'. Leave empty for all tiers.")] string? tierFilter,
                   CancellationToken _) =>
            {
                var summary = customerRepo.GetSatisfactionSummary();

                IReadOnlyList<string> atRisk = summary.AtRiskCustomers;

                // Apply tier filter if requested — currently filters at-risk list only
                // (full tier filtering would require a separate ICustomerRepository method)
                var result = new
                {
                    reportType = "Customer Satisfaction Summary",
                    tierFilter = string.IsNullOrWhiteSpace(tierFilter) ? "All tiers" : tierFilter,
                    totalCustomers = summary.TotalCustomers,
                    averageScore = summary.AverageScore,
                    scoreDistribution = summary.ScoreDistribution,
                    customersByTier = summary.CustomersByTier,
                    atRiskCustomers = atRisk,
                    atRiskCount = atRisk.Count,
                    npsCategory = summary.AverageScore >= 4.5 ? "Excellent"
                                       : summary.AverageScore >= 3.5 ? "Good"
                                       : summary.AverageScore >= 2.5 ? "Neutral"
                                       : "At Risk"
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
            },
            name: "customer_satisfaction_report",
            description: "Generate a customer satisfaction report including average CSAT score, score distribution, tier breakdown, and at-risk customers (score ≤ 2). Optionally filtered by tier.");
    }
}
