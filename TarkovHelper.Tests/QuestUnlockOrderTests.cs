using TarkovHelper.Models;
using TarkovHelper.Services;

namespace TarkovHelper.Tests;

/// <summary>
/// Guards the forward progression (unlock) ordering that sorts the quest list so prerequisites
/// appear before the quests they unlock (see the feature-quest-unlock-sort PRD). Exercises the pure
/// static <see cref="QuestGraphService.ComputeUnlockOrder"/> core with synthetic graphs, so no
/// service/DB is needed.
/// </summary>
public class QuestUnlockOrderTests
{
    private static TarkovTask Task(string normalized, string trader = "Prapor", string? name = null, string[]? previous = null)
        => new TarkovTask
        {
            NormalizedName = normalized,
            Name = name ?? normalized,
            Trader = trader,
            Previous = previous?.ToList(),
        };

    private static List<string> Order(params TarkovTask[] tasks)
        => QuestGraphService.ComputeUnlockOrder(tasks).Select(t => t.NormalizedName!).ToList();

    [Fact]
    public void Prerequisites_come_before_the_quests_they_unlock()
    {
        var a = Task("a");
        var b = Task("b", previous: new[] { "a" });
        var c = Task("c", previous: new[] { "b" });

        // Passed in reverse to prove the order comes from prerequisites, not input order.
        var order = Order(c, b, a);

        Assert.True(order.IndexOf("a") < order.IndexOf("b"));
        Assert.True(order.IndexOf("b") < order.IndexOf("c"));
    }

    [Fact]
    public void Every_prerequisite_precedes_its_dependent()
    {
        var tasks = new[]
        {
            Task("root"),
            Task("mid", previous: new[] { "root" }),
            Task("leaf1", previous: new[] { "mid" }),
            Task("leaf2", previous: new[] { "mid" }),
            Task("parallel"),
        };

        var ordered = QuestGraphService.ComputeUnlockOrder(tasks);
        var index = ordered
            .Select((t, i) => (t.NormalizedName!, i))
            .ToDictionary(x => x.Item1, x => x.i, StringComparer.OrdinalIgnoreCase);

        foreach (var t in tasks)
        {
            if (t.Previous == null) continue;
            foreach (var p in t.Previous)
                Assert.True(index[p] < index[t.NormalizedName!], $"{p} should precede {t.NormalizedName}");
        }
    }

    [Fact]
    public void Order_is_deterministic()
    {
        var tasks = new[]
        {
            Task("b-quest", "Therapist"),
            Task("a-quest", "Prapor"),
            Task("c-quest", "Prapor"),
        };

        Assert.Equal(Order(tasks), Order(tasks));
    }

    [Fact]
    public void Independent_quests_tiebreak_by_trader_then_name()
    {
        // No prerequisite relationships: canonical trader order (Prapor before Jaeger) wins,
        // then English name within the same trader.
        var jaeger = Task("z-jaeger", "Jaeger");
        var praporB = Task("b-prapor", "Prapor");
        var praporA = Task("a-prapor", "Prapor");

        Assert.Equal(new[] { "a-prapor", "b-prapor", "z-jaeger" }, Order(jaeger, praporB, praporA));
    }

    [Fact]
    public void Cycles_do_not_hang_and_include_all_nodes()
    {
        var a = Task("a", previous: new[] { "b" });
        var b = Task("b", previous: new[] { "a" });

        var order = Order(a, b);

        Assert.Contains("a", order);
        Assert.Contains("b", order);
        Assert.Equal(2, order.Count);
    }
}
