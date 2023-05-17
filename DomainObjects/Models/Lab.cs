namespace DomainObjects.Models;

public class Lab
{
    public string Name { get; set; }
    public string AddressLine1 { get; set; }
    public string AddressLine2 { get; set; }
    public string Postcode { get; set; }
    public string City { get; set; }
    public virtual Country Country { get; set; }
}