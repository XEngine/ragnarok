using Ragnarok.Features.NearbyStorage;

int checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception(message);
}

void Expect(int requested, int[] stock, bool success, params int[] expected)
{
    bool result = WithdrawalPlan.TryCreate(requested, stock, out int[] plan);
    Check(result == success, $"Wrong availability result for {requested} from [{string.Join(',', stock)}]");
    Check(plan.SequenceEqual(expected), "Wrong withdrawal order or partial withdrawal on failure");
}

Expect(4, [10, 20, 30], true, 4, 0, 0);       // Player first.
Expect(8, [3, 4, 20], true, 3, 4, 1);         // Split across nearest chests.
Expect(5, [0, 0, 5], true, 0, 0, 5);          // Chest-only crafting.
Expect(8, [3, 4], false, 0, 0);               // No partial payment.
Expect(7, [3, 4], true, 3, 4);                // Exact balance.
Expect(0, [3, 4], true, 0, 0);                // No-cost action.
Expect(1, [], false);                         // No inventories.
Expect(int.MaxValue, [int.MaxValue, 1], true, int.MaxValue, 0); // Sum cannot overflow.

Random random = new(1977);
for (int iteration = 0; iteration < 5000; iteration++)
{
    int[] stock = Enumerable.Range(0, random.Next(1, 10)).Select(_ => random.Next(0, 100)).ToArray();
    int requested = random.Next(0, 600);
    int[] original = (int[])stock.Clone();
    bool result = WithdrawalPlan.TryCreate(requested, stock, out int[] plan);
    Check(stock.SequenceEqual(original), "Planning mutated inventory counts");
    Check(result == (stock.Sum() >= requested), "Availability does not match total stock");
    Check(plan.Sum() == (result ? requested : 0), "Resources were created, lost, or partially spent");
    for (int i = 0; i < stock.Length; i++)
    {
        Check(plan[i] >= 0 && plan[i] <= stock[i], "A stack was overdrawn");
        if (i > 0 && plan[i] > 0) Check(plan[i - 1] == stock[i - 1], "Skipped a higher-priority source");
    }
}
Console.WriteLine($"PASS: {checks} allocation checks, including 5,000 randomized multi-inventory transactions.");

Check(Math.Abs(Ragnarok.Features.Gameplay.DeathPenalty.Scale(0.05f, -50) - 0.025f) < 0.00001f, "Half penalty should turn 5% loss into 2.5%");
Check(Ragnarok.Features.Gameplay.DeathPenalty.Scale(0.05f, -100) == 0f, "No-loss setting");
Check(Ragnarok.Features.Gameplay.DeathPenalty.Scale(0.05f, 0) == 0.05f, "Unchanged penalty");
Check(Ragnarok.Features.Gameplay.DeathPenalty.Scale(0f, 100) == 0f, "Cannot introduce level loss where world settings disable it");
Check(Ragnarok.Features.Gameplay.DeathPenalty.Scale(0.8f, 100) == 1f, "Increased penalties cannot produce negative skill levels");
Console.WriteLine("PASS: death penalty scaling and boundaries.");

ConfigSchemaChecks.Run(Check);
PickupQueueChecks.Run(Check);
StatusCategoryChecks.Run(Check);
StaminaRecoveryChecks.Run(Check);
MountBondChecks.Run(Check);
MountProgressChecks.Run(Check);
MountTemperamentChecks.Run(Check);
HoverPromptChecks.Run(Check);
CraftSearchChecks.Run(Check);
CraftFrameChecks.Run(Check);
RecipeDetailsChecks.Run(Check);
GraveRecoveryChecks.Run(Check);
BuildMenuChecks.Run(Check);

ConfigSliderChecks.Run(Check);
