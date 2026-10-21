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
        AmbitionLevel.NotAmbitiousEnough => "Zu klein",
        AmbitionLevel.SlightlyTooEasy => "Zu klein",
        AmbitionLevel.AboutRight => "Passend",
        AmbitionLevel.ABitTooAmbitious => "Zu groß",
        AmbitionLevel.FarTooAmbitious => "Zu groß",
        _ => level.ToString()
    };
}
