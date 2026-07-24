namespace MediCorePMS.DTOs;

// ─── Auth ─────────────────────────────────────────────────────────
public record LoginRequest(string Email, string Password);

public record LoginResponse(
    int Id,
    string Name,
    string Email,
    string Role,
    string Avatar,
    string AvatarColor,
    string Token
);

public record RegisterAdminRequest(
    string Name,
    string Email,
    string Password,
    string AdminKey
);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Code, string NewPassword);

// ─── User ─────────────────────────────────────────────────────────
public record UserDto(
    int Id,
    string Name,
    string Email,
    string Role,
    bool Active,
    string Avatar,
    string AvatarColor,
    DateTime CreatedAt
);

public record CreateUserRequest(
    string Name,
    string Email,
    string Password,
    string Role
);

public record UpdateUserRequest(
    string? Name,
    string? Email,
    string? Password,
    string? Role
);

// ─── Category ─────────────────────────────────────────────────────
public record CategoryDto(
    int Id,
    string Name,
    string? Description,
    int MedicineCount
);

public record CreateCategoryRequest(string Name, string? Description);
public record UpdateCategoryRequest(string? Name, string? Description);

// ─── Medicine ─────────────────────────────────────────────────────
public record MedicineDto(
    int Id,
    string Name,
    string GenericName,
    int CategoryId,
    string CategoryName,
    decimal Price,
    decimal Cost,
    int Stock,
    int ReorderLevel,
    string Unit,
    DateTime Expiry,
    string Manufacturer,
    string Barcode,
    bool Active,
    bool RequiresPrescription,
    DateTime CreatedAt
);

public record CreateMedicineRequest(
    string Name,
    string GenericName,
    int CategoryId,
    decimal Price,
    decimal Cost,
    int Stock,
    int ReorderLevel,
    string Unit,
    DateTime Expiry,
    string Manufacturer,
    string Barcode,
    bool RequiresPrescription = false
);

public record UpdateMedicineRequest(
    string? Name,
    string? GenericName,
    int? CategoryId,
    decimal? Price,
    decimal? Cost,
    int? Stock,
    int? ReorderLevel,
    string? Unit,
    DateTime? Expiry,
    string? Manufacturer,
    string? Barcode,
    bool? Active,
    bool? RequiresPrescription
);

// ─── Sale ─────────────────────────────────────────────────────────
public record SaleItemRequest(int MedicineId, int Quantity, decimal UnitPrice);

public record CreateSaleRequest(
    string CustomerName,
    List<SaleItemRequest> Items,
    decimal Discount,
    string PaymentMethod
);

public record SaleItemDto(
    int Id,
    int MedicineId,
    string MedicineName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal
);

public record SaleDto(
    int Id,
    string ReceiptNo,
    int CashierId,
    string CashierName,
    string CustomerName,
    List<SaleItemDto> Items,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    string PaymentMethod,
    bool Refunded,
    DateTime CreatedAt
);

// ─── Dashboard / Reports ──────────────────────────────────────────
public record DashboardDto(
    decimal TotalRevenue,
    int TotalTransactions,
    int ActiveMedicines,
    int LowStockCount,
    int OutOfStockCount,
    decimal TodayRevenue,
    int TodaySales,
    List<DailyRevenue> Last7Days,
    List<TopMedicine> TopMedicines
);

public record DailyRevenue(string Date, string DayLabel, decimal Revenue);
public record TopMedicine(int MedicineId, string Name, int TotalQty);

public record StockAlertDto(
    int Id,
    string Name,
    int Stock,
    int ReorderLevel,
    string Unit,
    string Status,        // "out" | "low" | "near_expiry"
    DateTime? Expiry
);

// ─── Shared ───────────────────────────────────────────────────────
public record PagedResult<T>(List<T> Data, int Total, int Page, int PageSize);
public record MessageResult(string Message);
