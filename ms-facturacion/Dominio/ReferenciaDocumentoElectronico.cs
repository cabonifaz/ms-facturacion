namespace ms_facturacion.Dominio;

public sealed record ReferenciaDocumentoElectronico(
    int? IdDocumentoElectronicoRelacionado, string TipoDocumentoRelacionadoCodigo, string SerieRelacionada,
    int CorrelativoRelacionado, string MotivoCodigo, string MotivoDescripcion);
