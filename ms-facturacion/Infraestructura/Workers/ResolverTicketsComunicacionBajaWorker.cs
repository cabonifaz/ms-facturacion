using Microsoft.Extensions.Hosting;
using ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

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
        var loteRepositorio = scope.ServiceProvider.GetRequiredService<ILoteDocumentoRepositorio>();

        // El tope de fila por ciclo se resuelve dentro de SP_LoteDocumento_ListarPendientesTicket
        // (TABLA_MAESTRA IdMaestro=9), no acá — así se puede ajustar con un UPDATE, sin redeploy.
        var pendientes = await loteRepositorio.ListarPendientesTicketAsync(cancellationToken);
        if (pendientes.IdTipoMensaje != TipoMensaje.Exito || pendientes.Datos is null)
        {
            if (pendientes.IdTipoMensaje != TipoMensaje.Exito)
            {
                logger.LogWarning("No se pudo listar los lotes pendientes de ticket: {Mensaje}", pendientes.Mensaje);
            }

            return;
        }

        if (pendientes.Datos.Count == 0)
        {
            return;
        }

        // AmbienteCodigo se deriva del entorno real del servidor, igual que en LotesDocumentoController.
        var ambienteCodigo = entorno.IsDevelopment() || entorno.IsStaging() ? "Beta" : "Produccion";
        var consultarTicketCasoDeUso = scope.ServiceProvider.GetRequiredService<ConsultarTicketComunicacionBajaCasoDeUso>();

        foreach (var lote in pendientes.Datos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resultado = await consultarTicketCasoDeUso.EjecutarAsync(
                lote.IdInquilino, lote.IdLoteDocumento, ambienteCodigo, cancellationToken);

            if (resultado.IdTipoMensaje != TipoMensaje.Exito)
            {
                logger.LogWarning(
                    "No se pudo consultar el ticket del lote {IdLoteDocumento} (inquilino {IdInquilino}): {Mensaje}",
                    lote.IdLoteDocumento, lote.IdInquilino, resultado.Mensaje);
            }
        }
    }
}
