namespace SapB1App.Models;

/// <summary>
/// Enregistrement d'une position GPS d'un commercial.
/// </summary>
public class LocationTrack
{
    public int       Id          { get; set; }
    public int       UserId      { get; set; }
    public double    Latitude    { get; set; }
    public double    Longitude   { get; set; }
    public double?   Accuracy    { get; set; }  // Précision GPS en mètres
    public double?   Speed       { get; set; }  // Vitesse en km/h
    public double?   Heading     { get; set; }  // Direction en degrés (0-360)
    public double?   Altitude    { get; set; }  // Altitude en mètres
    public DateTime  RecordedAt  { get; set; } = DateTime.UtcNow;

    // Navigation
    public AppUser User { get; set; } = null!;
}

/// <summary>
/// Type d'action de check-in/check-out.
/// </summary>
public enum CheckPointType
{
    CheckIn,
    CheckOut
}

/// <summary>
/// Enregistrement de check-in/check-out lors d'une visite client.
/// </summary>
public class VisitCheckPoint
{
    public int             Id         { get; set; }
    public int             VisitId    { get; set; }
    public int             UserId     { get; set; }
    public CheckPointType  Type       { get; set; }
    public double          Latitude   { get; set; }
    public double          Longitude  { get; set; }
    public double?         Accuracy   { get; set; }
    public string?         Address    { get; set; }  // Adresse géocodée (optionnelle)
    public string?         Notes      { get; set; }
    public DateTime        Timestamp  { get; set; } = DateTime.UtcNow;

    // Navigation
    public Visit   Visit { get; set; } = null!;
    public AppUser User  { get; set; } = null!;
}

/// <summary>
/// Résumé journalier de la distance parcourue par un commercial.
/// </summary>
public class DailyTrackSummary
{
    public int      Id              { get; set; }
    public int      UserId          { get; set; }
    public DateOnly Date            { get; set; }
    public double   TotalDistanceKm { get; set; }
    public int      PointsCount     { get; set; }
    public int      VisitsCount     { get; set; }
    public TimeSpan? TotalDuration  { get; set; }
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt      { get; set; }

    // Navigation
    public AppUser User { get; set; } = null!;
}
