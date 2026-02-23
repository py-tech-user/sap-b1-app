namespace SapB1App.DTOs;

public class ProductDto
{
    public int      Id          { get; set; }
    public string   ItemCode    { get; set; } = string.Empty;
    public string   ItemName    { get; set; } = string.Empty;
    public string?  Description { get; set; }
    public decimal  Price       { get; set; }
    public string?  Category    { get; set; }
    public int      Stock       { get; set; }
    public string?  Unit        { get; set; }
    public bool     IsActive    { get; set; }
    public DateTime CreatedAt   { get; set; }
}

public class CreateProductDto
{
    public string   ItemCode    { get; set; } = string.Empty;
    public string   ItemName    { get; set; } = string.Empty;
    public string?  Description { get; set; }
    public decimal  Price       { get; set; }
    public string?  Category    { get; set; }
    public int      Stock       { get; set; }
    public string?  Unit        { get; set; } = "Pcs";
}

public class UpdateProductDto
{
    public string   ItemName    { get; set; } = string.Empty;
    public string?  Description { get; set; }
    public decimal  Price       { get; set; }
    public string?  Category    { get; set; }
    public int      Stock       { get; set; }
}
