namespace ms_facturacion.Dominio;

/// Una fila de ERRORES_DOCUMENTO tal como se expone hacia afuera — ver
/// SP_ErrorDocumento_ListarUltimoEnvio (solo el último intento de envío, no el historial completo).
public sealed record ErrorDocumentoResumen(
    int IdErrorDocumento, string OrigenErrorCodigo, string CodigoError, string MensajeError,
    string? Campo, string SeveridadCodigo, DateTime FchCre);
