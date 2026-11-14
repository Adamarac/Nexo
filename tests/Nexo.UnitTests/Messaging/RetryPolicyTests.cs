using Nexo.Infrastructure.Messaging;

namespace Nexo.UnitTests.Messaging;

public sealed class RetryPolicyTests
{
    private static readonly RetryPolicy Policy =
        new([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(25)]);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 25)]
    public void DelayAfterFailure_WaitsLongerEachTime(int failures, int expectedMinutes)
    {
        Policy.DelayAfterFailure(failures).Should().Be(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Fact]
    public void DelayAfterFailure_GivesUpAfterTheLastDelay()
    {
        Policy.DelayAfterFailure(4).Should().BeNull();
    }
}
