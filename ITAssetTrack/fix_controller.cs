using System;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string path = @""C:\webserver\www\ITAssetTrack\ITAssetTrack.Web\Controllers\MaintenanceController.cs"";
        string content = File.ReadAllText(path);
        
        // 1. SaveTargetPm
        int start1 = content.IndexOf(""public async Task<IActionResult> SaveTargetPm(ScheduledMaint entity)"");
        int end1 = content.IndexOf(""public async Task<IActionResult> ScheduledForm("", start1);
        string body1 = content.Substring(start1, end1 - start1);
        body1 = body1.Replace(""model."", ""entity."");
        content = content.Substring(0, start1) + body1 + content.Substring(end1);
        
        // 2. ScheduledForm POST
        int start2 = content.IndexOf(""public async Task<IActionResult> ScheduledForm(ScheduledMaint entity"");
        int end2 = content.IndexOf(""public async Task<IActionResult> PrintScheduledPm"", start2);
        string body2 = content.Substring(start2, end2 - start2);
        body2 = body2.Replace(""model."", ""entity."");
        content = content.Substring(0, start2) + body2 + content.Substring(end2);
        
        // 3. EndPm
        int start3 = content.IndexOf(""public async Task<IActionResult> EndPm(ScheduledMaint entity)"");
        int end3 = content.IndexOf(""public async Task<IActionResult> CancelPm"", start3);
        string body3 = content.Substring(start3, end3 - start3);
        body3 = body3.Replace(""model."", ""entity."");
        content = content.Substring(0, start3) + body3 + content.Substring(end3);
        
        File.WriteAllText(path, content);
    }
}
