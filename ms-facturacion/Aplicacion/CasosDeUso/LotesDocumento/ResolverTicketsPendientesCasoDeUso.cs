using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;

// Excepción deliberada a "depende solo de Puertos": delega en ConsultarTicketComunicacionBajaCasoDeUso
// en vez de duplicar su lógica de consulta a SUNAT.
public sealed class ResolverTicketsPendientesCasoDeUso(
    ILoteDocumentoRepositorio loteRepositorio,
    ConsultarTicketComunicacionBajaCasoDeUso consultarTicketCasoDeUso)
{
    public async Task<ResultadoOperacion<IReadOnlyList<ResultadoResolucionTicket>>> EjecutarAsync(
        string ambienteCodigo, CancellationToken cancellationToken)
    {
        var pendientes = await loteRepositorio.ListarPendientesTicketAsync(cancellationToken);
        if (pendientes.IdTipoMensaje != TipoMensaje.Exito || pendientes.Datos is null)
        {
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
