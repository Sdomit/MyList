using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MyList.Helpers;
using MyList.Models;

namespace MyList.Services;

public sealed class StorageService
{
    public const int CurrentSchemaVersion = 6;
    private sealed record LoadedImportData(AppData? Data, string? AssetDirectory, string? TempRoot);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _options;
    private readonly LogService _log = LogService.Instance;
    private readonly ClipboardAssetService _clipboardAssetService;
    public string LastImportError { get; private set; } = string.Empty;

    public StorageService(ClipboardAssetService clipboardAssetService)
    {
        _clipboardAssetService = clipboardAssetService;
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _options.Converters.Add(new JsonStringEnumConverter());
    }

    public string AppDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MyList");

    public string DataFilePath => Path.Combine(AppDataDirectory, "data.json");

    public string BackupDirectory => Path.Combine(AppDataDirectory, "backups");

    private static string PackageDataFileName => "data.json";

    public AppData Load()
    {
        try
        {
            Directory.CreateDirectory(AppDataDirectory);
            if (!File.Exists(DataFilePath))
            {
                var fresh = CreateDefaultData();
                WriteAtomic(DataFilePath, JsonSerializer.Serialize(fresh, _options));
                return fresh;
            }

            var json = File.ReadAllText(DataFilePath);
            var data = JsonSerializer.Deserialize<AppData>(json, _options) ?? CreateDefaultData();
            EnsureDefaults(data);
            var hadSchemaChanges = EnsureSchemaVersion(data, out var migrationLogs);
            if (migrationLogs.Count > 0)
            {
                AppendMigrationLogs(data, migrationLogs);
                foreach (var logEntry in migrationLogs)
                {
                    _log.Log(logEntry);
                }
            }

            if (NormalizeAndMergeDuplicateItems(data, out var duplicateCount, out var remappedCount) || hadSchemaChanges)
            {
                WriteAtomic(DataFilePath, JsonSerializer.Serialize(data, _options));
                if (duplicateCount > 0 || remappedCount > 0)
                {
                    _log.Log($"Deduplicated items on load. Removed duplicates: {duplicateCount}, remapped links: {remappedCount}.");
                }
            }

            _clipboardAssetService.CleanupUnusedAssets(data.Items);

            return data;
        }
        catch (Exception ex)
        {
            _log.Log(ex, "Failed to load data.json, creating defaults.");
            return CreateDefaultData();
        }
    }

