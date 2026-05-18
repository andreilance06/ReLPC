using System.Collections.Generic;
using LiteDB;
using ReLPC.Models;

namespace ReLPC.Services;

public interface IDatabaseService
{
    void UpsertUser(UserProfile user);
    UserProfile? GetProfileByUsername(string username);
    void AddDataset(int userId, List<Point> dataset);
}

public class LiteDBService : IDatabaseService
{
    private readonly string _dbPath = "profiles.db";

    public LiteDBService()
    {
        using var db = new LiteDatabase(_dbPath);
        var col = db.GetCollection<UserProfile>("users");
        col.EnsureIndex(x => x.Username, unique: true);
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
        db.GetCollection<UserProfile>("users").Upsert(user);
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
}