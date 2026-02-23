namespace SapB1App.Models;

public enum VisitStatus
{
    Planned,      // Prévue
    InProgress,   // En cours (check-in effectué)
    Completed,    // Terminée (check-out effectué)
    Cancelled     // Annulée
}

public class Visit
{
    public int         Id         { get; set; }
    public int         CustomerId { get; set; }
    public int?        UserId     { get; set; }  // Commercial assigné
    public DateTime    Date       { get; set; }
    public VisitStatus Status     { get; set; } = VisitStatus.Planned;
    public string?     Comments   { get; set; }

    // Position planifiée (adresse client)
    public double?     Latitude   { get; set; }
    public double?     Longitude  { get; set; }

    // Check-in
    public DateTime?   CheckInAt        { get; set; }
    public double?     CheckInLatitude  { get; set; }
    public double?     CheckInLongitude { get; set; }

    // Check-out
    public DateTime?   CheckOutAt        { get; set; }
    public double?     CheckOutLatitude  { get; set; }
    public double?     CheckOutLongitude { get; set; }

    // Distance parcourue pendant la visite (en km)
    public double?     DistanceKm { get; set; }

    public int?        SapDocNum  { get; set; }
    public bool        SyncedToSap { get; set; } = false;
    public DateTime    CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime?   UpdatedAt  { get; set; }

    // Navigation
    public Customer Customer { get; set; } = null!;
    public AppUser? User     { get; set; }
    public ICollection<VisitCheckPoint> CheckPoints { get; set; } = new List<VisitCheckPoint>();
}
