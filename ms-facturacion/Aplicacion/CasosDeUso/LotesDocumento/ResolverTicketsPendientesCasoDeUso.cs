using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;

// Excepción deliberada a "depende solo de Puertos": delega en ConsultarTicketComunicacionBajaCasoDeUso
// en vez de duplicar su lógica de consulta a SUNAT.
public sealed class ResolverTicketsPendientesCasoDeUso(
    ILoteDocumentoRepositorio loteRepositorio,
    ConsultarTicketComunicacionBajaCasoDeUso consultarTicketCasoDeUso,
    ILogger<ResolverTicketsPendientesCasoDeUso> logger)
{
    public async Task<ResultadoOperacion<IReadOnlyList<ResultadoResolucionTicket>>> EjecutarAsync(
        string ambienteCodigo, CancellationToken cancellationToken)
    {
        try
        {
            return await EjecutarInternoAsync(ambienteCodigo, cancellationToken);
        }
        catch (Exception ex)
        {
            // consultarTicketCasoDeUso.EjecutarAsync ya atrapa sus propias excepciones (ver
            // ConsultarTicketComunicacionBajaCasoDeUso) — lo único que puede llegar hasta acá es una
            // excepción de ListarPendientesTicketAsync. Mismo criterio que el resto de los Casos de Uso:
            // sin esto, un ciclo entero del worker se perdía en silencio.
            logger.LogError(ex, "ResolverTicketsPendientes — excepción no controlada. ambienteCodigo={AmbienteCodigo}.", ambienteCodigo);
            return ResultadoOperacion<IReadOnlyList<ResultadoResolucionTicket>>.DeErrorSistema(ex.Message);
        }
    }

    private async Task<ResultadoOperacion<IReadOnlyList<ResultadoResolucionTicket>>> EjecutarInternoAsync(
        string ambienteCodigo, CancellationToken cancellationToken)
    {
        var pendientes = await loteRepositorio.ListarPendientesTicketAsync(cancellationToken);
        if (pendientes.IdTipoMensaje != TipoMensaje.Exito || pendientes.Datos is null)
        {
            if (pendientes.IdTipoMensaje == TipoMensaje.ErrorSistema)
            {
                logger.LogError("ResolverTicketsPendientes — falló al listar lotes pendientes: {Mensaje}", pendientes.Mensaje);
            }

            return new ResultadoOperacion<IReadOnlyList<ResultadoResolucionTicket>>(
                pendientes.IdTipoMensaje, pendientes.Mensaje, default);
        }

        var resultados = new List<ResultadoResolucionTicket>();

        foreach (var lote in pendientes.Datos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resultado = await consultarTicketCasoDeUso.EjecutarAsync(
                lote.IdInquilino, lote.IdLoteDocumento, ambienteCodigo, cancellationToken);

            resultados.Add(new ResultadoResolucionTicket(
                lote.IdInquilino, lote.IdLoteDocumento, resultado.IdTipoMensaje == TipoMensaje.Exito, resultado.Mensaje));
        }

        return ResultadoOperacion<IReadOnlyList<ResultadoResolucionTicket>>.DeExito(
            "Ciclo de resolución de tickets pendientes ejecutado.", resultados);
    }
}
