using ClosedXML.Excel;
using ExpenseTracker.Api.Common;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public record ExportFile(byte[] Content, string ContentType, string FileName);

public interface IExportService
{
    Task<ExportFile> ExportExpensesAsync(DateTime? from, DateTime? to);
}

public class ExcelExportService : IExportService
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;

    public ExcelExportService(AppDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<ExportFile> ExportExpensesAsync(DateTime? from, DateTime? to)
    {
        var filter = new ExpenseQuery { From = from, To = to };
        filter.ThrowIfInvalid();

        var userId = _user.Id;

        var items = await _db.Expenses
            .AsNoTracking()
            .Include(e => e.Category)
            .Where(e => e.UserId == userId)
            .ApplyFilters(filter)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Id)
            .ToListAsync();

        if (items.Count == 0)
            throw new NotFoundException("No expenses found in this date range.");

        using var workbook = new XLWorkbook();
        AddExpensesSheet(workbook, items);
        AddCategorySheet(workbook, items);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new ExportFile(
            stream.ToArray(),
            XlsxContentType,
            $"expenses_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }

    // Sheet 1: every expense, with a total row
    private static void AddExpensesSheet(XLWorkbook workbook, List<Expense> items)
    {
        var ws = workbook.Worksheets.Add("Expenses");
        ws.Cell(1, 1).Value = "Date";
        ws.Cell(1, 2).Value = "Category";
        ws.Cell(1, 3).Value = "Description";
        ws.Cell(1, 4).Value = "Amount";

        var row = 2;
        foreach (var e in items)
        {
            ws.Cell(row, 1).Value = e.Date;
            ws.Cell(row, 2).Value = e.Category!.Name;
            ws.Cell(row, 3).Value = e.Description ?? "";
            ws.Cell(row, 4).Value = e.Amount;
            row++;
        }

        var lastDataRow = row - 1;

        ws.Cell(row, 3).Value = "Total";
        ws.Cell(row, 4).Value = items.Sum(x => x.Amount);
        ws.Range(row, 1, row, 4).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        ws.Range(2, 1, lastDataRow, 1).Style.DateFormat.Format = "dd-mmm-yyyy";
        ws.Range(2, 4, row, 4).Style.NumberFormat.Format = "#,##0.00";
        StyleHeader(ws.Range(1, 1, 1, 4));
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    // Sheet 2: count and total per category
    private static void AddCategorySheet(XLWorkbook workbook, List<Expense> items)
    {
        var ws = workbook.Worksheets.Add("By category");
        ws.Cell(1, 1).Value = "Category";
        ws.Cell(1, 2).Value = "Transactions";
        ws.Cell(1, 3).Value = "Total";

        var groups = items
            .GroupBy(x => x.Category!.Name)
            .Select(g => new { Category = g.Key, Count = g.Count(), Total = g.Sum(x => x.Amount) })
            .OrderByDescending(g => g.Total)
            .ToList();

        var row = 2;
        foreach (var g in groups)
        {
            ws.Cell(row, 1).Value = g.Category;
            ws.Cell(row, 2).Value = g.Count;
            ws.Cell(row, 3).Value = g.Total;
            row++;
        }

        ws.Cell(row, 1).Value = "Total";
        ws.Cell(row, 2).Value = items.Count;
        ws.Cell(row, 3).Value = items.Sum(x => x.Amount);
        ws.Range(row, 1, row, 3).Style.Font.Bold = true;
        ws.Range(row, 1, row, 3).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        ws.Range(2, 3, row, 3).Style.NumberFormat.Format = "#,##0.00";
        StyleHeader(ws.Range(1, 1, 1, 3));
        ws.Columns().AdjustToContents();
    }

    private static void StyleHeader(IXLRange header)
    {
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F46E5");
    }
}