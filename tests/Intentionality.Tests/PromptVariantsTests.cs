using Intentionality.Core;

namespace Intentionality.Tests;

public class PromptVariantsTests
{
    [Fact]
    public void Pick_NeverRepeatsImmediately()
    {
        var prompts = new PromptVariants(new Random(42));
        string? previous = null;

        for (var i = 0; i < 40; i++)
        {
            var next = prompts.Pick(PromptKind.Work);
            Assert.Contains(next, PromptVariants.All(PromptKind.Work));
            if (previous is not null)
                Assert.NotEqual(previous, next);
            previous = next;
        }
    }

    [Fact]
    public void Pick_AllKinds_ReturnFromTheirPool()
    {
        var prompts = new PromptVariants(new Random(7));
        foreach (PromptKind kind in Enum.GetValues<PromptKind>())
        {
            var picked = prompts.Pick(kind);
            Assert.Contains(picked, PromptVariants.All(kind));
        }
    }
}
