namespace ms_facturacion.Infraestructura.Seguridad
{
    // Único llamador válido es maximlian3_backend; valida el header X-Api-Key contra ApiKey en appsettings.
    public class ApiKeyMiddleware(RequestDelegate siguiente, IConfiguration configuracion)
    {
        private const string NombreHeader = "X-Api-Key";

        public async Task InvokeAsync(HttpContext contexto)
        {
            var apiKeyEsperada = configuracion["ApiKey"];

            if (string.IsNullOrEmpty(apiKeyEsperada) ||
                !contexto.Request.Headers.TryGetValue(NombreHeader, out var apiKeyRecibida) ||
                apiKeyRecibida != apiKeyEsperada)
            {
                contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await siguiente(contexto);
        }
    }
}
