namespace ms_facturacion.Dominio;

/// Una fila de SP_DocumentoElectronico_ListarParaSireRvie — ya trae todos los campos resueltos que necesita
/// el generador del TXT SIRE RVIE (Formato 14.4, ver SIRE_RVIE_Estructura_Campos.md). Los campos 29-32
/// (documento modificado) solo vienen con valor en Notas de Crédito/Débito; en Factura/Boleta quedan null.
public sealed record DocumentoSireRvie(
    int IdDocumentoElectronico,
    string EmpresaRuc,
    string EmpresaRazonSocial,
    DateOnly FechaEmision,
    string TipoDocumentoCodigo,
    string Serie,
    int Correlativo,
    string ClienteTipoDocumentoCodigo,
    string ClienteNumeroDocumento,
    string ClienteNombre,
    decimal TotalExportacion,
    decimal TotalGravado,
    decimal TotalIgv,
    decimal TotalExonerado,
    decimal TotalInafecto,
    decimal TotalIsc,
    decimal TotalOtrosTributos,
    decimal TotalImporte,
    string MonedaCodigo,
    decimal? TipoCambio,
    DateOnly? FechaEmisionDocModificado,
    string? TipoDocumentoRelacionadoCodigo,
    string? SerieRelacionada,
    int? CorrelativoRelacionado);
