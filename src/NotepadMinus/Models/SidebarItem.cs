using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace NotepadMinus.Models;

/// <summary>
/// A single row in the left-hand notes sidebar. Represents a saved file on disk,
/// not an open tab. Selecting an item opens (or focuses) the corresponding tab.
/// </summary>
public sealed class SidebarItem : INotifyPropertyChanged
{
    private string _displayTitle = string.Empty;

    public string FullPath { get; }
    public DateTime LastWriteTime { get; }

    public string DisplayTitle
    {
        get => _displayTitle;
        set { _displayTitle = value; OnPropertyChanged(); }
    }

    public string SubText => LastWriteTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public SidebarItem(string fullPath, DateTime lastWriteTimeUtc)
    {
        FullPath = fullPath;
        LastWriteTime = lastWriteTimeUtc;
        _displayTitle = Path.GetFileNameWithoutExtension(fullPath);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
