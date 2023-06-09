namespace Automation.Models;

public sealed class BlobFile
{
    public string Name { get; set; }
    public byte[] Bytes { get; set; }
    public string Identifier { get; set; }
    public int ResponseCode { get; set; }

    public bool IsSuccessCode() => ResponseCode == 200 || ResponseCode == 201;
}