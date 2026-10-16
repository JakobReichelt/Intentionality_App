namespace Intentionality.Core;

public enum AmbitionLevel
{
    NotAmbitiousEnough,
    SlightlyTooEasy,
    AboutRight,
    ABitTooAmbitious,
    FarTooAmbitious
}

public static class AmbitionLevelLabels
{
    public static string ToDisplay(AmbitionLevel level) => level switch
    {
        AmbitionLevel.NotAmbitiousEnough => "Viel zu klein",
        AmbitionLevel.SlightlyTooEasy => "Zu klein",
        AmbitionLevel.AboutRight => "Passend",
        AmbitionLevel.ABitTooAmbitious => "Zu groß, aufteilen",
        AmbitionLevel.FarTooAmbitious => "Viel zu groß, aufteilen",
        _ => level.ToString()
    };
}
