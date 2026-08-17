using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface ILoteDocumentoRepositorio
{
    Task<ResultadoOperacion<LoteDocumentoCreado>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, DateOnly fechaReferencia, DateOnly fechaGeneracion,
        IReadOnlyList<ItemBajaEntrada> items, CancellationToken cancellationToken);

    /// Solo lectura — corre las mismas validaciones que InsertarAsync (ítems elegibles, sin Nota de Crédito/
    /// Débito vigente con desenlace todavía desconocido, plazo de 7 días, FechaEmision compartida, etc.) sin
    /// escribir nada, para que el llamador sepa de antemano si la Comunicación de Baja va a poder enviarse y
    /// qué documentos incluiría (los indicados + las Notas vigentes que se arrastrarían). Ver
    /// SP_LoteDocumento_PrevisualizarBaja.
    Task<ResultadoOperacion<IReadOnlyList<DocumentoBajaPreview>>> PrevisualizarBajaAsync(
        int idInquilino, int idEmpresa, DateOnly fechaReferencia, DateOnly fechaGeneracion,
        IReadOnlyList<ItemBajaEntrada> items, CancellationToken cancellationToken);

    /// Lote (TipoLoteCodigo='AnulacionManual') para el documento anulado manualmente + toda Nota de
    /// Crédito/Débito vigente arrastrada con él (ver IDocumentoElectronicoRepositorio.AnularManualmenteAsync),
    /// creado por AnularManualmenteDocumentoElectronicoCasoDeUso — nunca se transmite a SUNAT, solo sirve de
    /// contenedor de auditoría/almacenamiento para el Pdf "ANULADO" regenerado de cada uno. No repite las
    /// validaciones de InsertarAsync (plazo de 7 días, estado del documento, baja en curso, etc.): la
    /// elegibilidad ya se validó en SP_DocumentoElectronico_AnularManualmente, justo antes en la misma
    /// orquestación. fechaReferencia es la fecha de la propia anulación, no la FechaEmision de ningún
    /// documento — con varios documentos de fechas distintas, es lo único que todos comparten de verdad.
    Task<ResultadoOperacion<LoteDocumentoCreado>> InsertarManualAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, IReadOnlyList<ItemBajaEntrada> items,
        DateOnly fechaReferencia, DateTime fechaGeneracion, CancellationToken cancellationToken);

    Task<ResultadoOperacion<LoteDocumentoDetalle>> ObtenerAsync(
        int idInquilino, int idLoteDocumento, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> ActualizarEstadoSunatAsync(
        string usuarioEjecutor, int idInquilino, int idLoteDocumento, EstadoMaestroCodigo estadoCodigo, string? ticket,
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, CancellationToken cancellationToken);

    Task<ResultadoOperacion<IReadOnlyList<LotePendienteTicket>>> ListarPendientesTicketAsync(CancellationToken cancellationToken);
}
