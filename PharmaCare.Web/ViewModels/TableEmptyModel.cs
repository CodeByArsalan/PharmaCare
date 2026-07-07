namespace PharmaCare.Web.ViewModels;

/// <summary>
/// Model for the shared <c>_TableEmpty</c> partial — a consistent empty-state row for list tables,
/// with an optional call-to-action link ("Create your first …").
/// </summary>
public class TableEmptyModel
{
    public string Icon { get; set; } = "fa-inbox";
    public string Message { get; set; } = "No records found yet.";
    public int ColSpan { get; set; } = 1;
    public string? CtaText { get; set; }
    public string? CtaController { get; set; }
    public string? CtaAction { get; set; }
}
