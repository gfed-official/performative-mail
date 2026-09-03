namespace PerformativeMail.Client.UI;

public abstract record InteractPrompt
{
    private InteractPrompt()
    {
    }

    public sealed record None : InteractPrompt
    {
        public static None Instance { get; } = new();
    }

    public sealed record Deliver(string HeldAddress, string TargetAddress) : InteractPrompt;
}
