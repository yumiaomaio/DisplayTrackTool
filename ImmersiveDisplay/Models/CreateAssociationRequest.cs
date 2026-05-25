namespace ImmersiveDisplay.Models;

public class CreateAssociationRequest
{
    public string? IconFileName { get; set; }
    public List<UrlEntryDto> Entries { get; set; } = [];
}
