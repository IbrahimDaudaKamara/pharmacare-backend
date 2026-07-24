using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediCorePMS.Data;
using MediCorePMS.DTOs;
using MediCorePMS.Models;

namespace MediCorePMS.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SalesController(AppDbContext db) : ControllerBase
{
    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static SaleDto ToDto(Sale s) => new(
        s.Id,
        s.ReceiptNo,
        s.CashierId,
        s.Cashier?.Name ?? string.Empty,
        s.CustomerName,
        s.Items.Select(i => new SaleItemDto(
            i.Id, i.MedicineId,
            i.Medicine?.Name ?? string.Empty,
            i.Quantity, i.UnitPrice,
            i.Quantity * i.UnitPrice)).ToList(),
        s.Subtotal,
        s.Discount,
        s.Total,
        s.PaymentMethod,
        s.Refunded,
        s.CreatedAt);

    private IQueryable<Sale> WithIncludes() =>
        db.Sales
          .Include(s => s.Cashier)
          .Include(s => s.Items).ThenInclude(i => i.Medicine);

    /// <summary>Get all sales with optional date range / search filters.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string?   search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string?   paymentMethod,
        [FromQuery] int       page     = 1,
        [FromQuery] int       pageSize = 50)
    {
        var q = WithIncludes().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(s => s.ReceiptNo.Contains(search) || s.CustomerName.Contains(search));

        if (from.HasValue)          q = q.Where(s => s.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue)            q = q.Where(s => s.CreatedAt <= to.Value.ToUniversalTime());
        if (paymentMethod is not null) q = q.Where(s => s.PaymentMethod == paymentMethod);

        var total = await q.CountAsync();
        var data  = await q.OrderByDescending(s => s.CreatedAt)
                           .Skip((page - 1) * pageSize)
                           .Take(pageSize)
                           .ToListAsync();

        return Ok(new PagedResult<SaleDto>(data.Select(ToDto).ToList(), total, page, pageSize));
    }

    /// <summary>Get a single sale / receipt.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var sale = await WithIncludes().FirstOrDefaultAsync(s => s.Id == id);
        return sale is null ? NotFound() : Ok(ToDto(sale));
    }

    /// <summary>Process a new sale (POS). Decrements stock automatically.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSaleRequest req)
    {
        if (req.Items is null || req.Items.Count == 0)
            return BadRequest(new MessageResult("Sale must have at least one item."));

        // Validate stock and build items
        var saleItems = new List<SaleItem>();
        decimal subtotal = 0;

        foreach (var item in req.Items)
        {
            var med = await db.Medicines.FindAsync(item.MedicineId);
            if (med is null || !med.Active)
                return BadRequest(new MessageResult($"Medicine ID {item.MedicineId} not found or inactive."));
            if (med.Stock < item.Quantity)
                return BadRequest(new MessageResult($"Insufficient stock for '{med.Name}'. Available: {med.Stock}."));

            saleItems.Add(new SaleItem
            {
                MedicineId = item.MedicineId,
                Quantity   = item.Quantity,
                UnitPrice  = item.UnitPrice,
            });
            subtotal += item.Quantity * item.UnitPrice;
        }

        // Generate receipt number
        var count     = await db.Sales.CountAsync();
        var receiptNo = $"RCP-{(count + 1):D3}";

        var sale = new Sale
        {
            ReceiptNo     = receiptNo,
            CashierId     = CurrentUserId,
            CustomerName  = req.CustomerName,
            Subtotal      = subtotal,
            Discount      = req.Discount,
            Total         = subtotal - req.Discount,
            PaymentMethod = req.PaymentMethod,
            Refunded      = false,
            Items         = saleItems,
        };

        db.Sales.Add(sale);

        // Decrement stock
        foreach (var item in req.Items)
        {
            var med = await db.Medicines.FindAsync(item.MedicineId);
            med!.Stock -= item.Quantity;
        }

        await db.SaveChangesAsync();

        // Reload with includes for response
        var created = await WithIncludes().FirstOrDefaultAsync(s => s.Id == sale.Id);
        return CreatedAtAction(nameof(GetById), new { id = sale.Id }, ToDto(created!));
    }

    /// <summary>Refund a sale. Marks as refunded and restores stock.</summary>
    [HttpPost("{id:int}/refund")]
    [Authorize(Roles = "Admin,Pharmacist")]
    public async Task<IActionResult> Refund(int id)
    {
        var sale = await WithIncludes().FirstOrDefaultAsync(s => s.Id == id);
        if (sale is null) return NotFound();
        if (sale.Refunded) return BadRequest(new MessageResult("Sale has already been refunded."));

        // Restore stock for each item
        foreach (var item in sale.Items)
        {
            var med = await db.Medicines.FindAsync(item.MedicineId);
            if (med is not null) med.Stock += item.Quantity;
        }

        sale.Refunded = true;
        await db.SaveChangesAsync();

        return Ok(ToDto(sale));
    }

    /// <summary>Get daily revenue for the last N days (dashboard chart).</summary>
    [HttpGet("revenue/daily")]
    public async Task<IActionResult> DailyRevenue([FromQuery] int days = 7)
    {
        var result = new List<DailyRevenue>();
        for (int i = days - 1; i >= 0; i--)
        {
            var date  = DateTime.UtcNow.Date.AddDays(-i);
            var rev   = await db.Sales
                            .Where(s => s.CreatedAt.Date == date)
                            .SumAsync(s => (decimal?)s.Total) ?? 0;
            result.Add(new DailyRevenue(
                date.ToString("yyyy-MM-dd"),
                date.ToString("ddd"),
                rev));
        }
        return Ok(result);
    }

    /// <summary>Get top N medicines by quantity sold.</summary>
    [HttpGet("top-medicines")]
    public async Task<IActionResult> TopMedicines([FromQuery] int top = 5)
    {
        var topMeds = await db.SaleItems
            .GroupBy(si => si.MedicineId)
            .Select(g => new { MedicineId = g.Key, TotalQty = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.TotalQty)
            .Take(top)
            .ToListAsync();

        var result = new List<TopMedicine>();
        foreach (var t in topMeds)
        {
            var med = await db.Medicines.FindAsync(t.MedicineId);
            result.Add(new TopMedicine(t.MedicineId, med?.Name ?? "Unknown", t.TotalQty));
        }
        return Ok(result);
    }
}
