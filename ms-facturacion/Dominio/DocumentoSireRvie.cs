namespace ms_facturacion.Dominio;

/// Una fila de SP_DocumentoElectronico_ListarParaSireRvie — ya trae todos los campos resueltos que necesita
/// el generador del TXT SIRE RVIE (Formato 14.4, ver SIRE_RVIE_Estructura_Campos.md). Los campos 29-32
/// (documento modificado) solo vienen con valor en Notas de Crédito/Débito; en Factura/Boleta quedan null.
/// EstadoAnulacionCodigo (ComunicacionBajaAceptada/AnuladoManualmente/null) no se incluye en el TXT — el
/// campo "Estado del comprobante de pago" de la propuesta RVIE (Anexo N.° 1, RS 112-2021/SUNAT) es solo
/// referencial, SUNAT lo resuelve de su lado vía el CDR de baja que ya recibió. Se expone acá solo para que
/// el mapper/dashboard interno sepa cuáles documentos están anulados, ya que este SP ya no los excluye
/// (SUNAT confirma en sus Preguntas Frecuentes SIRE que un documento anulado/dado de baja sí debe seguir
/// anotado en el RVIE).
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
    string? EstadoAnulacionCodigo,
    DateOnly? FechaEmisionDocModificado,
    string? TipoDocumentoRelacionadoCodigo,
    string? SerieRelacionada,
    int? CorrelativoRelacionado);
