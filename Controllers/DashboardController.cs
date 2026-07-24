using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediCorePMS.Data;
using MediCorePMS.DTOs;

namespace MediCorePMS.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(AppDbContext db) : ControllerBase
{
    /// <summary>Returns all KPIs and chart data for the dashboard.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var today        = DateTime.UtcNow.Date;
        var totalRevenue = await db.Sales.SumAsync(s => (decimal?)s.Total) ?? 0;
        var totalTx      = await db.Sales.CountAsync();
        var todaySales   = await db.Sales.Where(s => s.CreatedAt.Date == today).ToListAsync();
        var todayRev     = todaySales.Sum(s => s.Total);
        var activeMeds   = await db.Medicines.CountAsync(m => m.Active);
        var lowStock     = await db.Medicines.CountAsync(m => m.Active && m.Stock > 0 && m.Stock <= m.ReorderLevel);
        var outOfStock   = await db.Medicines.CountAsync(m => m.Active && m.Stock == 0);

        // Last 7 days revenue
        var last7 = new List<DailyRevenue>();
        for (int i = 6; i >= 0; i--)
        {
            var d   = today.AddDays(-i);
            var rev = await db.Sales
                          .Where(s => s.CreatedAt.Date == d)
                          .SumAsync(s => (decimal?)s.Total) ?? 0;
            last7.Add(new DailyRevenue(d.ToString("yyyy-MM-dd"), d.ToString("ddd"), rev));
        }

        // Top 5 medicines by qty
        var topMeds = await db.SaleItems
            .GroupBy(si => si.MedicineId)
            .Select(g => new { MedicineId = g.Key, TotalQty = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.TotalQty)
            .Take(5)
            .ToListAsync();

        var topMedList = new List<TopMedicine>();
        foreach (var t in topMeds)
        {
            var med = await db.Medicines.FindAsync(t.MedicineId);
            topMedList.Add(new TopMedicine(t.MedicineId, med?.Name ?? "Unknown", t.TotalQty));
        }

        return Ok(new DashboardDto(
            totalRevenue, totalTx, activeMeds,
            lowStock, outOfStock,
            todayRev, todaySales.Count,
            last7, topMedList));
    }
}
