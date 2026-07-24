namespace MediCorePMS.Models;

// ─── User ───────────────────────────────────────────────
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Cashier"; // Admin | Pharmacist | Cashier
    public bool Active { get; set; } = true;
    public string Avatar { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "linear-gradient(135deg,#4f8ef7,#7c5cfc)";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? PasswordResetCode { get; set; }
    public DateTime? ResetCodeExpires { get; set; }

    public ICollection<Sale> Sales { get; set; } = [];
}

// ─── Category ───────────────────────────────────────────
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Medicine> Medicines { get; set; } = [];
}

// ─── Medicine ───────────────────────────────────────────
public class Medicine
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GenericName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public int Stock { get; set; }
    public int ReorderLevel { get; set; }
    public string Unit { get; set; } = "tablets";
    public DateTime Expiry { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public bool RequiresPrescription { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SaleItem> SaleItems { get; set; } = [];
}

// ─── Sale ───────────────────────────────────────────────
public class Sale
{
    public int Id { get; set; }
    public string ReceiptNo { get; set; } = string.Empty;
    public int CashierId { get; set; }
    public User? Cashier { get; set; }
    public string CustomerName { get; set; } = "Walk-in Customer";
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } = "cash"; // cash | card
    public bool Refunded { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SaleItem> Items { get; set; } = [];
}

// ─── SaleItem ────────────────────────────────────────────
public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
}
