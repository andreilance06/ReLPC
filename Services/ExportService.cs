using System;
using System.IO;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReLPC.Models;

namespace ReLPC.Services;

public interface IExportService
{
    byte[] ExportDatasetToPdf(DatasetRecord dataset);
    void ExportDataset(string filePath, DatasetRecord dataset);
}

public sealed class ExportService : IExportService
{
    private const string BodyFontFamily = "Latin Modern Math";
    private const string HeaderFontFamily = "Arial";

    private static readonly object FontLock = new();
    private static bool _fontsRegistered;

    static ExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        EnsureFonts();
    }

    public byte[] ExportDatasetToPdf(DatasetRecord dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        using var buffer = new MemoryStream();
        CreateDocument(dataset).GeneratePdf(buffer);
        return buffer.ToArray();
    }

    public void ExportDataset(string filePath, DatasetRecord dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        CreateDocument(dataset).GeneratePdf(filePath);
    }

    private static void EnsureFonts()
    {
        if (_fontsRegistered)
            return;

        lock (FontLock)
        {
            if (_fontsRegistered)
                return;

            var fontPath = ResolveFontPath();
            using var stream = File.OpenRead(fontPath);
            FontManager.RegisterFont(stream);
            _fontsRegistered = true;
        }
    }

    private static string ResolveFontPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "fonts", "latinmodern-math.otf"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "fonts", "latinmodern-math.otf"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "fonts",
                "latinmodern-math.otf"))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(
            "Could not find latinmodern-math.otf for PDF export.",
            Path.Combine("Assets", "fonts", "latinmodern-math.otf"));
    }

    private static Document CreateDocument(DatasetRecord dataset) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style
                    .FontFamily(BodyFontFamily)
                    .FontColor(ExportTheme.BodyText)
                    .FontSize(16));

                page.Content().AlignCenter().Column(outer =>
                {
                    outer.Item()
                        .Width(520)
                        .Background(ExportTheme.PanelBackground)
                        .Border(1)
                        .BorderColor(ExportTheme.PanelBorder)
                        .PaddingLeft(42)
                        .PaddingRight(42)
                        .PaddingTop(34)
                        .PaddingBottom(34)
                        .Column(panel =>
                        {
                            panel.Spacing(26);

                            panel.Item().Column(header =>
                            {
                                header.Spacing(5);
                                header.Item().Text("ReLPC Analysis Report")
                                    .FontFamily(HeaderFontFamily)
                                    .FontSize(24)
                                    .SemiBold()
                                    .FontColor(ExportTheme.Accent);

                                header.Item().Text($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}")
                                    .FontFamily(BodyFontFamily)
                                    .FontSize(10)
                                    .Italic()
                                    .FontColor(ExportTheme.MutedText);
                            });

                            panel.Item().Column(summary =>
                            {
                                summary.Spacing(5);
                                summary.Item().Text("Dataset Summary")
                                    .FontFamily(HeaderFontFamily)
                                    .FontSize(16)
                                    .SemiBold()
                                    .FontColor(ExportTheme.Accent);

                                summary.Item().Text($"Dataset Name: {dataset.Name ?? "Untitled"}")
                                    .FontFamily(BodyFontFamily)
                                    .FontSize(12)
                                    .FontColor(ExportTheme.BodyText);

                                AddDataPointsToSummary(summary, dataset);
                            });

                            AddResultSection(panel, "EQUATION", dataset.Equation ?? "No Calculation Yet", 20, 54);
                            AddResultSection(panel, "COEFFICIENT", dataset.Coefficient ?? "-", 18, 82);
                            AddResultSection(panel, "INTERMEDIATE COMPUTATIONS",
                                dataset.IntermediateComputations ?? "-", 18, 82);

                            if (dataset.Predictions is { Count: > 0 })
                                AddPredictionsSection(panel, dataset);
                        });
                });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.DefaultTextStyle(style => style
                            .FontFamily(BodyFontFamily)
                            .FontSize(10)
                            .FontColor(ExportTheme.MutedText));
                        text.Span("Page ");
                        text.CurrentPageNumber();
                    });
            });
        });

    private static void AddResultSection(
        ColumnDescriptor panel,
        string label,
        string content,
        float contentFontSize,
        float minContentHeight)
    {
        panel.Item().Column(section =>
        {
            section.Spacing(8);

            section.Item().Text(label)
                .FontFamily(HeaderFontFamily)
                .FontSize(14)
                .Bold()
                .FontColor(ExportTheme.Accent);

            section.Item()
                .MinHeight(minContentHeight)
                .BorderBottom(1)
                .BorderColor(ExportTheme.SectionDivider)
                .PaddingBottom(12)
                .AlignBottom()
                .Text(content)
                .FontFamily(BodyFontFamily)
                .FontSize(contentFontSize)
                .FontColor(ExportTheme.BodyText)
                .LineHeight(1.25f);
        });
    }

    private static void AddDataPointsToSummary(ColumnDescriptor summary, DatasetRecord dataset)
    {
        if (dataset.Points is not { Count: > 0 })
            return;

        summary.Item()
            .PaddingTop(8)
            .Text("Data Points")
            .FontFamily(HeaderFontFamily)
            .FontSize(12)
            .SemiBold()
            .FontColor(ExportTheme.Accent);

        summary.Item().Row(header =>
        {
            header.Spacing(8);
            AddColumnHeaderCell(header, "x", 12);
            AddColumnHeaderCell(header, "y", 12);
        });

        foreach (var point in dataset.Points)
        {
            summary.Item()
                .PaddingBottom(4)
                .Row(row =>
                {
                    row.Spacing(8);
                    AddColumnValueCell(row, point.X, 12);
                    AddColumnValueCell(row, point.Y, 12);
                });
        }
    }

    private static void AddPredictionsSection(ColumnDescriptor panel, DatasetRecord dataset)
    {
        panel.Item().Column(section =>
        {
            section.Spacing(8);

            section.Item().Text("PREDICTION")
                .FontFamily(HeaderFontFamily)
                .FontSize(14)
                .Bold()
                .FontColor(ExportTheme.Accent);

            section.Item().Row(header =>
            {
                header.Spacing(8);
                AddColumnHeaderCell(header, "x", 18);
                AddColumnHeaderCell(header, "y-hat", 18);
                AddColumnHeaderCell(header, "y", 18);
                AddColumnHeaderCell(header, "error", 18);
            });

            foreach (var prediction in dataset.Predictions)
            {
                section.Item()
                    .PaddingBottom(8)
                    .Row(row =>
                    {
                        row.Spacing(8);
                        AddColumnValueCell(row, prediction.X, 16);
                        AddColumnValueCell(row, prediction.YPred, 16);
                        AddColumnValueCell(row, prediction.Y, 16);
                        AddColumnValueCell(row, prediction.Error, 16);
                    });
            }
        });
    }

    private static void AddColumnHeaderCell(RowDescriptor row, string label, float fontSize)
    {
        row.RelativeItem().Text(label)
            .FontFamily(HeaderFontFamily)
            .FontSize(fontSize)
            .Bold()
            .FontColor(ExportTheme.Accent);
    }

    private static void AddColumnValueCell(RowDescriptor row, string? value, float fontSize)
    {
        row.RelativeItem().Text(value ?? string.Empty)
            .FontFamily(BodyFontFamily)
            .FontSize(fontSize)
            .FontColor(ExportTheme.BodyText);
    }

    private static class ExportTheme
    {
        public const string Accent = "#FF4E3B";
        public const string BodyText = "#171717";
        public const string PanelBackground = "#F9FFFFFF";
        public const string PanelBorder = "#CCFF4E3B";
        public const string SectionDivider = "#40FF4E3B";
        public const string MutedText = "#885038";
    }
}
