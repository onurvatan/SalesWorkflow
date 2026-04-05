using SalesWorkflow.Data;
using SalesWorkflow.Models;
using Xunit;

namespace SalesWorkflow.Tests.Data;

public class CustomerRepositoryTests
{
    private readonly ICustomerRepository _repo = new CustomerRepository();

    [Fact]
    public void GetAll_ReturnsFiveCustomers()
    {
        Assert.Equal(5, _repo.GetAll().Count);
    }

    [Fact]
    public void FindByCustomerId_ReturnsCorrectCustomer()
    {
        var customer = _repo.FindByCustomerId("CUST-003");
        Assert.NotNull(customer);
        Assert.Equal("Carol White", customer.Name);
        Assert.Equal(CustomerTier.VIP, customer.Tier);
    }

    [Fact]
    public void FindByCustomerId_CaseInsensitive()
    {
        var customer = _repo.FindByCustomerId("cust-001");
        Assert.NotNull(customer);
    }

    [Fact]
    public void FindByCustomerId_NoMatch_ReturnsNull()
    {
        var customer = _repo.FindByCustomerId("CUST-999");
        Assert.Null(customer);
    }

    [Fact]
    public void FindByName_ReturnsMatchingCustomers()
    {
        var results = _repo.FindByName("Eva");
        Assert.Single(results);
        Assert.Equal("CUST-005", results[0].CustomerId);
    }

    [Fact]
    public void FindByName_CaseInsensitive()
    {
        var results = _repo.FindByName("alice");
        Assert.Single(results);
    }

    [Fact]
    public void FindByName_NoMatch_ReturnsEmpty()
    {
        var results = _repo.FindByName("NoSuchPerson");
        Assert.Empty(results);
    }

    [Fact]
    public void GetSatisfactionSummary_TotalCustomers_IsFive()
    {
        var summary = _repo.GetSatisfactionSummary();
        Assert.Equal(5, summary.TotalCustomers);
    }

    [Fact]
    public void GetSatisfactionSummary_AverageScore_IsPositive()
    {
        var summary = _repo.GetSatisfactionSummary();
        Assert.True(summary.AverageScore > 0 && summary.AverageScore <= 5);
    }

    [Fact]
    public void GetSatisfactionSummary_AtRiskCustomers_ContainsDavidLee()
    {
        var summary = _repo.GetSatisfactionSummary();
        // David Lee has score 2 — at-risk threshold
        Assert.Contains(summary.AtRiskCustomers,
            s => s.Contains("David", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetSatisfactionSummary_CustomersByTier_ContainsAllThreeTiers()
    {
        var summary = _repo.GetSatisfactionSummary();
        Assert.Contains("Standard", summary.CustomersByTier.Keys);
        Assert.Contains("Premium", summary.CustomersByTier.Keys);
        Assert.Contains("VIP", summary.CustomersByTier.Keys);
    }
}
