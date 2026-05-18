using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReLPC.Models;

public partial class Point : ObservableObject
{
    [ObservableProperty]
    public partial double? X { get; set; }

    [ObservableProperty]
    public partial double? Y { get; set; }

    public Point()
    {
    }

    public Point(double? x, double? y)
    {
        X = x;
        Y = y;
    }

    [Display(AutoGenerateField = false)] public bool IsEmpty => X == null && Y == null;
}