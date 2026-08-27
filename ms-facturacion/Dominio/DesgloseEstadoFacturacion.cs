namespace ms_facturacion.Dominio;

/// Fila de SP_Facturacion_ObtenerDesgloseEstado — top 5 estados (por cantidad) + "Otros" agrupando
/// el resto, para el dashboard de Facturación Analítica del Gerente en maximlian3_backend.
/// IdEstadoMaestro es NULL en la fila "Otros". Estado sale de TABLA_MAESTRA IdMaestro=1 (String3,
/// etiqueta corta) — no incluye ComunicacionBajaAceptada/ErrorSunat/AnuladoManualmente/
/// ResumenBajaAceptado (ya se anularon o fallaron, quedan fuera del conteo).
public sealed record DesgloseEstadoFacturacion(int? IdEstadoMaestro, string Estado, int CantidadFacturas, decimal MontoFacturado);
