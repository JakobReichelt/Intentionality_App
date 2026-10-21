namespace Intentionality.Core;

public enum PromptKind
{
    Work,
    Goal,
    DoneCriterion,
    Duration,
    IfThen,
    ResumeNote,
    CompletionLead,
    DistractionLead,
    Extension
}

/// <summary>
/// Rotates simple German prompts. Never returns the same variant twice in a row for a kind.
/// </summary>
public sealed class PromptVariants
{
    private static readonly IReadOnlyDictionary<PromptKind, string[]> Pools = new Dictionary<PromptKind, string[]>
    {
        [PromptKind.Work] =
        [
            "Woran arbeitest du?",
            "Was machst du jetzt?",
            "Worum geht es?"
        ],
        [PromptKind.Goal] =
        [
            "Was ist dein Ziel?",
            "Was willst du schaffen?",
            "Was soll fertig werden?"
        ],
        [PromptKind.DoneCriterion] =
        [
            "Wann bist du fertig?",
            "Woran merkst du, dass es fertig ist?",
            "Was ist dann erledigt?"
        ],
        [PromptKind.Duration] =
        [
            "Wie viel Zeit brauchst du?",
            "Wie lange brauchst du?"
        ],
        [PromptKind.IfThen] =
        [
            "Was lenkt dich ab — und was tust du dann?",
            "Welche Ablenkung — und dein Plan dagegen?"
        ],
        [PromptKind.ResumeNote] =
        [
            "Was ist der nächste Schritt?",
            "Womit machst du weiter?"
        ],
        [PromptKind.CompletionLead] =
        [
            "Fertig: ",
            "Erledigt: "
        ],
        [PromptKind.DistractionLead] =
        [
            "Gehört das zu: ",
            "Teil von: "
        ],
        [PromptKind.Extension] =
        [
            "Wie viel Zeit brauchst du noch?",
            "Wie lange noch?"
        ]
    };

    private readonly Dictionary<PromptKind, int> _lastIndex = new();
    private readonly Random _rng;

    public PromptVariants(Random? rng = null)
    {
        _rng = rng ?? Random.Shared;
    }

    public string Pick(PromptKind kind)
    {
        if (!Pools.TryGetValue(kind, out var pool) || pool.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Keine Varianten für diesen Prompt.");

        int index;
        if (pool.Length == 1)
        {
            index = 0;
        }
        else
        {
            var last = _lastIndex.TryGetValue(kind, out var previous) ? previous : -1;
            do
            {
                index = _rng.Next(pool.Length);
            } while (index == last);
        }

        _lastIndex[kind] = index;
        return pool[index];
    }

    public static IReadOnlyList<string> All(PromptKind kind) => Pools[kind];
}
