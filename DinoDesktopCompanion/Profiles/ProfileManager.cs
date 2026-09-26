using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.Profiles;

public sealed class ProfilesData
{
    public int SaveVersion { get; set; } = ProfileManager.CurrentSaveVersion;
    public List<ProfileInfo> Profiles { get; set; } = [];
    public string? ActiveProfileId { get; set; }
    public string? LastUsedProfileId { get; set; }
}

public sealed class ProfileManager
{
    public const int CurrentSaveVersion = 2;
    private static readonly HashSet<string> AllowedProfileColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "#789E5B", "#4E8BC7", "#D979A5", "#8B6CC4", "#E3BC45", "#E58A45"
    };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _dataDirectory;
    private readonly string _profilesJsonPath;
    private readonly string _profilesDirectory;
    private readonly string _backupsDirectory;
    private readonly FileLogger _logger;
    private ProfilesData _data = new();

    public event EventHandler<ProfileInfo>? ActiveProfileChanged;
    public IReadOnlyList<ProfileInfo> Profiles => _data.Profiles.OrderByDescending(p => p.LastPlayed).ToArray();
    public ProfileInfo? ActiveProfile => _data.Profiles.FirstOrDefault(p => p.Id == _data.ActiveProfileId);
    public string? LastError { get; private set; }

    public ProfileManager(FileLogger logger, string? dataDirectory = null)
    {
        _logger = logger;
        _dataDirectory = dataDirectory ?? Path.Combine(AppContext.BaseDirectory, "Data");
        _profilesDirectory = Path.Combine(_dataDirectory, "Profiles");
        _backupsDirectory = Path.Combine(_dataDirectory, "Backups");
        _profilesJsonPath = Path.Combine(_dataDirectory, "profiles.json");
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_profilesJsonPath))
                _data = JsonSerializer.Deserialize<ProfilesData>(File.ReadAllText(_profilesJsonPath), JsonOptions) ?? new ProfilesData();

            ValidateAndMigrateIndex();
            if (_data.Profiles.Count == 0) MigrateLegacyDataIfPresent();
            RestoreLastActiveProfile();
            SaveIndex();
            if (ActiveProfile is not null) EnsureDailyBackup(ActiveProfile.Id);
        }
        catch (Exception ex)
        {
            _logger.Error("Fehler beim Laden der Profilübersicht.", ex);
            _data = new ProfilesData();
        }
    }

    private void ValidateAndMigrateIndex()
    {
        if (_data.SaveVersion > CurrentSaveVersion)
            throw new InvalidDataException($"SaveVersion {_data.SaveVersion} wird von dieser App nicht unterstützt.");

        if (_data.SaveVersion < CurrentSaveVersion)
        {
            BackupAllProfiles("vor-migration");
            _data.SaveVersion = CurrentSaveVersion;
        }

        _data.Profiles = (_data.Profiles ?? [])
            .Where(profile => profile is not null && IsValidId(profile.Id) && profile.SaveVersion <= CurrentSaveVersion)
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        foreach (var profile in _data.Profiles)
        {
            NormalizeProfileFields(profile);
            WriteProfileMetadata(profile);
        }
    }

    private void RestoreLastActiveProfile()
    {
        var candidate = _data.ActiveProfileId ?? _data.LastUsedProfileId;
        if (candidate is null || _data.Profiles.All(p => p.Id != candidate))
            candidate = _data.Profiles.OrderByDescending(p => p.LastPlayed).FirstOrDefault()?.Id;
        _data.ActiveProfileId = candidate;
        _data.LastUsedProfileId = candidate;
    }

    private void MigrateLegacyDataIfPresent()
    {
        var legacyFiles = new[] { "progress.json", "achievements.json", "statistics.json", "stats.json", "collections.json", "expeditions.json", "inventory.json", "task-progress.json", "events.json" };
        if (!legacyFiles.Any(file => File.Exists(Path.Combine(_dataDirectory, file)))) return;

        var profile = new ProfileInfo { ProfileName = "Mein Dino", DinoName = "Dino", LastPlayed = DateTime.Now };
        _data.Profiles.Add(profile);
        _data.ActiveProfileId = profile.Id;
        _data.LastUsedProfileId = profile.Id;
        var target = GetProfileDirectory(profile.Id);
        Directory.CreateDirectory(target);
        foreach (var file in legacyFiles)
        {
            var source = Path.Combine(_dataDirectory, file);
            if (File.Exists(source)) File.Copy(source, Path.Combine(target, file), true);
        }
        WriteProfileMetadata(profile);
        CreateBackup(profile.Id, "legacy-migration");
    }

    public string GetProfileDirectory(string profileId)
    {
        if (!IsValidId(profileId)) throw new ArgumentException("Ungültige Profil-ID.", nameof(profileId));
        return Path.Combine(_profilesDirectory, profileId);
    }

    public string GetActiveProfileDirectory() => ActiveProfile is { } profile
        ? GetProfileDirectory(profile.Id)
        : throw new InvalidOperationException("Es ist kein Dino-Profil aktiv.");

    public ProfileInfo CreateProfile(string profileName, string dinoName, int? birthdayDay = null, int? birthdayMonth = null, string? profileColor = null, string? note = null)
    {
        ValidateBirthday(birthdayDay, birthdayMonth);
        var profile = new ProfileInfo
        {
            ProfileName = RequireName(profileName, "Profilname"),
            DinoName = NormalizeName(dinoName, "Dino"),
            BirthdayDay = birthdayDay,
            BirthdayMonth = birthdayMonth,
            Note = NormalizeNote(note),
            ProfileColor = NormalizeProfileColor(profileColor),
            LastPlayed = DateTime.Now,
            SaveVersion = CurrentSaveVersion
        };
        Directory.CreateDirectory(GetProfileDirectory(profile.Id));
        WriteProfileMetadata(profile);
        _data.Profiles.Add(profile);
        SaveIndex();
        return profile;
    }

    public bool SetActiveProfile(string id)
    {
        LastError = null;
        var profile = _data.Profiles.FirstOrDefault(p => p.Id == id);
        if (profile is null)
        {
            LastError = "Das Profil wurde nicht gefunden.";
            return false;
        }

        var previousActiveId = _data.ActiveProfileId;
        var previousLastUsedId = _data.LastUsedProfileId;
        var previousLastPlayed = profile.LastPlayed;
        try
        {
            NormalizeProfileFields(profile);
            _data.ActiveProfileId = id;
            _data.LastUsedProfileId = id;
            profile.LastPlayed = DateTime.Now;
            WriteProfileMetadata(profile);
            SaveIndex();
        }
        catch (Exception ex)
        {
            _data.ActiveProfileId = previousActiveId;
            _data.LastUsedProfileId = previousLastUsedId;
            profile.LastPlayed = previousLastPlayed;
            LastError = "Das Profil konnte nicht aktiviert werden.";
            _logger.Error($"Profil {id} konnte nicht aktiviert werden.", ex);
            return false;
        }

        try { EnsureDailyBackup(id); }
        catch (Exception ex) { _logger.Error($"Backup für Profil {id} konnte nicht erstellt werden.", ex); }
        ActiveProfileChanged?.Invoke(this, profile);
        return true;
    }

    public bool DeleteProfile(string id)
    {
        var profile = _data.Profiles.FirstOrDefault(p => p.Id == id);
        if (profile is null) return false;
        CreateBackup(id, "vor-loeschen");
        _data.Profiles.Remove(profile);
        if (_data.ActiveProfileId == id)
        {
            var replacement = _data.Profiles.OrderByDescending(p => p.LastPlayed).FirstOrDefault();
            _data.ActiveProfileId = replacement?.Id;
            _data.LastUsedProfileId = replacement?.Id;
        }
        SaveIndex();
        var directory = GetProfileDirectory(id);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        if (ActiveProfile is { } active) ActiveProfileChanged?.Invoke(this, active);
        return true;
    }

    public bool UpdateProfile(string id, string profileName, string dinoName, int? birthdayDay, int? birthdayMonth, string? profileColor = null, string? note = null)
    {
        var profile = _data.Profiles.FirstOrDefault(p => p.Id == id);
        if (profile is null) return false;
        ValidateBirthday(birthdayDay, birthdayMonth);
        profile.ProfileName = RequireName(profileName, "Profilname");
        profile.DinoName = NormalizeName(dinoName, "Dino");
        profile.BirthdayDay = birthdayDay;
        profile.BirthdayMonth = birthdayMonth;
        profile.Note = NormalizeNote(note ?? profile.Note);
        profile.ProfileColor = NormalizeProfileColor(profileColor ?? profile.ProfileColor);
        WriteProfileMetadata(profile);
        SaveIndex();
        return true;
    }

    public void RenameProfile(string id, string newName)
    {
        var profile = _data.Profiles.FirstOrDefault(p => p.Id == id);
        if (profile is not null) UpdateProfile(id, newName, profile.DinoName, profile.BirthdayDay, profile.BirthdayMonth, profile.ProfileColor, profile.Note);
    }

    public bool SaveProfile(string id)
    {
        var profile = _data.Profiles.FirstOrDefault(p => p.Id == id);
        if (profile is null) return false;
        NormalizeProfileFields(profile);
        WriteProfileMetadata(profile);
        SaveIndex();
        return true;
    }

    public bool ExportProfile(string id, string archivePath)
    {
        LastError = null;
        try
        {
            var profile = _data.Profiles.FirstOrDefault(p => p.Id == id) ?? throw new InvalidDataException("Profil nicht gefunden.");
            WriteProfileMetadata(profile);
            var directory = GetProfileDirectory(id);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(archivePath))!);
            if (File.Exists(archivePath)) File.Delete(archivePath);
            ZipFile.CreateFromDirectory(directory, archivePath, CompressionLevel.Optimal, false);
            return true;
        }
        catch (Exception ex) { LastError = ex.Message; _logger.Error($"Profil {id} konnte nicht exportiert werden.", ex); return false; }
    }

    public ProfileInfo? ImportProfile(string archivePath, string? requestedName = null)
    {
        LastError = null;
        var staging = Path.Combine(Path.GetTempPath(), "DinoDesktopCompanionImport", Guid.NewGuid().ToString("N"));
        try
        {
            if (!File.Exists(archivePath)) throw new FileNotFoundException("Die Importdatei wurde nicht gefunden.");
            if (ActiveProfile is not null) CreateBackup(ActiveProfile.Id, "vor-import");
            Directory.CreateDirectory(staging);
            ExtractValidatedArchive(archivePath, staging);
            var metadataPath = Path.Combine(staging, "profile.json");
            if (!File.Exists(metadataPath)) throw new InvalidDataException("profile.json fehlt im Dino-Spielstand.");
            foreach (var jsonPath in Directory.EnumerateFiles(staging, "*.json", SearchOption.AllDirectories))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
                if (document.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                    throw new InvalidDataException($"{Path.GetFileName(jsonPath)} besitzt keine gültige JSON-Struktur.");
            }
            var imported = JsonSerializer.Deserialize<ProfileInfo>(File.ReadAllText(metadataPath), JsonOptions)
                ?? throw new InvalidDataException("profile.json ist leer oder beschädigt.");
            NormalizeProfileFields(imported);
            if (!IsValidProfile(imported)) throw new InvalidDataException("Der Dino-Spielstand enthält ungültige Pflichtdaten.");
            if (imported.SaveVersion > CurrentSaveVersion) throw new InvalidDataException($"SaveVersion {imported.SaveVersion} wird nicht unterstützt.");

            imported.Id = Guid.NewGuid().ToString("N");
            imported.ProfileName = MakeUniqueName(string.IsNullOrWhiteSpace(requestedName) ? imported.ProfileName : requestedName.Trim());
            imported.LastPlayed = DateTime.Now;
            imported.SaveVersion = CurrentSaveVersion;
            imported.ProfileColor = NormalizeProfileColor(imported.ProfileColor);
            var target = GetProfileDirectory(imported.Id);
            Directory.CreateDirectory(_profilesDirectory);
            Directory.Move(staging, target);
            WriteProfileMetadata(imported);
            _data.Profiles.Add(imported);
            SaveIndex();
            return imported;
        }
        catch (Exception ex) { LastError = ex.Message; _logger.Error("Profilimport fehlgeschlagen.", ex); return null; }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }

    public void UpdateLastPlayed()
    {
        if (ActiveProfile is not { } profile) return;
        profile.LastPlayed = DateTime.Now;
        WriteProfileMetadata(profile);
        SaveIndex();
    }

    public void UpdateDinoState(DinoState state, DateTime? sleepStartTime = null)
    {
        if (ActiveProfile is not { } profile) return;
        if (profile.State == state && (state != DinoState.Sleeping || profile.SleepStartTime.HasValue)) return;
        profile.State = state;
        profile.SleepStartTime = state == DinoState.Sleeping ? sleepStartTime ?? DateTime.Now : null;
        WriteProfileMetadata(profile);
        SaveIndex();
    }

    public string GetSuggestedExportName(ProfileInfo profile)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(profile.ProfileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return $"{safe}_{DateTime.Now:yyyy-MM-dd}.dino";
    }

    public ProfileSummary GetSummary(ProfileInfo profile)
    {
        var level = 1;
        var skinId = "standard";
        var skinName = "Grüner Dino";
        var skinColor = "#789E5B";
        try
        {
            var progressPath = Path.Combine(GetProfileDirectory(profile.Id), "progress.json");
            if (File.Exists(progressPath))
            {
                using var progress = JsonDocument.Parse(File.ReadAllText(progressPath));
                if (progress.RootElement.TryGetProperty("Level", out var value) && value.TryGetInt32(out var parsed)) level = parsed;
            }
            var collectionsPath = Path.Combine(GetProfileDirectory(profile.Id), "collections.json");
            if (File.Exists(collectionsPath))
            {
                using var collections = JsonDocument.Parse(File.ReadAllText(collectionsPath));
                if (collections.RootElement.TryGetProperty("EquippedSkinId", out var value) && value.ValueKind == JsonValueKind.String) skinId = value.GetString() ?? skinId;
            }
            var skinsPath = Path.Combine(AppContext.BaseDirectory, "GameData", "skins.json");
            if (File.Exists(skinsPath))
            {
                using var skins = JsonDocument.Parse(File.ReadAllText(skinsPath));
                foreach (var skin in skins.RootElement.EnumerateArray())
                {
                    if (!skin.TryGetProperty("Id", out var id) || !string.Equals(id.GetString(), skinId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (skin.TryGetProperty("Name", out var name)) skinName = name.GetString() ?? skinName;
                    if (skin.TryGetProperty("BaseColor", out var color)) skinColor = color.GetString() ?? skinColor;
                    break;
                }
            }
        }
        catch (Exception ex) { _logger.Error($"Profilvorschau für {profile.Id} konnte nicht gelesen werden.", ex); }
        return new ProfileSummary(level, skinId, skinName, skinColor);
    }

    private void SaveIndex() => AtomicWriteJson(_profilesJsonPath, _data);

    private void WriteProfileMetadata(ProfileInfo profile)
    {
        profile.SaveVersion = CurrentSaveVersion;
        AtomicWriteJson(Path.Combine(GetProfileDirectory(profile.Id), "profile.json"), profile);
    }

    private void AtomicWriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        var json = JsonSerializer.Serialize(value, JsonOptions);
        File.WriteAllText(temporary, json);
        _ = JsonSerializer.Deserialize<T>(File.ReadAllText(temporary), JsonOptions)
            ?? throw new InvalidDataException($"Validierung von {Path.GetFileName(path)} fehlgeschlagen.");
        File.Move(temporary, path, true);
    }

    private void EnsureDailyBackup(string id)
    {
        var directory = Path.Combine(_backupsDirectory, id);
        if (Directory.Exists(directory) && Directory.EnumerateFiles(directory, $"{DateTime.Now:yyyy-MM-dd}_*.dino").Any()) return;
        CreateBackup(id, "taeglich");
    }

    private void CreateBackup(string id, string reason)
    {
        var source = GetProfileDirectory(id);
        if (!Directory.Exists(source) || !Directory.EnumerateFiles(source).Any()) return;
        var directory = Path.Combine(_backupsDirectory, id);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{DateTime.Now:yyyy-MM-dd_HHmmss_fff}_{reason}.dino");
        ZipFile.CreateFromDirectory(source, path, CompressionLevel.Fastest, false);
        foreach (var old in Directory.EnumerateFiles(directory, "*.dino").OrderByDescending(File.GetLastWriteTimeUtc).Skip(5)) File.Delete(old);
    }

    private void BackupAllProfiles(string reason)
    {
        foreach (var profile in _data.Profiles.Where(p => IsValidId(p.Id))) CreateBackup(profile.Id, reason);
    }

    private static void ExtractValidatedArchive(string archivePath, string targetDirectory)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count == 0 || archive.Entries.Count > 500) throw new InvalidDataException("Das Archiv ist leer oder ungewöhnlich groß.");
        if (archive.Entries.Sum(entry => entry.Length) > 50 * 1024 * 1024) throw new InvalidDataException("Der Dino-Spielstand ist zu groß.");
        var root = Path.GetFullPath(targetDirectory) + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries)
        {
            var destination = Path.GetFullPath(Path.Combine(targetDirectory, entry.FullName));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Das Archiv enthält einen ungültigen Pfad.");
            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, true);
        }
    }

    private string MakeUniqueName(string desired)
    {
        desired = RequireName(desired, "Profilname");
        if (_data.Profiles.All(p => !p.ProfileName.Equals(desired, StringComparison.CurrentCultureIgnoreCase))) return desired;
        var number = 2;
        while (_data.Profiles.Any(p => p.ProfileName.Equals($"{desired} ({number})", StringComparison.CurrentCultureIgnoreCase))) number++;
        return $"{desired} ({number})";
    }

    private static bool IsValidProfile(ProfileInfo profile) => IsValidId(profile.Id)
        && !string.IsNullOrWhiteSpace(profile.ProfileName)
        && !string.IsNullOrWhiteSpace(profile.DinoName)
        && profile.SaveVersion is > 0 and <= CurrentSaveVersion
        && IsValidBirthday(profile.BirthdayDay, profile.BirthdayMonth);

    private static bool IsValidId(string id) => Guid.TryParseExact(id, "N", out _) || id == "default";
    private static string NormalizeName(string? value, string fallback)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized[..Math.Min(normalized.Length, 60)];
    }

    private static void NormalizeProfileFields(ProfileInfo profile)
    {
        profile.ProfileName = NormalizeName(profile.ProfileName, "Mein Dino");
        profile.DinoName = NormalizeName(profile.DinoName, "Dino");
        profile.Note = NormalizeNote(profile.Note);
        if (!IsValidBirthday(profile.BirthdayDay, profile.BirthdayMonth))
        {
            profile.BirthdayDay = null;
            profile.BirthdayMonth = null;
        }
        profile.ProfileColor = NormalizeProfileColor(profile.ProfileColor);
        profile.EventProgress ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        profile.UnlockedTitles ??= [];
        if (profile.SaveVersion <= 0) profile.SaveVersion = CurrentSaveVersion;
    }

    public static string NormalizeProfileColor(string? color) => color is not null && AllowedProfileColors.Contains(color)
        ? AllowedProfileColors.First(value => value.Equals(color, StringComparison.OrdinalIgnoreCase))
        : ProfileInfo.DefaultProfileColor;
    private static string NormalizeNote(string? value)
    {
        var normalized = value?.Trim() ?? "";
        return normalized[..Math.Min(normalized.Length, 500)];
    }
    private static string RequireName(string value, string label) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 60
        ? value.Trim()
        : throw new ArgumentException($"{label} muss 1 bis 60 Zeichen lang sein.");
    private static bool IsValidBirthday(int? day, int? month) => day is null && month is null || day is >= 1 and <= 31 && month is >= 1 and <= 12 && day <= DateTime.DaysInMonth(2000, month.Value);
    private static void ValidateBirthday(int? day, int? month)
    {
        if (!IsValidBirthday(day, month)) throw new ArgumentException("Geburtstag muss aus einem gültigen Tag und Monat bestehen.");
    }
}

public sealed record ProfileSummary(int Level, string EquippedSkinId, string SkinName, string SkinColor);
