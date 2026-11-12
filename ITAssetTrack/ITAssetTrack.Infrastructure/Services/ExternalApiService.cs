using System.Net.Http;
using System.Text.Json;
using ITAssetTrack.Models;

namespace ITAssetTrack.Services
{
    /// <summary>
    /// Mockup Service untuk integrasi ke ITSM dan ITAM.
    /// Ketika API sudah siap, ganti base URL dan sesuaikan response model.
    /// </summary>
    public class ExternalApiService
    {
        private readonly HttpClient _httpClient;
        
        public ExternalApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // _httpClient.BaseAddress = new Uri("https://api.infrahub.com/");
        }

        public async Task<Asset?> GetAssetFromITAM(string serialNumber)
        {
            // TODO: Ganti dengan pemanggilan API ITAM sungguhan
            // var response = await _httpClient.GetAsync($"itam/assets?sn={serialNumber}");
            // if (response.IsSuccessStatusCode)
            // {
            //     var content = await response.Content.ReadAsStringAsync();
            //     return JsonSerializer.Deserialize<Asset>(content);
            // }

            // MOCKUP RESPONSE:
            await Task.Delay(500); // simulate network delay
            if(serialNumber == "MOCK-123")
            {
                return new Asset
                {
                    SerialNumber = "MOCK-123",
                    DeviceType = "Notebook",
                    Brand = "MockBrand",
                    Ram = "32 GB",
                    Storage = "1 TB",
                    Status = "InUse"
                };
            }
            return null;
        }

        public async Task<bool> SendTicketToITSM(UnscheduledMaint pm)
        {
            // TODO: Ganti dengan pemanggilan API ITSM sungguhan (misal membuat tiket HPSM)
            // var payload = new {
            //     title = $"Trouble: {pm.SerialNumber}",
            //     description = pm.Keluhan,
            //     priority = pm.Priority,
            //     reportedBy = pm.NamaUser
            // };
            // var response = await _httpClient.PostAsJsonAsync("itsm/tickets", payload);
            // return response.IsSuccessStatusCode;

            // MOCKUP RESPONSE:
            await Task.Delay(500);
            return true;
        }
    }
}
