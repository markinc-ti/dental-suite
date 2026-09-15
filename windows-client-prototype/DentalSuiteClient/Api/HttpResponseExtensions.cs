using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DentalSuiteClient.Api
{
    public static class HttpResponseExtensions
    {
        /// <summary>
        /// Igual que EnsureSuccessStatusCode(), pero si falla intenta leer
        /// el campo "detail" que manda FastAPI (el motivo real del
        /// rechazo, por ejemplo "Ya existe un usuario con ese correo.")
        /// en vez de solo mostrar el código HTTP.
        /// </summary>
        public static async Task EnsureSuccessWithDetailAsync(this HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            string detail = $"Código {(int)response.StatusCode} ({response.StatusCode})";
            try
            {
                string body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("detail", out var detailProp))
                {
                    detail = detailProp.ValueKind == JsonValueKind.String
                        ? detailProp.GetString() ?? detail
                        : detailProp.GetRawText();
                }
                else if (!string.IsNullOrWhiteSpace(body))
                {
                    detail = body;
                }
            }
            catch
            {
                // Si el cuerpo no es JSON o no se pudo leer, nos quedamos
                // con el código HTTP como mensaje.
            }

            throw new HttpRequestException(detail);
        }
    }
}