    public async Task SaveAsync(AppData data)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(AppDataDirectory);
            var json = JsonSerializer.Serialize(data, _options);
            await WriteAtomicAsync(DataFilePath, json).ConfigureAwait(false);
            await EnsureBackupAsync(data).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Log(ex, "Failed to save data.json.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task EnsureBackupAsync(AppData data)
    {
        if ((DateTime.UtcNow - data.AppSettings.LastBackupDate).TotalDays < 7)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(BackupDirectory);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(BackupDirectory, $"backup_{timestamp}.zip");
            await WritePackageAsync(data, backupPath).ConfigureAwait(false);
            data.AppSettings.LastBackupDate = DateTime.UtcNow;
            await WriteAtomicAsync(DataFilePath, JsonSerializer.Serialize(data, _options)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Log(ex, "Failed to create backup.");
        }
    }

    public async Task ExportCollectionsAsync(AppData data, string filePath)
    {
        EnsureDefaults(data);
        data.SchemaVersion = CurrentSchemaVersion;

        // Snapshot a deep clone on the calling (UI) thread before the first await,
        // so package serialization cannot race with concurrent UI mutations of the
        // live collections.
        var clone = CreateExportClone(data);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await WritePackageAsync(clone, filePath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Log(ex, "Failed to export collections.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AppData?> ImportCollectionsAsync(string filePath)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        LoadedImportData? loaded = null;
        try
        {
            LastImportError = string.Empty;
            loaded = await LoadPackageOrJsonAsync(filePath).ConfigureAwait(false);
            return PrepareImportedData(loaded, "Import");
        }
        catch (Exception ex)
        {
            _log.Log(ex, "Failed to import collections.");
            LastImportError = "Import failed due to invalid or corrupted file content.";
            return null;
        }
        finally
        {
            CleanupImportTempDirectory(loaded?.TempRoot);
            _gate.Release();
        }
    }

    public async Task<AppData?> RestoreLatestBackupAsync()
    {
        LoadedImportData? loaded = null;
        try
        {
            if (!Directory.Exists(BackupDirectory))
            {
                return null;
            }

            var file = Directory.GetFiles(BackupDirectory, "backup_*.*")
                .Where(path => path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f)
                .FirstOrDefault();

            if (file is null)
            {
                return null;
            }

            loaded = await LoadPackageOrJsonAsync(file).ConfigureAwait(false);
            return PrepareImportedData(loaded, "Backup restore");
        }
        catch (Exception ex)
        {
            _log.Log(ex, "Failed to restore backup.");
            return null;
        }
        finally
        {
            CleanupImportTempDirectory(loaded?.TempRoot);
        }
    }

    private static AppData CreateDefaultData()
    {
        var data = new AppData();
        data.SchemaVersion = CurrentSchemaVersion;
        data.Collections.Add(new CollectionModel { Name = "Main" });
        return data;
    }

    private AppData CreateExportClone(AppData data)
    {
        // Deep clone via JSON round-trip so the exported snapshot shares no mutable
        // references with the live data. Runs synchronously on the caller's thread.
        var json = JsonSerializer.Serialize(data, _options);
        var clone = JsonSerializer.Deserialize<AppData>(json, _options) ?? new AppData();
        clone.SchemaVersion = CurrentSchemaVersion;
        return clone;
    }

    private async Task WritePackageAsync(AppData data, string filePath)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"MyListPackage_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempRoot);
            var jsonPath = Path.Combine(tempRoot, PackageDataFileName);
            var json = JsonSerializer.Serialize(data, _options);
            await File.WriteAllTextAsync(jsonPath, json).ConfigureAwait(false);

            var assetDirectory = Path.Combine(tempRoot, ClipboardAssetService.AssetDirectoryName);
            _clipboardAssetService.ExportReferencedAssets(data.Items, assetDirectory);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            ZipFile.CreateFromDirectory(tempRoot, filePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch (Exception ex)
                {
                    _log.Log(ex, "Failed to clean package temp directory.");
                }
            }
        }
    }

    private static void WriteAtomic(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = targetPath + ".tmp";
        File.WriteAllText(tempPath, content);
        ReplaceFile(tempPath, targetPath);
    }

    private static async Task WriteAtomicAsync(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = targetPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, content).ConfigureAwait(false);
        ReplaceFile(tempPath, targetPath);
    }

    private static void ReplaceFile(string tempPath, string targetPath)
    {
        try
        {
            if (File.Exists(targetPath))
            {
                // Atomic replace when possible (same volume).
                File.Replace(tempPath, targetPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, targetPath);
            }
        }
        catch
        {
            // Fallback for providers that don't support File.Replace semantics
            // (cross-volume, network shares). Move-with-overwrite avoids the
            // delete-then-move window that left the target missing on crash
            // or for concurrent readers.
            File.Move(tempPath, targetPath, overwrite: true);
        }
    }

    private async Task<LoadedImportData?> LoadPackageOrJsonAsync(string filePath)
    {
        if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            return new LoadedImportData(JsonSerializer.Deserialize<AppData>(json, _options), null, null);
        }

