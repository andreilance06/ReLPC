using System;
using System.Collections.Generic;

namespace ReLPC.Models;

public sealed class RecentDatasetEntry
{
    public int DatasetId { get; set; }
    public DateTime OpenedAt { get; set; }
}

public sealed class RecentDatasetsFile
{
    public List<UserRecentDatasets> Users { get; set; } = [];
}

public sealed class UserRecentDatasets
{
    public int UserId { get; set; }
    public List<RecentDatasetEntry> Entries { get; set; } = [];
}
