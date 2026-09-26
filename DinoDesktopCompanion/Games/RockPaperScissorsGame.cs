using DinoDesktopCompanion.Statistics;

namespace DinoDesktopCompanion.Games;

public static class RockPaperScissorsGame
{
    public static RockPaperScissorsResult DetermineResult(RockPaperScissorsChoice player, RockPaperScissorsChoice dino)
    {
        if (player == dino) return RockPaperScissorsResult.Draw;
        return (player, dino) switch
        {
            (RockPaperScissorsChoice.Rock, RockPaperScissorsChoice.Scissors) or
            (RockPaperScissorsChoice.Scissors, RockPaperScissorsChoice.Paper) or
            (RockPaperScissorsChoice.Paper, RockPaperScissorsChoice.Rock) => RockPaperScissorsResult.Win,
            _ => RockPaperScissorsResult.Loss
        };
    }

    public static string GetGermanName(RockPaperScissorsChoice choice) => choice switch
    {
        RockPaperScissorsChoice.Rock => "Stein",
        RockPaperScissorsChoice.Scissors => "Schere",
        _ => "Papier"
    };
}

public enum RockPaperScissorsChoice
{
    Rock,
    Scissors,
    Paper
}
