namespace ImmersiveDisplay.Models;

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
