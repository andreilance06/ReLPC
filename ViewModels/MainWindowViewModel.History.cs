using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using ReLPC.Models;

namespace ReLPC.ViewModels;

public partial class MainWindowViewModel
{
    // Feature: change-based undo/redo for the data entry table. Each user action produces a
    // group of fine-grained changes (cell edits and row insertions/removals); undo/redo replays
    // the inverse or forward operations rather than restoring whole-table snapshots.
    private abstract record HistoryChange;

    private sealed record CellChange(Point Point, string Property, double? OldValue, double? NewValue) : HistoryChange;

    private sealed record RowInsertChange(int Index, Point Point) : HistoryChange;

    private sealed record RowRemoveChange(int Index, Point Point) : HistoryChange;

    private readonly Stack<List<HistoryChange>> _undoStack = new();
    private readonly Stack<List<HistoryChange>> _redoStack = new();
    private readonly List<HistoryChange> _pendingChanges = [];
    private readonly Dictionary<Point, (double? X, double? Y)> _lastValues = new();
    private bool _suppressHistory;

    private void RecordCellChange(Point point, string property)
    {
        if (_suppressHistory) return;

        _lastValues.TryGetValue(point, out var prev);
        double? oldVal = property == nameof(Point.X) ? prev.X : prev.Y;
        double? newVal = property == nameof(Point.X) ? point.X : point.Y;
        if (Nullable.Equals(oldVal, newVal)) return;

        _pendingChanges.Add(new CellChange(point, property, oldVal, newVal));
    }

    private void RecordRowInsert(int index, Point point)
    {
        if (_suppressHistory) return;
        _pendingChanges.Add(new RowInsertChange(index, point));
    }

    private void RecordRowRemove(int index, Point point)
    {
        if (_suppressHistory) return;
        _pendingChanges.Add(new RowRemoveChange(index, point));
    }

    private void FlushPendingChanges()
    {
        if (_suppressHistory || _pendingChanges.Count == 0) return;

        _undoStack.Push([.._pendingChanges]);
        _pendingChanges.Clear();
        _redoStack.Clear();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void ResetHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _pendingChanges.Clear();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void RebuildLastValues()
    {
        _lastValues.Clear();
        foreach (var p in Inputs)
            _lastValues[p] = (p.X, p.Y);
    }

    private bool CanUndo() => _undoStack.Count > 0;
    private bool CanRedo() => _redoStack.Count > 0;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_undoStack.Count == 0) return;

        var group = _undoStack.Pop();
        _suppressHistory = true;
        try
        {
            for (int i = group.Count - 1; i >= 0; i--)
                ApplyInverse(group[i]);
        }
        finally
        {
            _suppressHistory = false;
        }

        _redoStack.Push(group);
        RebuildLastValues();
        Calculate();
        AutoSaveCurrentDataset();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_redoStack.Count == 0) return;

        var group = _redoStack.Pop();
        _suppressHistory = true;
        try
        {
            foreach (var change in group)
                ApplyForward(change);
        }
        finally
        {
            _suppressHistory = false;
        }

        _undoStack.Push(group);
        RebuildLastValues();
        Calculate();
        AutoSaveCurrentDataset();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void ApplyForward(HistoryChange change)
    {
        switch (change)
        {
            case CellChange cc:
                if (cc.Property == nameof(Point.X)) cc.Point.X = cc.NewValue;
                else cc.Point.Y = cc.NewValue;
                break;
            case RowInsertChange ric:
                Inputs.Insert(Math.Min(ric.Index, Inputs.Count), ric.Point);
                break;
            case RowRemoveChange rrc:
                Inputs.Remove(rrc.Point);
                break;
        }
    }

    private void ApplyInverse(HistoryChange change)
    {
        switch (change)
        {
            case CellChange cc:
                if (cc.Property == nameof(Point.X)) cc.Point.X = cc.OldValue;
                else cc.Point.Y = cc.OldValue;
                break;
            case RowInsertChange ric:
                Inputs.Remove(ric.Point);
                break;
            case RowRemoveChange rrc:
                Inputs.Insert(Math.Min(rrc.Index, Inputs.Count), rrc.Point);
                break;
        }
    }
}
