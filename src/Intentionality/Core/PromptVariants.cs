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
/// Rotates German prompt phrasings so repeated sessions feel less autopilot.
/// Never returns the same variant twice in a row for a given kind.
/// </summary>
public sealed class PromptVariants
{
    private static readonly IReadOnlyDictionary<PromptKind, string[]> Pools = new Dictionary<PromptKind, string[]>
    {
        [PromptKind.Work] =
        [
            "Woran arbeitest du gerade?",
            "Was steht als Nächstes an?",
            "Womit beschäftigst du dich jetzt?",
            "Worauf willst du dich jetzt konzentrieren?"
        ],
        [PromptKind.Goal] =
        [
            "Was ist das Ziel dieser Sitzung?",
            "Was willst du in dieser Sitzung schaffen?",
            "Womit wäre diese Sitzung erfolgreich?",
            "Was soll am Ende dieser Sitzung stehen?"
        ],
        [PromptKind.DoneCriterion] =
        [
            "Woran erkennst du, dass du fertig bist?",
            "Wann gilt dieses Ziel als erreicht?",
            "Was muss sichtbar sein, damit du stoppst?",
            "Welches Ergebnis zählt als fertig?"
        ],
        [PromptKind.Duration] =
        [
            "Wie viel Zeit brauchst du?",
            "Wie lange willst du dranbleiben?",
            "Wie viel Zeit nimmst du dir dafür?",
            "Bis wann willst du freigeschaltet bleiben?"
        ],
        [PromptKind.IfThen] =
        [
            "Was zieht dich am ehesten ab — und was tust du dann?",
            "Welche Ablenkung droht — und wie reagierst du?",
            "Was könnte dich unterbrechen — und dein Plan dagegen?",
            "Wenn dich etwas ablenkt: was und was tust du dann?"
        ],
        [PromptKind.ResumeNote] =
        [
            "Was ist der allererste nächste Schritt?",
            "Womit würdest du beim nächsten Mal starten?",
            "Welcher konkrete Schritt folgt als Nächstes?",
            "Was notierst du für den Wiedereinstieg?"
        ],
        [PromptKind.CompletionLead] =
        [
            "Fertig mit: ",
            "Geschafft: ",
            "Erledigt: ",
            "Ziel erreicht — "
        ],
        [PromptKind.DistractionLead] =
        [
            "Gehört das zu: ",
            "Ist das Teil von: ",
            "Passt das zu deinem Ziel: ",
            "Willst du das jetzt für: "
        ],
        [PromptKind.Extension] =
        [
            "Wie viel Zeit brauchst du noch?",
            "Wie lange brauchst du noch?",
            "Wie viel zusätzliche Zeit nimmst du dir?",
            "Bis wann willst du weiterarbeiten?"
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
