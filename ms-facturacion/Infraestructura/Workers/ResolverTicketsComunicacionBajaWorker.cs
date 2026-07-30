using Microsoft.Extensions.Hosting;
using ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;
using ms_facturacion.Aplicacion.Comun;

namespace ms_facturacion.Infraestructura.Workers;

public sealed class ResolverTicketsComunicacionBajaWorker(
    IServiceScopeFactory scopeFactory, IHostEnvironment entorno, ILogger<ResolverTicketsComunicacionBajaWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ResolverPendientesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error no controlado al resolver tickets de comunicación de baja pendientes.");
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }

    private async Task ResolverPendientesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var casoDeUso = scope.ServiceProvider.GetRequiredService<ResolverTicketsPendientesCasoDeUso>();

        var ambienteCodigo = entorno.IsDevelopment() || entorno.IsStaging() ? "Beta" : "Produccion";
        var resultado = await casoDeUso.EjecutarAsync(ambienteCodigo, cancellationToken);

        if (resultado.IdTipoMensaje != TipoMensaje.Exito || resultado.Datos is null)
        {
            logger.LogWarning("No se pudo ejecutar el ciclo de resolución de tickets pendientes: {Mensaje}", resultado.Mensaje);
            return;
        }

        foreach (var item in resultado.Datos.Where(r => !r.Exito))
        {
            logger.LogWarning(
                "No se pudo consultar el ticket del lote {IdLoteDocumento} (inquilino {IdInquilino}): {Mensaje}",
                item.IdLoteDocumento, item.IdInquilino, item.Mensaje);
        }
    }
}
