using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using NotepadMinus.Services;

namespace NotepadMinus;

/// <summary>
/// Paints translucent rectangles over all Find matches that fall in the visible
/// portion of the adorned TextBox. The "current" match gets a stronger tint;
/// the actual caret selection is handled by <see cref="TextBox.Select(int,int)"/>.
/// </summary>
internal sealed class MatchHighlightAdorner : Adorner
{
    // Cap how many decorations we attempt to paint per frame even within the
    // viewport; tens of thousands of overlapping rects would still tank perf.
    private const int MaxRectsPerFrame = 2000;

    private readonly TextBox _editor;
    private static readonly Brush AllMatchesBrush = new SolidColorBrush(Color.FromArgb(80, 255, 210, 74));
    private static readonly Brush CurrentMatchBrush = new SolidColorBrush(Color.FromArgb(140, 255, 165, 0));

    static MatchHighlightAdorner()
    {
        AllMatchesBrush.Freeze();
        CurrentMatchBrush.Freeze();
    }

    public MatchHighlightAdorner(TextBox editor) : base(editor)
    {
        _editor = editor;
        IsHitTestVisible = false;
    }

    public IReadOnlyList<FindService.Match> Matches { get; set; } = System.Array.Empty<FindService.Match>();
    public int CurrentIndex { get; set; } = -1;

    protected override void OnRender(DrawingContext dc)
    {
        if (Matches.Count == 0) return;
        var viewportHeight = _editor.ViewportHeight;
        var viewportWidth = _editor.ViewportWidth;
        if (viewportHeight <= 0 || viewportWidth <= 0) return;

        var textLength = _editor.Text?.Length ?? 0;
        var painted = 0;

        for (int i = 0; i < Matches.Count && painted < MaxRectsPerFrame; i++)
        {
            var m = Matches[i];
            if (m.Start < 0 || m.Length <= 0 || m.Start >= textLength) continue;
            var end = System.Math.Min(m.Start + m.Length - 1, textLength - 1);

            var startRect = _editor.GetRectFromCharacterIndex(m.Start);
            if (startRect.IsEmpty) continue;
            // Cheap viewport cull.
            if (startRect.Bottom < 0 || startRect.Top > viewportHeight) continue;

            var endRect = _editor.GetRectFromCharacterIndex(end, trailingEdge: true);
            if (endRect.IsEmpty) endRect = startRect;

            var brush = i == CurrentIndex ? CurrentMatchBrush : AllMatchesBrush;

            if (System.Math.Abs(endRect.Top - startRect.Top) < 0.5)
            {
                // Single-line match.
                var rect = new Rect(startRect.X, startRect.Y,
                    System.Math.Max(1, endRect.X - startRect.X), startRect.Height);
                dc.DrawRectangle(brush, null, rect);
            }
            else
            {
                // Multi-line match: first line to right edge, full middle lines, last line from left.
                dc.DrawRectangle(brush, null,
                    new Rect(startRect.X, startRect.Y,
                        System.Math.Max(1, viewportWidth - startRect.X), startRect.Height));
                var midTop = startRect.Bottom;
                if (endRect.Top > midTop)
                {
                    dc.DrawRectangle(brush, null,
                        new Rect(0, midTop, viewportWidth, endRect.Top - midTop));
                }
                dc.DrawRectangle(brush, null,
                    new Rect(0, endRect.Top, System.Math.Max(1, endRect.X), endRect.Height));
            }
            painted++;
        }
    }
}
