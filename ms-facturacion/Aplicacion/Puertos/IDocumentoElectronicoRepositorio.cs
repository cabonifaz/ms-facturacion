using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface IDocumentoElectronicoRepositorio
{
    Task<ResultadoOperacion<DocumentoElectronicoCreado>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string idExterno, string? numeroReferencia,
        int idTipoDocumentoMaestro, DateOnly fechaEmision, TimeOnly horaEmision,
        int idMonedaMaestro, decimal? tipoCambio, int idTipoOperacionMaestro, int? idFormaPago, ClienteDatosEntrada cliente,
        DocumentoAfectadoEntrada? documentoAfectado, IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas,
        IReadOnlyList<CuotaDocumentoElectronicoEntrada> cuotas, IReadOnlyList<CampoExtraEntrada> camposExtra,
        CancellationToken cancellationToken);

    Task<ResultadoOperacion<DocumentoElectronicoDetalle>> ObtenerAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken);

    /// Lectura exclusiva de TokenPublico, para el generador de PDF (URL de verificación en la leyenda) —
    /// no forma parte de DocumentoElectronico/ObtenerAsync a propósito (ese no debe exponerlo vía API).
    Task<ResultadoOperacion<string>> ObtenerTokenPublicoAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken);

    /// Camino inverso a ObtenerTokenPublicoAsync: dado el token público (el "código de verificación" del
    /// PDF), devuelve el documento — puerta de entrada de la verificación pública (link con solo el token,
    /// sin idInquilino, sin autenticación). Proyección pública sin ningún Id* interno, ver
    /// SP_DocumentoElectronico_ObtenerPorToken y DocumentoElectronicoDetallePublico.
    Task<ResultadoOperacion<DocumentoElectronicoDetallePublico>> ObtenerPorTokenAsync(
        string tokenPublico, CancellationToken cancellationToken);

    /// Cliente + líneas de un documento ya emitido, sin resolver los Num1 contra TABLA_MAESTRA — para
    /// prellenar/listar ambos al armar una Nota de Crédito/Débito contra ese documento.
    Task<ResultadoOperacion<DatosParaNota>> ObtenerParaNotaAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken);

    Task<ResultadoOperacion<ResultadoPaginado<DocumentoElectronicoResumen>>> ListarAsync(
        int idInquilino, int idEmpresa, string? estadoCodigo, string? busqueda, DateOnly? fechaDesde, DateOnly? fechaHasta,
        int numeroPagina, int tamanoPagina, CancellationToken cancellationToken);

    /// Exclusivo para el listado que maximlian3_backend expone desde PedidoFactura — ver
    /// SP_DocumentoElectronico_ListarParaPedidoFactura.
    Task<ResultadoOperacion<ResultadoPaginado<FacturaResumenPedidoFactura>>> ListarParaPedidoFacturaAsync(
        int idInquilino, int idEmpresa, string? estadoCodigo, int? idFormaPago, DateOnly? fechaDesde, DateOnly? fechaHasta,
        string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken);

    /// Documentos de un período (cualquier fecha del mes) ya listos para el generador del TXT SIRE RVIE —
    /// ver SP_DocumentoElectronico_ListarParaSireRvie y SIRE_RVIE_Estructura_Campos.md. Sin paginación: un
    /// período se exporta entero.
    Task<ResultadoOperacion<IReadOnlyList<DocumentoSireRvie>>> ListarParaSireRvieAsync(
        int idInquilino, int idEmpresa, DateOnly periodo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<EstadoDocumentoElectronicoActualizado>> ActualizarEstadoSunatAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, EstadoMaestroCodigo estadoCodigo, string? sunatHash,
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, string? sunatTicket, DateTime fecha, CancellationToken cancellationToken);

    /// Marca un documento como AnuladoManualmente (15) — para cuando SUNAT ya muestra el documento como
    /// anulado (p.ej. anulado directo en su portal) sin que este sistema haya tramitado esa baja por su
    /// propia Comunicación de Baja. A diferencia de ActualizarEstadoSunatAsync (uso exclusivo del Worker,
    /// refleja una respuesta real de SUNAT), este es el único camino para que un usuario registre
    /// manualmente una anulación ya ocurrida — el SP valida elegibilidad (solo Aceptado/
    /// AceptadoConObservaciones, sin otra anulación en curso o ya registrada) en vez de confiar ciegamente
    /// en el llamador. fechaAnulacion es la fecha real en que ocurrió (normalmente se descubre después),
    /// la decide el llamador — no "ahora". Arrastra automáticamente las Notas de Crédito/Débito vigentes de
    /// idDocumentoElectronico (mismo criterio que SP_LoteDocumento_Insertar para la baja real) — la lista
    /// devuelta incluye el documento padre y toda Nota arrastrada, no solo el que se pasó.
    Task<ResultadoOperacion<IReadOnlyList<EstadoDocumentoElectronicoActualizado>>> AnularManualmenteAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, string motivo, DateTime fechaAnulacion,
        CancellationToken cancellationToken);

    /// Solo lectura — corre las mismas validaciones que AnularManualmenteAsync (documento elegible, sin Nota
    /// de Crédito/Débito vigente con desenlace todavía desconocido) sin escribir nada, para que el llamador
    /// sepa de antemano si la anulación manual va a poder ejecutarse y qué documentos afectaría (el propio +
    /// las Notas vigentes que se arrastrarían). Ver SP_DocumentoElectronico_PrevisualizarAnulacionManual.
    Task<ResultadoOperacion<IReadOnlyList<DocumentoAnulacionManualPreview>>> PrevisualizarAnulacionManualAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken);

    Task<ResultadoOperacion<bool>> ActualizarFechaEmisionAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico,
        DateOnly fechaEmision, TimeOnly horaEmision, CancellationToken cancellationToken);

    /// Reserva previa al envío, para los 4 tipos de documento: si pasa, marca el documento como Enviando —
    /// evita que un reintento concurrente del mismo documento se envíe dos veces en paralelo. Para Factura/
    /// Boleta es solo eso. Para Nota de Crédito/Débito, además revalida bajo lock que el documento afectado
    /// siga Aceptado/no anulado y que la moneda coincida; para Nota de Crédito específicamente también
    /// revalida el saldo disponible. Ver SP_DocumentoElectronico_ValidarSaldoNotaCredito (el nombre quedó
    /// del alcance original, acotado solo a Nota de Crédito, antes de extenderse a los otros 3 tipos).
    Task<ResultadoOperacion<bool>> ValidarSaldoNotaCreditoAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken);

    /// "Guardar cambios" en lote: reemplaza el diseño anterior de 6 endpoints granulares (Agregar/Actualizar/
    /// Eliminar por línea/cuota) — el llamador manda el estado final deseado de líneas y cuotas, y el SP
    /// calcula el diff (insertar/actualizar/dar de baja) en una sola transacción.
    /// idMotivoMaestro: solo aplica a Nota de Crédito/Débito (null en Factura/Boleta) — a diferencia de
    /// documentoAfectado/idDocumentoElectronicoRelacionado (fijo desde Insertar), el motivo sí es editable
    /// mientras el documento siga PendienteEnvio.
    /// idExterno: IdExterno solo se llenaba al crear el documento y quedaba obsoleto en cuanto las líneas
    /// cambiaban (en maximlian3_backend es el join de los IdPedido detrás de cada línea) — el llamador debe
    /// mandar el valor ya recalculado con las líneas actuales, igual que ya hace al insertar.
    Task<ResultadoOperacion<DocumentoElectronicoCambiosGuardados>> GuardarCambiosAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, string idExterno, int? idFormaPago, string? numeroReferencia,
        int idMonedaMaestro, decimal? tipoCambio, int idTipoOperacionMaestro, int? idMotivoMaestro,
        IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas, IReadOnlyList<CuotaDocumentoElectronicoEntrada> cuotas,
        IReadOnlyList<CampoExtraEntrada> camposExtra, CancellationToken cancellationToken);

    /// Marca el estado de pago de una cuota (Pendiente/Pagado) — transición independiente del EstadoCodigo
    /// del documento, puede ocurrir mucho después de que el documento ya fue aceptado por SUNAT. fechaPago
    /// debe ser coherente con estadoCuotaCodigo: NULL si Pendiente, obligatoria si Pagado (permite registrar
    /// la fecha real de un pago pasado, no siempre "ahora").
    Task<ResultadoOperacion<CuotaDocumentoElectronico>> ActualizarEstadoCuotaAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idCuotaDocumentoElectronico,
        EstadoCuotaCodigo estadoCuotaCodigo, DateTime? fechaPago, CancellationToken cancellationToken);

    /// Para que maximlian3_backend sincronice PEDIDO_FACTURA sondeando EVENTOS_DOCUMENTO desde un checkpoint
    /// (IdEventoDocumento, monótono — sin comparar fechas). EsAnulacion distingue un Rechazado de sendBill de
    /// uno de Comunicación de Baja, ya que SUNAT usa el mismo código para ambos.
    Task<ResultadoOperacion<IReadOnlyList<EventoDocumentoReciente>>> ListarEventosRecientesAsync(
        int idInquilino, int ultimoIdEvento, CancellationToken cancellationToken);

    /// Dashboard de PedidoFactura en maximlian3_backend — reemplaza el cálculo que vivía ahí sobre
    /// TARIFARIO. Ver SP_DocumentoElectronico_ObtenerResumenFacturacion para el criterio completo (neto de
    /// Notas, convertido a PEN, con el chequeo de fecha del documento afectado).
    Task<ResultadoOperacion<ResumenFacturacion>> ObtenerResumenFacturacionAsync(
        int idInquilino, int idEmpresa, DateOnly? fechaDesde, DateOnly? fechaHasta, CancellationToken cancellationToken);
}
