namespace PharmaCare.Domain.Models.Prescriptions;

/// <summary>
/// Represents a medical prescription
/// </summary>
public class Prescription
{
    public int PrescriptionID { get; set; }
    public string PrescriptionNumber { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public DateTime PrescriptionDate { get; set; }
    public string FilePath { get; set; } = string.Empty; // Scanned prescription file
    public int UploadedBy { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
}
