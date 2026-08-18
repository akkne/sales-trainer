namespace Sellevate.Ai.Features.Dialog.Constants;

/// <summary>
/// The grade a dialog is judged on, and its conversion to the scale the rest of the platform reads.
///
/// <para>
/// Two scales exist and neither may drift. This service asks the model for and shows the learner a
/// 0–10 grade; every score in learning-service is on 0–100, so an assignment threshold written as
/// «оценка >= 70» is comparable only after multiplying by
/// <see cref="LearningServiceScaleFactor"/>. That multiplication crosses a service boundary in the
/// <c>DialogEvaluated</c> event, so changing either number silently re-grades every assignment.
/// </para>
/// </summary>
public static class DialogScoreScale
{
    /// <summary>Lowest grade. A conversation the manager wrecked.</summary>
    public const int Minimum = 0;

    /// <summary>Highest grade the model is asked for and the learner is shown.</summary>
    public const int Maximum = 10;

    /// <summary>Multiplier taking a 0–10 grade onto learning-service's 0–100 scale.</summary>
    public const int LearningServiceScaleFactor = 10;
}
