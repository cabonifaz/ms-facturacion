namespace ms_facturacion.Dominio;

/// Lo que devuelve el cliente SOAP ya interpretado: el CDR decodificado y su veredicto.
/// EstadoCodigo ya viene mapeado a uno de los valores de TABLA_MAESTRA IdMaestro=1
/// (Aceptado / AceptadoConObservaciones / Rechazado) según el rango de SunatCodigoRespuesta
/// (0 / 4000+ / 2000-3999 — ver flujo_tablas_microservicio_facturacion_sunat.md §10).
public sealed record ResultadoEnvioSunat(
    string EstadoCodigo, string SunatCodigoRespuesta, string SunatDescripcionRespuesta, byte[] CdrZipBytes, byte[] CdrXmlBytes);
