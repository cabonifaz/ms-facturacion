using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface ILoteDocumentoRepositorio
{
    Task<ResultadoOperacion<LoteDocumentoCreado>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, DateOnly fechaReferencia, DateOnly fechaGeneracion,
        IReadOnlyList<ItemBajaEntrada> items, CancellationToken cancellationToken);

    /// Lote de un solo documento (TipoLoteCodigo='AnulacionManual'), creado por
    /// AnularManualmenteDocumentoElectronicoCasoDeUso — nunca se transmite a SUNAT, solo sirve de contenedor
    /// de auditoría/almacenamiento para el Pdf "ANULADO" regenerado. No repite las validaciones de
    /// InsertarAsync (plazo de 7 días, estado del documento, baja en curso, etc.): la elegibilidad ya se
    /// validó en SP_DocumentoElectronico_AnularManualmente, justo antes en la misma orquestación.
    Task<ResultadoOperacion<LoteDocumentoCreado>> InsertarManualAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, int idDocumentoElectronico, string motivoDescripcion,
        DateOnly fechaReferencia, DateTime fechaGeneracion, CancellationToken cancellationToken);

    Task<ResultadoOperacion<LoteDocumentoDetalle>> ObtenerAsync(
        int idInquilino, int idLoteDocumento, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> ActualizarEstadoSunatAsync(
        string usuarioEjecutor, int idInquilino, int idLoteDocumento, EstadoMaestroCodigo estadoCodigo, string? ticket,
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, CancellationToken cancellationToken);

    Task<ResultadoOperacion<IReadOnlyList<LotePendienteTicket>>> ListarPendientesTicketAsync(CancellationToken cancellationToken);
}
