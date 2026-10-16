namespace Intentionality.Services;

public static class EndSessionSignal
{
    public const string Name = "IntentionalityEndSession";
    public const string Argument = "--end-session";

    public static void Pulse()
    {
        try
        {
            using var handle = EventWaitHandle.OpenExisting(Name);
            handle.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }
}
