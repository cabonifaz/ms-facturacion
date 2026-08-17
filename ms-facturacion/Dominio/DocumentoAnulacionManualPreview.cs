namespace ms_facturacion.Dominio;

/// Fila de SP_DocumentoElectronico_PrevisualizarAnulacionManual — un documento que se vería afectado
/// (el documento indicado, o una Nota de Crédito/Débito vigente que se arrastraría con él) si se ejecutara
/// SP_DocumentoElectronico_AnularManualmente ahora mismo. EstadoCodigo es el estado ACTUAL del documento
/// (Aceptado/AceptadoConObservaciones) — todavía no cambió nada, esto es solo una previsualización.
public sealed record DocumentoAnulacionManualPreview(
    int IdDocumentoElectronico, string TipoDocumentoCodigo, string NumeroDocumento,
    DateOnly FechaEmision, string EstadoCodigo);
