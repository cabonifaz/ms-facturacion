namespace ms_facturacion.Dominio;

/// Agrega los 4 result sets de SP_DocumentoElectronico_Obtener (cabecera, líneas, referencia, cuotas).
public sealed record DocumentoElectronicoDetalle(
    DocumentoElectronico Cabecera,
    IReadOnlyList<LineaDocumentoElectronico> Lineas,
    ReferenciaDocumentoElectronico? Referencia,
    IReadOnlyList<CuotaDocumentoElectronico> Cuotas);
