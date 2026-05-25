namespace ImmersiveDisplay.Models;

public class IconImportResult
{
    public string FileName { get; set; } = "";
    public string Base64 { get; set; } = "";
    public bool ConflictResolved { get; set; }
    public string? ResolvedFileName { get; set; }
}
