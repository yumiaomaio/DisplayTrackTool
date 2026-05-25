namespace ImmersiveDisplay.Models;

public class UrlRequest
{
    public string? IconFileName { get; set; }
    public List<UrlEntryDto> Entries { get; set; } = [];
}
public class UrlEntryDto
{
    public string Name { get; set; } = "";
    public UrlLocationsDto Locations { get; set; } = new();
}

public class UrlLocationsDto
{
    public bool StartMenu { get; set; } = true;
    public bool Desktop { get; set; }
}