namespace ms_facturacion.Dominio;

/// Proyección liviana para listados — SP_DocumentoElectronico_Listar solo devuelve estas columnas.
public sealed record DocumentoElectronicoResumen(
    int IdDocumentoElectronico, string TipoDocumentoCodigo, string Serie, int Correlativo,
    string EstadoCodigo, string ClienteNombre, decimal TotalImporte, DateOnly FechaEmision);
