namespace ms_facturacion.Dominio;

/// Proyección pública (verificación por token, endpoint anónimo) de DocumentoElectronicoDetalle — sin
/// ningún Id* interno (IdDocumentoElectronico, IdEmpresa, IdLineaDocumentoElectronico,
/// IdCuotaDocumentoElectronico, IdDocumentoElectronicoRelacionado). Ver SP_DocumentoElectronico_ObtenerPorToken.
public sealed record DocumentoElectronicoDetallePublico(
    DocumentoElectronicoPublico Cabecera,
    IReadOnlyList<LineaDocumentoElectronicoPublica> Lineas,
    ReferenciaDocumentoElectronicaPublica? Referencia,
    IReadOnlyList<CuotaDocumentoElectronicaPublica> Cuotas);

public sealed record DocumentoElectronicoPublico(
    string? NumeroReferencia, string TipoDocumentoCodigo, string Serie, int Correlativo, string EstadoCodigo,
    DateOnly FechaEmision, TimeOnly HoraEmision, string MonedaCodigo, decimal? TipoCambio, string TipoOperacionCodigo, string? FormaPagoCodigo,
    string EmpresaRuc, string EmpresaRazonSocial, string? EmpresaNombreComercial, string EmpresaDireccion, string EmpresaUbigeo,
    string ClienteTipoDocumentoCodigo, string ClienteNumeroDocumento, string ClienteNombre, string? ClienteDireccion,
    string? ClienteCorreo, string ClientePaisCodigo,
    decimal TotalGravado, decimal TotalInafecto, decimal TotalExonerado, decimal TotalExportacion, decimal TotalIgv,
    decimal TotalIsc, decimal TotalOtrosTributos, decimal TotalDescuento, decimal TotalCargo, decimal TotalImporte,
    string? SunatHash, string? SunatCodigoRespuesta, string? SunatDescripcionRespuesta,
    DateTime? FechaAceptacion, DateTime? FechaRechazo, DateTime? FechaAnulacion, DateTime FchCre);

public sealed record LineaDocumentoElectronicoPublica(
    int NumeroLinea, string? ProductoCodigo, string? ProductoSunatCodigo, string Descripcion, string UnidadMedidaCodigo,
    decimal Cantidad, decimal ValorUnitario, decimal PrecioUnitario, decimal MontoDescuento, string AfectacionIgvCodigo,
    string TributoSunatCodigo, string TributoNombre, string TributoTaxTypeCode, string TributoCategoria,
    decimal PorcentajeIgv, decimal MontoIgv, decimal MontoIsc, decimal MontoOtrosTributos, decimal ValorLinea, decimal TotalLinea);

public sealed record ReferenciaDocumentoElectronicaPublica(
    string TipoDocumentoRelacionadoCodigo, string SerieRelacionada, int CorrelativoRelacionado,
    string MotivoCodigo, string MotivoDescripcion);

public sealed record CuotaDocumentoElectronicaPublica(
    int NumeroCuota, DateOnly FechaVencimiento, decimal Monto, string EstadoCuotaCodigo, DateTime? FechaPago);
