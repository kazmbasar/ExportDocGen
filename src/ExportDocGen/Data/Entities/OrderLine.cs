namespace ExportDocGen.Data.Entities;

/// <summary>One product line on an order.</summary>
public class OrderLine
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }

    /// <summary>Price per unit in the order's currency.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Display order on screens and documents.</summary>
    public int LineNumber { get; set; }
}
