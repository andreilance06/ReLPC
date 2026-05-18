using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ReLPC.Models;

namespace ReLPC.Services;

public interface IRecentDatasetsService
{
    void RecordOpened(int userId, int datasetId);
    IReadOnlyList<DatasetRecord> GetRecentDatasets(int userId, int maxCount = 20);
}

public sealed class RecentDatasetsService : IRecentDatasetsService
{
    private const int MaxEntriesPerUser = 50;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly string _storePath;
    private readonly IDatabaseService _database;
    private readonly object _sync = new();

    public RecentDatasetsService(IDatabaseService database)
    {
        _database = database;
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ReLPC");
        Directory.CreateDirectory(folder);
        _storePath = Path.Combine(folder, "recent-datasets.json");
    }

    public void RecordOpened(int userId, int datasetId)
    {
        if (userId <= 0 || datasetId <= 0)
            return;

        lock (_sync)
        {
            var file = LoadFile();
            var userRecent = file.Users.FirstOrDefault(user => user.UserId == userId);
            if (userRecent is null)
            {
                userRecent = new UserRecentDatasets { UserId = userId };
                file.Users.Add(userRecent);
            }

            userRecent.Entries.RemoveAll(entry => entry.DatasetId == datasetId);
            userRecent.Entries.Insert(0, new RecentDatasetEntry
            {
                DatasetId = datasetId,
                OpenedAt = DateTime.Now
            });

            if (userRecent.Entries.Count > MaxEntriesPerUser)
                userRecent.Entries.RemoveRange(MaxEntriesPerUser, userRecent.Entries.Count - MaxEntriesPerUser);

            SaveFile(file);
        }
    }

    public IReadOnlyList<DatasetRecord> GetRecentDatasets(int userId, int maxCount = 20)
    {
        if (userId <= 0 || maxCount <= 0)
            return [];

        List<RecentDatasetEntry> entries;
        lock (_sync)
        {
            var file = LoadFile();
            entries = file.Users
                .FirstOrDefault(user => user.UserId == userId)?
                .Entries
                .ToList() ?? [];
        }

        var results = new List<DatasetRecord>();
        foreach (var entry in entries)
        {
            if (results.Count >= maxCount)
                break;

            var dataset = _database.GetDataset(entry.DatasetId);
            if (dataset is null || dataset.UserId != userId)
                continue;

            results.Add(dataset);
        }

        return results;
    }

    private RecentDatasetsFile LoadFile()
    {
        if (!File.Exists(_storePath))
            return new RecentDatasetsFile();

        try
        {
            var json = File.ReadAllText(_storePath);
            var legacy = TryMigrateLegacyFormat(json);
            if (legacy is not null)
            {
                SaveFile(legacy);
                return legacy;
            }

            return JsonSerializer.Deserialize<RecentDatasetsFile>(json, JsonOptions)
                   ?? new RecentDatasetsFile();
        }
        catch
        {
            return new RecentDatasetsFile();
        }
    }

    private static RecentDatasetsFile? TryMigrateLegacyFormat(string json)
    {
        if (!json.TrimStart().StartsWith('{') || json.Contains("\"Users\"", StringComparison.Ordinal))
            return null;

        try
        {
            var legacy = JsonSerializer.Deserialize<Dictionary<string, List<RecentDatasetEntry>>>(json, JsonOptions);
            if (legacy is null || legacy.Count == 0)
                return null;

            var file = new RecentDatasetsFile();
            foreach (var pair in legacy)
            {
                if (!int.TryParse(pair.Key, out var userId))
                    continue;

                file.Users.Add(new UserRecentDatasets
                {
                    UserId = userId,
                    Entries = pair.Value ?? []
                });
            }

            return file.Users.Count > 0 ? file : null;
        }
        catch
        {
            return null;
        }
    }

    private void SaveFile(RecentDatasetsFile file)
    {
        var json = JsonSerializer.Serialize(file, JsonOptions);
        var tempPath = _storePath + ".tmp";
        File.WriteAllText(tempPath, json);
        if (File.Exists(_storePath))
            File.Delete(_storePath);

        File.Move(tempPath, _storePath);
    }
}
