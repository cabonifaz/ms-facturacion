namespace ms_facturacion.Dominio;

public sealed record LoteDocumento(
    int IdLoteDocumento, int IdEmpresa, string TipoLoteCodigo, string Nombre, DateOnly FechaReferencia,
    DateTime FechaGeneracion, string EstadoCodigo, string? Ticket, string? SunatCodigoRespuesta,
    string? SunatDescripcionRespuesta);

public sealed record ItemLoteDocumentoDetalle(
    int IdItemLoteDocumento, int IdDocumentoElectronico, int NumeroLinea, string MotivoDescripcion,
    string EstadoItemCodigo, string TipoDocumentoCodigo, string Serie, int Correlativo);

public sealed record LoteDocumentoDetalle(LoteDocumento Cabecera, IReadOnlyList<ItemLoteDocumentoDetalle> Items);

/// Input para SP_LoteDocumento_Insertar — un documento a incluir en la comunicación de baja.
public sealed record ItemBajaEntrada(int IdDocumentoElectronico, string MotivoDescripcion);

public sealed record LoteDocumentoCreado(int IdLoteDocumento, string Nombre, string EstadoCodigo, DateTime FechaGeneracion);

/// Fila de SP_LoteDocumento_ListarPendientesTicket — lotes en TicketRecibido/TicketPendiente, sin
/// scope de inquilino (usado por el worker que resuelve tickets, no por una request HTTP de un tenant).
public sealed record LotePendienteTicket(int IdInquilino, int IdLoteDocumento, string? Ticket);

public sealed record ResultadoResolucionTicket(int IdInquilino, int IdLoteDocumento, bool Exito, string Mensaje);

/// Fila de SP_LoteDocumento_PrevisualizarBaja — un documento que se vería incluido (uno de los indicados, o
/// una Nota de Crédito/Débito vigente que se arrastraría con su Factura/Boleta) si se ejecutara
/// SP_LoteDocumento_Insertar ahora mismo. EstadoCodigo es el estado ACTUAL del documento — todavía no
/// cambió nada, esto es solo una previsualización.
public sealed record DocumentoBajaPreview(
    int IdDocumentoElectronico, string TipoDocumentoCodigo, string NumeroDocumento,
    DateOnly FechaEmision, string EstadoCodigo);
