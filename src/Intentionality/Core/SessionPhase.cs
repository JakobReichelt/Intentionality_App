namespace Intentionality.Core;

public enum SessionPhase
{
    AskingWork,
    AskingDuration,
    AskingGoal,
    Unlocked,
    AskingCompletion,
    AskingReflection,
    AskingExtension,
    OnBreak,
    // Appended for JSON string-enum compatibility with older state files.
    AskingDoneCriterion,
    AskingIfThen,
    AskingResumeNote,
    CheckingDistraction
}
