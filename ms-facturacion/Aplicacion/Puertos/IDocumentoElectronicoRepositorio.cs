using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface IDocumentoElectronicoRepositorio
{
    Task<ResultadoOperacion<DocumentoElectronicoCreado>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string idExterno, string? numeroReferencia,
        int idTipoDocumentoMaestro, DateOnly fechaEmision, TimeOnly horaEmision,
        int idMonedaMaestro, decimal? tipoCambio, int idTipoOperacionMaestro, int idFormaPago, ClienteDatosEntrada cliente,
        DocumentoAfectadoEntrada? documentoAfectado, IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas,
        IReadOnlyList<CuotaDocumentoElectronico> cuotas, IReadOnlyList<CampoExtraEntrada> camposExtra,
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

    Task<ResultadoOperacion<ResultadoPaginado<DocumentoElectronicoResumen>>> ListarAsync(
        int idInquilino, int idEmpresa, string? estadoCodigo, string? busqueda, DateOnly? fechaDesde, DateOnly? fechaHasta,
        int numeroPagina, int tamanoPagina, CancellationToken cancellationToken);

    /// Exclusivo para el listado que maximlian3_backend expone desde PedidoFactura — ver
    /// SP_DocumentoElectronico_ListarParaPedidoFactura.
    Task<ResultadoOperacion<ResultadoPaginado<FacturaResumenPedidoFactura>>> ListarParaPedidoFacturaAsync(
        int idInquilino, int idEmpresa, string? estadoCodigo, int? idFormaPago, DateOnly? fechaDesde, DateOnly? fechaHasta,
        string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken);

    Task<ResultadoOperacion<EstadoDocumentoElectronicoActualizado>> ActualizarEstadoSunatAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, EstadoMaestroCodigo estadoCodigo, string? sunatHash,
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, string? sunatTicket, CancellationToken cancellationToken);

    Task<ResultadoOperacion<bool>> ActualizarFechaEmisionAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico,
        DateOnly fechaEmision, TimeOnly horaEmision, CancellationToken cancellationToken);

    /// "Guardar cambios" en lote: reemplaza el diseño anterior de 6 endpoints granulares (Agregar/Actualizar/
    /// Eliminar por línea/cuota) — el llamador manda el estado final deseado de líneas y cuotas, y el SP
    /// calcula el diff (insertar/actualizar/dar de baja) en una sola transacción.
    Task<ResultadoOperacion<DocumentoElectronicoCambiosGuardados>> GuardarCambiosAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idFormaPago, string? numeroReferencia,
        int idMonedaMaestro, decimal? tipoCambio, int idTipoOperacionMaestro,
        IReadOnlyList<LineaDocumentoElectronicoEntrada> lineas, IReadOnlyList<CuotaDocumentoElectronico> cuotas,
        IReadOnlyList<CampoExtraEntrada> camposExtra, CancellationToken cancellationToken);

    /// Marca el estado de pago de una cuota (Pendiente/Pagado) — transición independiente del EstadoCodigo
    /// del documento, puede ocurrir mucho después de que el documento ya fue aceptado por SUNAT.
    Task<ResultadoOperacion<CuotaDocumentoElectronico>> ActualizarEstadoCuotaAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idCuotaDocumentoElectronico,
        EstadoCuotaCodigo estadoCuotaCodigo, CancellationToken cancellationToken);

    /// Para que maximlian3_backend sincronice PEDIDO_FACTURA sondeando EVENTOS_DOCUMENTO desde un checkpoint
    /// (IdEventoDocumento, monótono — sin comparar fechas). EsAnulacion distingue un Rechazado de sendBill de
    /// uno de Comunicación de Baja, ya que SUNAT usa el mismo código para ambos.
    Task<ResultadoOperacion<IReadOnlyList<EventoDocumentoReciente>>> ListarEventosRecientesAsync(
        int idInquilino, int ultimoIdEvento, CancellationToken cancellationToken);
}
