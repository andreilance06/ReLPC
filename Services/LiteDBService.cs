using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using ReLPC.Models;

namespace ReLPC.Services;

public interface IDatabaseService
{
    void UpsertUser(UserProfile user);
    UserProfile? GetProfileByUsername(string username);
    void AddDataset(int userId, List<Point> dataset);
    DatasetRecord CreateDataset(int userId, string name);
    List<DatasetRecord> GetDatasets(int userId);
    DatasetRecord? GetDataset(int datasetId);
    void UpsertDataset(DatasetRecord dataset);
    void DeleteDataset(int datasetId, int userId);
}

public class LiteDBService : IDatabaseService
{
    private readonly string _dbPath;

    public LiteDBService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ReLPC");
        Directory.CreateDirectory(folder);
        _dbPath = Path.Combine(folder, "profiles.db");

        using var db = new LiteDatabase(_dbPath);
        var col = db.GetCollection<UserProfile>("users");
        col.EnsureIndex(x => x.Username, unique: true);
        db.GetCollection<DatasetRecord>("datasets").EnsureIndex(x => x.UserId);
    }

    public UserProfile? GetProfileByUsername(string username)
    {
        using var db = new LiteDatabase(_dbPath);
        return db.GetCollection<UserProfile>("users")
            .FindOne(u => u.Username == username);
    }

    public void UpsertUser(UserProfile user)
    {
        using var db = new LiteDatabase(_dbPath);
        var col = db.GetCollection<UserProfile>("users");
        if (user.Id == 0)
            col.Insert(user);
        else
            col.Update(user);
    }

    public void AddDataset(int userId, List<Point> entry)
    {
        using var db = new LiteDatabase(_dbPath);
        var col = db.GetCollection<UserProfile>("users");
        var user = col.FindById(userId);

        if (user is null) return;
        user.Datasets.Add(entry);
        col.Update(user);
    }

    public DatasetRecord CreateDataset(int userId, string name)
    {
        using var db = new LiteDatabase(_dbPath);
        var dataset = new DatasetRecord
        {
            UserId = userId,
            Name = name,
            Points =
            [
                new DatasetPointRecord(),
                new DatasetPointRecord(),
                new DatasetPointRecord()
            ]
        };

        db.GetCollection<DatasetRecord>("datasets").Insert(dataset);
        return dataset;
    }

    public List<DatasetRecord> GetDatasets(int userId)
    {
        using var db = new LiteDatabase(_dbPath);
        return db.GetCollection<DatasetRecord>("datasets")
            .Find(dataset => dataset.UserId == userId)
            .OrderByDescending(dataset => dataset.UpdatedAt)
            .ToList();
    }

    public DatasetRecord? GetDataset(int datasetId)
    {
        using var db = new LiteDatabase(_dbPath);
        return db.GetCollection<DatasetRecord>("datasets").FindById(datasetId);
    }

    public void UpsertDataset(DatasetRecord dataset)
    {
        using var db = new LiteDatabase(_dbPath);
        dataset.UpdatedAt = DateTime.Now;
        var col = db.GetCollection<DatasetRecord>("datasets");
        if (dataset.Id == 0)
            col.Insert(dataset);
        else
            col.Update(dataset);
    }

    public void DeleteDataset(int datasetId, int userId)
    {
        using var db = new LiteDatabase(_dbPath);
        var col = db.GetCollection<DatasetRecord>("datasets");
        var dataset = col.FindById(datasetId);
        if (dataset is null || dataset.UserId != userId)
            return;

        col.Delete(datasetId);
    }
}