        if (!filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            LastImportError = "Unsupported import format.";
            return null;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"MyListImport_{Guid.NewGuid():N}");
        try
        {
            ZipFile.ExtractToDirectory(filePath, tempRoot, overwriteFiles: true);
            var jsonPath = Path.Combine(tempRoot, PackageDataFileName);
            if (!File.Exists(jsonPath))
            {
                LastImportError = "Import package is missing data.json.";
                return null;
            }

            var json = await File.ReadAllTextAsync(jsonPath).ConfigureAwait(false);
            var imported = JsonSerializer.Deserialize<AppData>(json, _options);
            var assetDirectory = Path.Combine(tempRoot, ClipboardAssetService.AssetDirectoryName);
            return new LoadedImportData(imported, assetDirectory, tempRoot);
        }
        catch
        {
            CleanupImportTempDirectory(tempRoot);
            throw;
        }
    }

    private AppData? PrepareImportedData(LoadedImportData? loaded, string operationName)
    {
        var imported = loaded?.Data;
        if (imported is null)
        {
            LastImportError = $"{operationName} file is empty or invalid.";
            return null;
        }

        EnsureDefaults(imported);
        if (imported.SchemaVersion > CurrentSchemaVersion)
        {
            LastImportError = $"Unsupported schema version {imported.SchemaVersion}. This app supports up to {CurrentSchemaVersion}.";
            return null;
        }

        var migrated = EnsureSchemaVersion(imported, out var migrationLogs);
        if (migrationLogs.Count > 0)
        {
            AppendMigrationLogs(imported, migrationLogs);
            foreach (var logEntry in migrationLogs)
            {
                _log.Log($"{operationName} migration: {logEntry}");
            }
        }

        if (NormalizeAndMergeDuplicateItems(imported, out var duplicateCount, out var remappedCount))
        {
            _log.Log($"{operationName} deduped: removed {duplicateCount}, remapped {remappedCount}.");
        }

        if (migrated)
        {
            _log.Log($"{operationName} schema migrated successfully.");
        }

        if (!string.IsNullOrWhiteSpace(loaded?.AssetDirectory))
        {
            _clipboardAssetService.ImportReferencedAssets(imported.Items, loaded.AssetDirectory!);
        }

        return imported;
    }

    private void CleanupImportTempDirectory(string? tempRoot)
    {
        if (string.IsNullOrWhiteSpace(tempRoot) || !Directory.Exists(tempRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch (Exception ex)
        {
            _log.Log(ex, "Failed to clean import temp directory.");
        }
    }

    private static void EnsureCollectionEntries(CollectionModel collection)
    {
        collection.ItemIds ??= new System.Collections.ObjectModel.ObservableCollection<Guid>();
        collection.Entries ??= new System.Collections.ObjectModel.ObservableCollection<CollectionEntryModel>();

        if (collection.Entries.Count == 0 && collection.ItemIds.Count > 0)
        {
            foreach (var itemId in collection.ItemIds.Distinct())
            {
                collection.Entries.Add(new CollectionEntryModel
                {
                    Kind = CollectionEntryKind.Item,
                    ItemId = itemId
                });
            }
        }

        var itemIds = collection.Entries
            .Where(entry => entry.Kind == CollectionEntryKind.Item && entry.ItemId != Guid.Empty)
            .Select(entry => entry.ItemId)
            .Distinct()
            .ToList();

        if (!itemIds.SequenceEqual(collection.ItemIds))
        {
            collection.ItemIds.Clear();
            foreach (var itemId in itemIds)
            {
                collection.ItemIds.Add(itemId);
            }
        }
    }

    private void EnsureDefaults(AppData data)
    {
        if (data.SchemaVersion <= 0)
        {
            data.SchemaVersion = 1;
        }

        data.AppSettings ??= new AppSettings();
        data.AppSettings.GlobalHotkey ??= new HotkeySettings();
        data.AppSettings.HiddenQuickMenuViews ??= new List<string>();
        data.Items ??= new System.Collections.ObjectModel.ObservableCollection<ItemModel>();
        data.Collections ??= new System.Collections.ObjectModel.ObservableCollection<CollectionModel>();
        data.ClipboardImportHistory ??= new System.Collections.ObjectModel.ObservableCollection<ClipboardImportHistoryEntry>();
        data.MigrationLogs ??= new System.Collections.ObjectModel.ObservableCollection<string>();

        if (data.AppSettings.GlobalHotkey.Modifiers == (HotkeyModifiers.Control | HotkeyModifiers.Shift)
            && data.AppSettings.GlobalHotkey.Key == System.Windows.Input.Key.Space)
        {
            data.AppSettings.GlobalHotkey = new HotkeySettings();
        }

        if (double.IsNaN(data.AppSettings.ItemScale) || double.IsInfinity(data.AppSettings.ItemScale))
        {
            data.AppSettings.ItemScale = 1.0;
        }
        else
        {
            data.AppSettings.ItemScale = Math.Clamp(data.AppSettings.ItemScale, 0.75, 1.5);
        }

        if (!Enum.IsDefined(typeof(ViewMode), data.AppSettings.ViewMode))
        {
            data.AppSettings.ViewMode = ViewMode.Grid;
        }

        data.AppSettings.MiniLauncherItemLimit = Math.Clamp(data.AppSettings.MiniLauncherItemLimit, 3, 7);

        if (!Enum.IsDefined(typeof(UiSkin), data.AppSettings.Skin))
        {
            data.AppSettings.Skin = UiSkin.Windows11;
        }

        if (data.Collections.Count == 0)
        {
            data.Collections.Add(new CollectionModel { Name = "Main" });
        }

        foreach (var item in data.Items)
        {
            item.Tags ??= new System.Collections.ObjectModel.ObservableCollection<string>();
            item.LaunchProfile ??= new ItemLaunchProfile();
            item.ClipboardContent ??= string.Empty;
            item.ClipboardImageAssetPath = _clipboardAssetService.NormalizeAssetReference(item.ClipboardImageAssetPath);
            item.ClipboardImagePixelWidth = Math.Max(0, item.ClipboardImagePixelWidth);
            item.ClipboardImagePixelHeight = Math.Max(0, item.ClipboardImagePixelHeight);
            item.ActionContent ??= string.Empty;
            if (!Enum.IsDefined(typeof(ActionKind), item.ActionKind))
            {
                item.ActionKind = ActionKind.None;
            }
            item.MtabMemberIds ??= new System.Collections.ObjectModel.ObservableCollection<Guid>();
            item.MtabPaths ??= new System.Collections.ObjectModel.ObservableCollection<string>();
            if (!Enum.IsDefined(typeof(ItemHealthState), item.HealthState))
            {
                item.HealthState = ItemHealthState.Healthy;
            }
        }

        foreach (var collection in data.Collections)
        {
            collection.ItemIds ??= new System.Collections.ObjectModel.ObservableCollection<Guid>();
            collection.Entries ??= new System.Collections.ObjectModel.ObservableCollection<CollectionEntryModel>();
            EnsureCollectionEntries(collection);
        }
    }

    private static bool EnsureSchemaVersion(AppData data, out List<string> migrationLogs)
    {
        migrationLogs = new List<string>();
        var version = data.SchemaVersion <= 0 ? 1 : data.SchemaVersion;
        var changed = false;

        if (version > CurrentSchemaVersion)
        {
            migrationLogs.Add($"Schema {version} is newer than supported {CurrentSchemaVersion}. Keeping data as-is.");
            return false;
        }

        while (version < CurrentSchemaVersion)
        {
            switch (version)
            {
                case 1:
                    MigrateV1ToV2(data, migrationLogs);
                    version = 2;
                    changed = true;
                    break;
                case 2:
                    MigrateV2ToV3(data, migrationLogs);
                    version = 3;
                    changed = true;
                    break;
                case 3:
                    MigrateV3ToV4(data, migrationLogs);
                    version = 4;
                    changed = true;
                    break;
                case 4:
                    MigrateV4ToV5(data, migrationLogs);
                    version = 5;
                    changed = true;
                    break;
                case 5:
                    MigrateV5ToV6(data, migrationLogs);
                    version = 6;
                    changed = true;
                    break;
                default:
                    migrationLogs.Add($"No migration path from schema {version}; forcing schema {CurrentSchemaVersion} defaults.");
                    version = CurrentSchemaVersion;
                    changed = true;
                    break;
            }
        }

        if (data.SchemaVersion != version)
        {
            data.SchemaVersion = version;
            changed = true;
        }

        return changed;
    }

    private static void MigrateV1ToV2(AppData data, ICollection<string> logs)
    {
        foreach (var item in data.Items)
        {
            item.LaunchProfile ??= new ItemLaunchProfile();
            if (item.HealthState == default && item.IsOffline)
            {
                item.HealthState = ItemHealthState.Offline;
            }
            else if (item.HealthState == default)
            {
                item.HealthState = ItemHealthState.Healthy;
            }
        }

        logs.Add("Migrated schema v1->v2: added launch profile and health state defaults.");
    }

    private static void MigrateV2ToV3(AppData data, ICollection<string> logs)
    {
        data.ClipboardImportHistory ??= new System.Collections.ObjectModel.ObservableCollection<ClipboardImportHistoryEntry>();
        data.MigrationLogs ??= new System.Collections.ObjectModel.ObservableCollection<string>();
        if (!Enum.IsDefined(typeof(UiDensity), data.AppSettings.UiDensity))
        {
            data.AppSettings.UiDensity = UiDensity.Comfortable;
        }

        logs.Add("Migrated schema v2->v3: added clipboard history, migration logs, and UI density defaults.");
    }

    private static void MigrateV3ToV4(AppData data, ICollection<string> logs)
    {
        foreach (var item in data.Items)
        {
            item.MtabMemberIds ??= new System.Collections.ObjectModel.ObservableCollection<Guid>();
            item.MtabPaths ??= new System.Collections.ObjectModel.ObservableCollection<string>();
        }

        logs.Add("Migrated schema v3->v4: added Mtab defaults.");
    }

    private static void MigrateV4ToV5(AppData data, ICollection<string> logs)
    {
        foreach (var item in data.Items)
        {
            item.ClipboardImageAssetPath ??= string.Empty;
            item.ClipboardImagePixelWidth = Math.Max(0, item.ClipboardImagePixelWidth);
            item.ClipboardImagePixelHeight = Math.Max(0, item.ClipboardImagePixelHeight);
        }

        logs.Add("Migrated schema v4->v5: added clipboard image defaults.");
    }

    private static void MigrateV5ToV6(AppData data, ICollection<string> logs)
    {
        foreach (var item in data.Items)
        {
            item.ActionContent ??= string.Empty;
            if (!Enum.IsDefined(typeof(ActionKind), item.ActionKind))
            {
                item.ActionKind = ActionKind.None;
            }
        }

        foreach (var collection in data.Collections)
        {
            collection.Entries ??= new System.Collections.ObjectModel.ObservableCollection<CollectionEntryModel>();
            if (collection.Entries.Count == 0)
            {
                foreach (var itemId in collection.ItemIds.Distinct())
                {
                    collection.Entries.Add(new CollectionEntryModel
                    {
                        Kind = CollectionEntryKind.Item,
                        ItemId = itemId
                    });
                }
            }
        }

        logs.Add("Migrated schema v5->v6: added internal action defaults and collection entries.");
    }

    private static void AppendMigrationLogs(AppData data, IEnumerable<string> logs)
    {
        foreach (var log in logs)
        {
            data.MigrationLogs.Add($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {log}");
        }

        while (data.MigrationLogs.Count > 200)
        {
            data.MigrationLogs.RemoveAt(0);
        }
    }

    private static bool NormalizeAndMergeDuplicateItems(AppData data, out int duplicateCount, out int remappedCount)
    {
        duplicateCount = 0;
        remappedCount = 0;
        var hadNormalizationChanges = false;

        var keyToSurvivor = new Dictionary<string, ItemModel>(StringComparer.OrdinalIgnoreCase);
        var idMap = new Dictionary<Guid, Guid>();
        var survivors = new List<ItemModel>();

        foreach (var item in data.Items)
        {
            if (!PathNormalizationHelper.TryNormalizeUserPath(item.Path, out var normalizedPath, out var pathKey))
            {
                idMap[item.Id] = item.Id;
                survivors.Add(item);
                continue;
            }

            if (!string.Equals(item.Path, normalizedPath, StringComparison.Ordinal))
            {
                item.Path = normalizedPath;
                hadNormalizationChanges = true;
            }

            if (!keyToSurvivor.TryGetValue(pathKey, out var survivor))
            {
                keyToSurvivor[pathKey] = item;
                idMap[item.Id] = item.Id;
                survivors.Add(item);
                continue;
            }

            duplicateCount++;
            idMap[item.Id] = survivor.Id;
            MergeItemMetadata(survivor, item);
        }

        if (duplicateCount > 0)
        {
            data.Items.Clear();
            foreach (var survivor in survivors)
            {
                data.Items.Add(survivor);
            }
        }

        var existingIds = data.Items.Select(existingItem => existingItem.Id).ToHashSet();

        foreach (var collection in data.Collections)
        {
            EnsureCollectionEntries(collection);

            var seen = new HashSet<Guid>();
            var updatedEntries = new System.Collections.ObjectModel.ObservableCollection<CollectionEntryModel>();

            foreach (var entry in collection.Entries)
            {
                if (entry.Kind == CollectionEntryKind.Separator)
                {
                    updatedEntries.Add(new CollectionEntryModel
                    {
                        Id = entry.Id,
                        Kind = CollectionEntryKind.Separator
                    });
                    continue;
                }

                var mappedId = idMap.TryGetValue(entry.ItemId, out var survivorId) ? survivorId : entry.ItemId;
                if (!existingIds.Contains(mappedId))
                {
                    remappedCount++;
                    continue;
                }

                if (!seen.Add(mappedId))
                {
                    remappedCount++;
                    continue;
                }

                if (mappedId != entry.ItemId)
                {
                    remappedCount++;
                }

                updatedEntries.Add(new CollectionEntryModel
                {
                    Id = entry.Id,
                    Kind = CollectionEntryKind.Item,
                    ItemId = mappedId
                });
            }

            if (updatedEntries.Count != collection.Entries.Count ||
                updatedEntries.Zip(collection.Entries, (left, right) => left.Kind == right.Kind && left.ItemId == right.ItemId).Any(match => !match))
            {
                collection.Entries = updatedEntries;
            }

            collection.ItemIds = new System.Collections.ObjectModel.ObservableCollection<Guid>(
                collection.Entries
                    .Where(entry => entry.Kind == CollectionEntryKind.Item && entry.ItemId != Guid.Empty)
                    .Select(entry => entry.ItemId));
        }

        foreach (var item in data.Items.Where(i => i.IsMtab))
        {
            item.MtabMemberIds ??= new System.Collections.ObjectModel.ObservableCollection<Guid>();
            item.MtabPaths ??= new System.Collections.ObjectModel.ObservableCollection<string>();
            var normalizedMtabPaths = new System.Collections.ObjectModel.ObservableCollection<string>();
            var seenPathKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mtabPath in item.MtabPaths)
            {
                if (!PathNormalizationHelper.TryNormalizeUserPath(mtabPath, out var normalizedPath, out var pathKey))
                {
                    remappedCount++;
                    continue;
                }

                if (!seenPathKeys.Add(pathKey))
                {
                    remappedCount++;
                    continue;
                }

                normalizedMtabPaths.Add(normalizedPath);
            }

            if (normalizedMtabPaths.Count != item.MtabPaths.Count || !normalizedMtabPaths.SequenceEqual(item.MtabPaths))
            {
                item.MtabPaths = normalizedMtabPaths;
            }
            var seenMembers = new HashSet<Guid>();
            var updatedMembers = new System.Collections.ObjectModel.ObservableCollection<Guid>();
            foreach (var memberId in item.MtabMemberIds)
            {
                var mappedId = idMap.TryGetValue(memberId, out var remapped) ? remapped : memberId;
                if (!existingIds.Contains(mappedId))
                {
                    remappedCount++;
                    continue;
                }

                if (mappedId == item.Id)
                {
                    remappedCount++;
                    continue;
                }

                if (!seenMembers.Add(mappedId))
                {
                    remappedCount++;
                    continue;
                }

                if (mappedId != memberId)
                {
                    remappedCount++;
                }

                updatedMembers.Add(mappedId);
            }

            if (updatedMembers.Count != item.MtabMemberIds.Count || !updatedMembers.SequenceEqual(item.MtabMemberIds))
            {
                item.MtabMemberIds = updatedMembers;
            }
        }

        return duplicateCount > 0 || remappedCount > 0 || hadNormalizationChanges;
    }

    private static void MergeItemMetadata(ItemModel survivor, ItemModel duplicate)
    {
        if (duplicate.IsFavorite)
        {
            survivor.IsFavorite = true;
        }

        if (duplicate.CreatedDate < survivor.CreatedDate)
        {
            survivor.CreatedDate = duplicate.CreatedDate;
        }

        if (duplicate.LastOpenedDate > survivor.LastOpenedDate)
        {
            survivor.LastOpenedDate = duplicate.LastOpenedDate;
        }

        if (string.IsNullOrWhiteSpace(survivor.IconPath) && !string.IsNullOrWhiteSpace(duplicate.IconPath))
        {
            survivor.IconPath = duplicate.IconPath;
            survivor.UseSystemIcon = duplicate.UseSystemIcon;
        }

        if (!survivor.IsActionItem && duplicate.IsActionItem)
        {
            survivor.IsActionItem = true;
            survivor.ActionKind = duplicate.ActionKind;
        }

        if (string.IsNullOrWhiteSpace(survivor.ActionContent) && !string.IsNullOrWhiteSpace(duplicate.ActionContent))
        {
            survivor.ActionContent = duplicate.ActionContent;
        }

        survivor.LaunchProfile ??= new ItemLaunchProfile();
        duplicate.LaunchProfile ??= new ItemLaunchProfile();
        if (string.IsNullOrWhiteSpace(survivor.LaunchProfile.Arguments) && !string.IsNullOrWhiteSpace(duplicate.LaunchProfile.Arguments))
        {
            survivor.LaunchProfile.Arguments = duplicate.LaunchProfile.Arguments;
        }

        if (string.IsNullOrWhiteSpace(survivor.LaunchProfile.WorkingDirectory) && !string.IsNullOrWhiteSpace(duplicate.LaunchProfile.WorkingDirectory))
        {
            survivor.LaunchProfile.WorkingDirectory = duplicate.LaunchProfile.WorkingDirectory;
        }

        if (duplicate.LaunchProfile.RunAsAdmin)
        {
            survivor.LaunchProfile.RunAsAdmin = true;
        }

        if (survivor.LaunchProfile.TerminalProfile == TerminalProfile.Default && duplicate.LaunchProfile.TerminalProfile != TerminalProfile.Default)
        {
            survivor.LaunchProfile.TerminalProfile = duplicate.LaunchProfile.TerminalProfile;
        }

        foreach (var tag in duplicate.Tags)
        {
            if (!survivor.Tags.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
            {
                survivor.Tags.Add(tag);
            }
        }

        if (survivor.HealthState == ItemHealthState.Healthy && duplicate.HealthState != ItemHealthState.Healthy)
        {
            survivor.HealthState = duplicate.HealthState;
        }
    }
}
