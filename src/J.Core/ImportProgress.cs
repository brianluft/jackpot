namespace J.Core;

public sealed class ImportProgress(bool needsConversion, Action<double> updateProgress, Action<string> updateMessage)
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
        {
            updateProgress(0.5d + progress / 2);
        }
        else
        {
            var total = 5;
            if (!needsConversion)
                total--;

            var n = phase switch
            {
                Phase.Converting => 0,
                Phase.MakingThumbnails => 1,
                Phase.Segmenting => 2,
                Phase.Encrypting => 3,
                Phase.Verifying => 4,
                _ => throw new ArgumentOutOfRangeException(nameof(phase)),
            };

            if (n > 0 && !needsConversion)
                n--;

            updateProgress((n + progress) / total / 2);
        }
    }

    public void UpdateMessage(string message)
    {
        updateMessage(message);
    }
}
