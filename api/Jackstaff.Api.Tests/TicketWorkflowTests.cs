using Jackstaff.Api.Modules.Tickets;
using Xunit;

namespace Jackstaff.Api.Tests;

public sealed class TicketWorkflowTests
{
    [Theory]
    [InlineData("open", "in_review", true)]
    [InlineData("open", "rejected", true)]
    [InlineData("in_review", "approved", true)]
    [InlineData("in_review", "in_progress", true)]
    [InlineData("approved", "in_progress", true)]
    [InlineData("in_progress", "resolved", true)]
    [InlineData("in_progress", "rejected", true)]
    [InlineData("resolved", "open", false)]
    [InlineData("rejected", "in_progress", false)]
    [InlineData("open", "resolved", false)]
    [InlineData("approved", "resolved", false)]
    public void CanTransition_EnforcesWorkflow(string current, string next, bool expected)
    {
        Assert.Equal(expected, TicketWorkflow.CanTransition(current, next));
    }

    [Fact]
    public void CanTransition_IsCaseInsensitive()
    {
        Assert.True(TicketWorkflow.CanTransition("OPEN", "In_Review"));
    }
}
