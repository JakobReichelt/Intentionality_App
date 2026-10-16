using Intentionality.Services;

namespace Intentionality.Tests;

public class KeyboardHookTests
{
    const uint VkLwin = 0x5B;
    const uint VkRwin = 0x5C;
    const uint VkK = 0x4B;
    const uint VkReturn = 0x0D;
    const uint VkEscape = 0x1B;
    const uint Injected = 0x10;
    const uint KeyUp = 0x80;

    [Fact]
    public void PhysicalWinKey_IsSuppressed()
    {
        Assert.True(KeyboardHook.ShouldSuppress(VkLwin, flags: 0, controlDown: false));
        Assert.True(KeyboardHook.ShouldSuppress(VkRwin, flags: KeyUp, controlDown: false));
    }

    [Fact]
    public void InjectedWinKeyUp_ReachesTheSystem()
    {
        Assert.False(KeyboardHook.ShouldSuppress(VkLwin, flags: Injected | KeyUp, controlDown: false));
        Assert.False(KeyboardHook.ShouldSuppress(VkRwin, flags: Injected, controlDown: false));
    }

    [Fact]
    public void EnterAndK_AreNotSuppressed()
    {
        Assert.False(KeyboardHook.ShouldSuppress(VkReturn, flags: 0, controlDown: false));
        Assert.False(KeyboardHook.ShouldSuppress(VkK, flags: 0, controlDown: false));
    }

    [Fact]
    public void CtrlEscape_IsSuppressed_PlainEscapeIsNot()
    {
        Assert.True(KeyboardHook.ShouldSuppress(VkEscape, flags: 0, controlDown: true));
        Assert.False(KeyboardHook.ShouldSuppress(VkEscape, flags: 0, controlDown: false));
    }
}
