namespace J.Core;

public sealed class ImportProgress(Action<double> updateProgress, Action<string> updateMessage)
{
    public enum Phase
    {
        Converting,
        MakingThumbnails,
        Segmenting,
        Encrypting,
        Verifying,
        Uploading,
    }

    public void UpdateProgress(Phase phase, double progress)
    {
        var text = phase switch
        {
            Phase.MakingThumbnails => "Making thumbnails",
            _ => phase.ToString(),
        };

        updateMessage($"{text} ({progress * 100:0}%)");

        if (phase == Phase.Uploading)
            updateProgress(progress);
    }

    public void UpdateMessage(string message)
    {
        updateMessage(message);
    }
}
