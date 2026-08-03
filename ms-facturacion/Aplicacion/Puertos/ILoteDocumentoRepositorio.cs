using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface ILoteDocumentoRepositorio
{
    Task<ResultadoOperacion<LoteDocumentoCreado>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, DateOnly fechaReferencia,
        IReadOnlyList<ItemBajaEntrada> items, CancellationToken cancellationToken);

    Task<ResultadoOperacion<LoteDocumentoDetalle>> ObtenerAsync(
        int idInquilino, int idLoteDocumento, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> ActualizarEstadoSunatAsync(
        string usuarioEjecutor, int idInquilino, int idLoteDocumento, EstadoMaestroCodigo estadoCodigo, string? ticket,
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, CancellationToken cancellationToken);

    Task<ResultadoOperacion<IReadOnlyList<LotePendienteTicket>>> ListarPendientesTicketAsync(CancellationToken cancellationToken);
}
