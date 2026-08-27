namespace ms_facturacion.Dominio;

/// Fila de SP_Facturacion_ObtenerEvolucion — serie temporal de facturación (solo Factura/Boleta)
/// para el dashboard de Facturación Analítica del Gerente en maximlian3_backend. Periodo es la
/// clave de agrupación cruda ("2026-01-08" | "2026-W03" | "2026-01" | "2026"); Etiqueta ya viene
/// formateada desde el SP (dd/mm | Sem 01 | Mes Año | Año, según granularidad) — el llamador no
/// formatea fechas.
public sealed record EvolucionFacturacion(string Periodo, string Etiqueta, int CantidadPedidos, decimal MontoFacturado);
