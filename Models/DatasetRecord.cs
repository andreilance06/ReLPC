using System;
using System.Collections.Generic;
using LiteDB;

namespace ReLPC.Models;

public class DatasetRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public List<DatasetPointRecord> Points { get; set; } = [];
    public string Equation { get; set; } = "No Calculation Yet";
    public string Coefficient { get; set; } = "-";
    public string IntermediateComputations { get; set; } = "-";
    public List<PredictionRecord> Predictions { get; set; } = [];

    [BsonIgnore]
    public string DashboardTitle =>
        string.IsNullOrWhiteSpace(Name) ? "UNTITLED DATASET" : Name.ToUpperInvariant();

    [BsonIgnore]
    public string ModifiedDateDisplay => UpdatedAt.ToString("MM/dd/yy");

    public override string ToString() => $"{Name} - {UpdatedAt:g}";
}

public class DatasetPointRecord
{
    public string X { get; set; } = string.Empty;
    public string Y { get; set; } = string.Empty;
}

public class PredictionRecord
{
    public string X { get; set; } = string.Empty;
    public string YPred { get; set; } = string.Empty;
    public string Y { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
