using System;
using DinoDesktopCompanion.Progress;

namespace DinoDesktopCompanion.Services;

public sealed class DesktopActivityRewardService
{
    private readonly ProgressService _progress;
    private readonly object _gate = new();
    
    // Limits
    private const int MaxBonusApPerHour = 3;
    private int _bonusApGivenThisHour = 0;
    private DateTimeOffset _currentHourStart = DateTimeOffset.MinValue;
    
    // Cooldown between AP gains
    private DateTimeOffset _lastBonusTime = DateTimeOffset.MinValue;
    private readonly TimeSpan _cooldown = TimeSpan.FromMinutes(2);

    public DesktopActivityRewardService(ProgressService progress)
    {
        _progress = progress;
    }

    public bool TryRewardAP(string activityName)
    {
        var now = DateTimeOffset.Now;

        lock (_gate)
        {
            // Reset hour tracker if needed
            if (now - _currentHourStart >= TimeSpan.FromHours(1))
            {
                _currentHourStart = now;
                _bonusApGivenThisHour = 0;
            }

            // Check max AP
            if (_progress.Current.AdventurePoints >= _progress.Current.MaxAdventurePoints)
                return false;

            // Check hourly limit
            if (_bonusApGivenThisHour >= MaxBonusApPerHour)
                return false;

            // Check cooldown
            if (now - _lastBonusTime < _cooldown)
                return false;

            // Allow AP
            _bonusApGivenThisHour++;
            _lastBonusTime = now;
        }

        _progress.AddInstantAP(1);
        return true;
    }
}
