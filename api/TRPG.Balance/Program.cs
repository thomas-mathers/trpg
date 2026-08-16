using TRPG.Balance;
using TRPG.Domain.Models;

var mode = args.Length > 0 ? args[0] : "fight";

switch (mode)
{
    case "fight":
        var fightTrialCount =
            args.Length > 1 && int.TryParse(args[1], out var parsedFightTrials)
                ? parsedFightTrials
                : 1000;
        var fightOutputPath = args.Length > 2 ? args[2] : "fight-results.csv";
        await FightLogExperiment.Run(fightTrialCount, fightOutputPath);
        break;

    case "matrix":
        var matrixTrials =
            args.Length > 1 && int.TryParse(args[1], out var parsedMatrixTrials)
                ? parsedMatrixTrials
                : 100;
        var matrixLevel =
            args.Length > 2 && int.TryParse(args[2], out var parsedLevel) ? parsedLevel : 100;
        var matrixOutputPath = args.Length > 3 ? args[3] : "profession-monster-win-rates.csv";
        await ProfessionMatrixExperiment.Run(matrixTrials, matrixLevel, matrixOutputPath);
        break;

    case "diagnose":
        var diagnoseProfession = Enum.Parse<Profession>(args[1]);
        var diagnoseMonsterType = Enum.Parse<CreatureType>(args[2]);
        var diagnoseLevel =
            args.Length > 3 && int.TryParse(args[3], out var parsedDiagnoseLevel)
                ? parsedDiagnoseLevel
                : 100;
        DiagnosticExperiment.Run(diagnoseProfession, diagnoseMonsterType, diagnoseLevel);
        break;

    case "defensecurve":
        var targetHitChance =
            args.Length > 1 && double.TryParse(args[1], out var parsedHitChance)
                ? parsedHitChance
                : 0.75;
        var defenseCurveOutputPath = args.Length > 2 ? args[2] : "defense-evasion-curve.csv";
        await DefenseCurveExperiment.Run(targetHitChance, defenseCurveOutputPath);
        break;

    case "trace":
        var traceProfession = Enum.Parse<Profession>(args[1]);
        var traceMonsterType = Enum.Parse<CreatureType>(args[2]);
        var traceLevel =
            args.Length > 3 && int.TryParse(args[3], out var parsedTraceLevel)
                ? parsedTraceLevel
                : 100;
        var traceTrialCount =
            args.Length > 4 && int.TryParse(args[4], out var parsedTraceTrials)
                ? parsedTraceTrials
                : 1;
        TraceExperiment.Run(traceProfession, traceMonsterType, traceLevel, traceTrialCount);
        break;

    default:
        Console.WriteLine(
            $"Unknown mode '{mode}'. Expected 'fight', 'matrix', 'diagnose', 'defensecurve', or 'trace'."
        );
        break;
}
