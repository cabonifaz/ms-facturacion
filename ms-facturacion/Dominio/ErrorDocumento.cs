namespace ms_facturacion.Dominio;

/// Un error/observación normalizado del CDR — solo escritura en este pase.
public sealed record ErrorDocumento(
    int IdDocumentoElectronico, int? IdTransmisionSunat, string OrigenErrorCodigo, string CodigoError,
    string MensajeError, string? Campo, string SeveridadCodigo);
