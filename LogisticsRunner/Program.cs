using Logistics.Runner;

// dotnet run --project LogisticsRunner -- [--days N] [--towns N] [--nests N] [--seed N] [--save-at HOUR]   (docs/balance-runner.md)
var options = new RunOptions();
for (int i = 0; i + 1 < args.Length; i += 2)
{
    options = args[i] switch
    {
        "--days" => options with { Days = int.Parse(args[i + 1]) },
        "--towns" => options with { Towns = int.Parse(args[i + 1]) },
        "--nests" => options with { Nests = int.Parse(args[i + 1]) },
        "--seed" => options with { Seed = ulong.Parse(args[i + 1]) },
        "--save-at" => options with { SaveAtHour = int.Parse(args[i + 1]) },
        _ => throw new ArgumentException($"Unknown option {args[i]}."),
    };
}

var report = BalanceRunner.Run(options);
foreach (var day in report.Days)
{
    string held = string.Join(" ", day.Held.Select(h => $"{h.Key}={h.Value}"));
    string events = string.Join(" ", day.Events.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => $"{e.Key}={e.Value}"));
    Console.WriteLine($"day {day.Day,3}  {held}  penalized={day.Penalized}  {events}");
}

if (options.SaveAtHour is not null)
{
    var straight = BalanceRunner.Run(options with { SaveAtHour = null });
    Console.WriteLine(straight.Hash == report.Hash ? "save and load: same hash" : "save and load: HASH DIFFERS");
    if (straight.Hash != report.Hash) return 1;
}

Console.WriteLine($"hash {report.Hash:X16}");
return 0;
