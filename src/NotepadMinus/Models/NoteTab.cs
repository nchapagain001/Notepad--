using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NotepadMinus.Services;

namespace NotepadMinus.Models;

/// <summary>
/// A single open tab. The note text is stored as a single string in <see cref="Text"/>.
/// The first line is treated as the title (used for the tab label, the sidebar entry,
/// and the auto-derived filename). <see cref="HeaderLine"/> and <see cref="BodyText"/>
/// are kept as computed views over <see cref="Text"/> for backwards compatibility.
/// </summary>
public sealed class NoteTab : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private string? _filePath;
    private DateTime _createdDate = DateTime.Now;
    private bool _isDirty;
    private string _extension = TextFileExtensions.DefaultExtension;
    private bool _isForeign;
    private Encoding _encoding = new UTF8Encoding(false);
    private string _lineEnding = "\r\n";

    /// <summary>The full text of the note as shown in the editor.</summary>
    public string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (_text == value) return;
            _text = value;
            _isDirty = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HeaderLine));
            OnPropertyChanged(nameof(BodyText));
            OnPropertyChanged(nameof(DisplayTitle));
        }
    }

    /// <summary>The first line of <see cref="Text"/>. Setter rewrites the first line in place.</summary>
    public string HeaderLine
    {
        get
        {
            if (string.IsNullOrEmpty(_text)) return string.Empty;
            var idx = _text.IndexOf('\n');
            return idx < 0 ? _text.TrimEnd('\r') : _text[..idx].TrimEnd('\r');
        }
        set
        {
            value ??= string.Empty;
            Text = NoteContent.Join(value, BodyText, "\r\n");
        }
    }

    /// <summary>Everything after the first newline in <see cref="Text"/>. Setter rewrites the body.</summary>
    public string BodyText
    {
        get
        {
            if (string.IsNullOrEmpty(_text)) return string.Empty;
            var idx = _text.IndexOf('\n');
            return idx < 0 ? string.Empty : _text[(idx + 1)..];
        }
        set
        {
            value ??= string.Empty;
            Text = NoteContent.Join(HeaderLine, value, "\r\n");
        }
    }

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath == value) return;
            _filePath = value;
            if (!string.IsNullOrEmpty(value))
            {
                var ext = Path.GetExtension(value);
                if (!string.IsNullOrEmpty(ext)) _extension = ext.ToLowerInvariant();
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayTitle));
        }
    }

    public DateTime CreatedDate
    {
        get => _createdDate;
        set { _createdDate = value; OnPropertyChanged(); }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set { if (_isDirty == value) return; _isDirty = value; OnPropertyChanged(); }
    }

    /// <summary>Extension to use when this tab is saved. Defaults to ".txt" for new untitled notes.</summary>
    public string Extension
    {
        get => _extension;
        set { _extension = string.IsNullOrWhiteSpace(value) ? ".txt" : value.ToLowerInvariant(); OnPropertyChanged(); }
    }

    /// <summary>True when this tab represents a file outside the current sidebar root (v1.5 workspace mode).</summary>
    public bool IsForeign
    {
        get => _isForeign;
        set { if (_isForeign == value) return; _isForeign = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayTitle)); }
    }

    public Encoding Encoding
    {
        get => _encoding;
        set { _encoding = value ?? new UTF8Encoding(false); OnPropertyChanged(); }
    }

    public string LineEnding
    {
        get => _lineEnding;
        set { _lineEnding = string.IsNullOrEmpty(value) ? "\r\n" : value; OnPropertyChanged(); }
    }

    /// <summary>Title displayed in the tab strip and sidebar.</summary>
    public string DisplayTitle
    {
        get
        {
            string baseTitle;
            if (!string.IsNullOrEmpty(_filePath))
                baseTitle = Path.GetFileName(_filePath);
            else
            {
                var first = HeaderLine;
                baseTitle = string.IsNullOrWhiteSpace(first) ? "untitled" : first;
            }
            return _isForeign ? "\u2197 " + baseTitle : baseTitle;
        }
    }

    /// <summary>The full text rendered to disk, with <see cref="LineEnding"/> applied.</summary>
    public string ToFileContent()
    {
        if (string.IsNullOrEmpty(_text)) return string.Empty;
        if (_lineEnding == "\r\n") return _text;
        // WPF TextBox stores \r\n; rewrite to preserve the on-disk line ending.
        return _text.Replace("\r\n", _lineEnding);
    }

    /// <summary>Load text from raw file content, without raising IsDirty. Normalizes line endings to \r\n internally.</summary>
    public void LoadFromFileContent(string raw)
    {
        var normalized = string.IsNullOrEmpty(raw)
            ? string.Empty
            : raw.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        _text = normalized;
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(HeaderLine));
        OnPropertyChanged(nameof(BodyText));
        OnPropertyChanged(nameof(DisplayTitle));
        MarkClean();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void MarkClean() { _isDirty = false; OnPropertyChanged(nameof(IsDirty)); }
}
