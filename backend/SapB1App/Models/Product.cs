namespace SapB1App.Models;

public class Product
{
    public int      Id          { get; set; }
    public string   ItemCode    { get; set; } = string.Empty;
    public string   ItemName    { get; set; } = string.Empty;
    public string?  Description { get; set; }
    public decimal  Price       { get; set; }
    public string?  Category    { get; set; }
    public int      Stock       { get; set; }
    public string?  Unit        { get; set; } = "Pcs";
    public bool     IsActive    { get; set; } = true;
    public int?     SapDocNum   { get; set; }
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt  { get; set; }
}
