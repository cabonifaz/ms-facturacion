namespace ms_facturacion.Dominio;

/// Proyección liviana para listados — SP_Certificado_Listar solo devuelve estas columnas.
public sealed record CertificadoResumen(
    int IdCertificado, string Sujeto, string NumeroSerie, DateOnly ValidoDesde, DateOnly ValidoHasta, bool Activo);
