namespace ms_facturacion.Dominio;

/// Agrega los 5 result sets de SP_DocumentoElectronico_Obtener (cabecera, líneas, referencia, cuotas, campos extra).
public sealed record DocumentoElectronicoDetalle(
    DocumentoElectronico Cabecera,
    IReadOnlyList<LineaDocumentoElectronico> Lineas,
    ReferenciaDocumentoElectronico? Referencia,
    IReadOnlyList<CuotaDocumentoElectronico> Cuotas,
    IReadOnlyList<CampoExtraEntrada> CamposExtra);
