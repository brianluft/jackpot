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
            Phase.Converting => "Re-encoding",
            Phase.MakingThumbnails => "Making thumbnails",
            Phase.Segmenting => "Splitting into parts",
            Phase.Encrypting => "Encrypting files",
            Phase.Verifying => "Verifying files",
            _ => phase.ToString(),
        };

        if (phase == Phase.Uploading && progress == 0)
            updateMessage("Waiting to upload");
        else
            updateMessage($"{text} ({progress * 100:0}%)");

        if (phase == Phase.Uploading)
            updateProgress(progress);
    }

    public void UpdateMessage(string message)
    {
        updateMessage(message);
    }
}
