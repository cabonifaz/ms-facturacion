namespace ms_facturacion.Dominio;

/// Lo que devuelve el cliente SOAP ya interpretado: el CDR decodificado y su veredicto.
/// EstadoCodigo ya viene mapeado a uno de los valores de TABLA_MAESTRA IdMaestro=1
/// (Aceptado / AceptadoConObservaciones / Rechazado) según el rango de SunatCodigoRespuesta
/// (0 / 4000+ / 2000-3999 — ver flujo_tablas_microservicio_facturacion_sunat.md §10).
/// Observaciones son todos los cbc:Note del CDR (además del cac:Response/cbc:Description principal) — un
/// "AceptadoConObservaciones" puede traer varias, y antes se descartaban todas menos la Description.
public sealed record ResultadoEnvioSunat(
    EstadoMaestroCodigo EstadoCodigo, string SunatCodigoRespuesta, string SunatDescripcionRespuesta,
    IReadOnlyList<string> Observaciones, byte[] CdrZipBytes, byte[] CdrXmlBytes);
