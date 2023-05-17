namespace DomainObjects.Models;

public class TestRegistration
{
    public virtual Urn Urn { get; set; } = null!;
    public string Gender { get; set; } = "Not Provided";
    public DateTime? DateOfBirth { get; set; } = null;
    public DateTime SampleCollectedDate { get; set; } = DateTime.UtcNow;
    public string FirstName { get; set; } = "Not Provided";
    public string LastName { get; set; } = "Not Provided";
    public string PhoneNumber { get; set; } = "Not Provided";
}