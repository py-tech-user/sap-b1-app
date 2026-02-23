namespace SapB1App.DTOs;

public class VisitDto
{
    public int      Id           { get; set; }
    public int      CustomerId   { get; set; }
    public string   CustomerName { get; set; } = string.Empty;
    public string   CustomerCode { get; set; } = string.Empty;
    public int?     UserId       { get; set; }
    public string?  UserName     { get; set; }
    public DateTime Date         { get; set; }
    public string   Status       { get; set; } = string.Empty;
    public string?  Comments     { get; set; }

    // Position planifiée
    public double?  Latitude     { get; set; }
    public double?  Longitude    { get; set; }

    // Check-in
    public DateTime? CheckInAt        { get; set; }
    public double?   CheckInLatitude  { get; set; }
    public double?   CheckInLongitude { get; set; }

    // Check-out
    public DateTime? CheckOutAt        { get; set; }
    public double?   CheckOutLatitude  { get; set; }
    public double?   CheckOutLongitude { get; set; }

    // Distance et durée
    public double?  DistanceKm   { get; set; }
    public double?  DurationMins { get; set; }  // Calculé: CheckOutAt - CheckInAt

    public bool     SyncedToSap  { get; set; }
    public DateTime CreatedAt    { get; set; }
}

public class CreateVisitDto
{
    public int      CustomerId { get; set; }
    public int?     UserId     { get; set; }  // Commercial assigné
    public DateTime Date       { get; set; }
    public string   Status     { get; set; } = "Planned";
    public string?  Comments   { get; set; }
    public double?  Latitude   { get; set; }
    public double?  Longitude  { get; set; }
}

public class UpdateVisitDto
{
    public int?     UserId    { get; set; }
    public DateTime Date      { get; set; }
    public string   Status    { get; set; } = string.Empty;
    public string?  Comments  { get; set; }
    public double?  Latitude  { get; set; }
    public double?  Longitude { get; set; }
}
