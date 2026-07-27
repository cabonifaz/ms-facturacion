namespace ms_facturacion.Dominio;

/// Resultado de getStatus ya interpretado. CdrXmlBytes es null mientras EstadoCodigo siga "en proceso"
/// (respuesta 98 de SUNAT) — solo viene poblado cuando SUNAT ya terminó de procesar el ticket (respuesta 0).
public sealed record ResultadoConsultaTicket(
    EstadoMaestroCodigo EstadoCodigo, string? SunatCodigoRespuesta, string? SunatDescripcionRespuesta, byte[]? CdrXmlBytes);
