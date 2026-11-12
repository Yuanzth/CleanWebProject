using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ITAssetTrack.Models;
using ITAssetTrack.Services;

namespace ITAssetTrack.Data
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Auto-migrate database on MSSQL (Disabled - Migration on hold per user request)
            // context.Database.Migrate();

            // Seed Excel Master if empty (ignoring mock viewer asset)
            if (!context.Assets.Any(a => a.SerialNumber != "SN-GUEST-LAPTOP-12345"))
            {
                string[] possibleFiles = new string[]
                {
                    "Asset PC&NB_HQ_DEPT_with_NRP.xlsx",
                    "Asset PC & NB_HQ_DEPT_NRP.xlsx",
                    "Asset PC & NB_HQ_DEPT.xlsx"
                };

                string? resolvedPath = null;
                foreach (var fileName in possibleFiles)
                {
                    string candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                    if (File.Exists(candidate)) { resolvedPath = candidate; break; }

                    candidate = Path.Combine(Directory.GetCurrentDirectory(), fileName);
                    if (File.Exists(candidate)) { resolvedPath = candidate; break; }

                    candidate = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", fileName));
                    if (File.Exists(candidate)) { resolvedPath = candidate; break; }
                }

                if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath))
                {
                    var importService = new AssetExcelImportService(context);
                    importService.ImportExcelAsync(resolvedPath).GetAwaiter().GetResult();
                }
            }

            // Seed PM Plan if empty
            string currentMonth = DateTime.Today.ToString("yyyy-MM");
            if (!context.PmPlans.Any(p => p.MonthYear == currentMonth))
            {
                context.PmPlans.Add(new PmPlan { MonthYear = currentMonth, TargetQuantity = 100 });
                context.SaveChanges();
            }
        }
    }
}
