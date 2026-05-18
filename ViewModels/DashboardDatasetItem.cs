using ReLPC.Models;

namespace ReLPC.ViewModels;

public sealed class DashboardDatasetItem
{
    public required DatasetRecord Dataset { get; init; }

    public string Title => Dataset.DashboardTitle;

    public string ModifiedDateDisplay => Dataset.ModifiedDateDisplay;

    public string OwnerCaption { get; init; } = "";
}
