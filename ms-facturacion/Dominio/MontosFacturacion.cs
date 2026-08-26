namespace ms_facturacion.Dominio;

/// Resultado de SP_Facturacion_ObtenerMontosFacturacion — dashboard de Facturación Analítica del
/// Gerente en maximlian3_backend. A diferencia de ResumenFacturacion (que neteaba Factura/Boleta +
/// Nota de Débito − Nota de Crédito en un solo MontoTotalPEN), acá van separados: 3 tarjetas
/// distintas del dashboard. TotalNotasCredito es negativo (pérdida), TotalNotasDebito positivo
/// (ganancia) — mismo criterio +Ganancia/-Pérdida. Siempre en PEN, MonedaIcono fijo ('S/').
public sealed record MontosFacturacion(decimal TotalFacturado, decimal TotalNotasCredito, decimal TotalNotasDebito, string MonedaIcono);
