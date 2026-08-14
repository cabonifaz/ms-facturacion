namespace ms_facturacion.Dominio;

/// Resultado de SP_DocumentoElectronico_ObtenerResumenFacturacion — CantidadFacturas cuenta solo
/// Factura/Boleta; MontoTotalPEN es neto de Notas (Factura/Boleta + Nota de Débito − Nota de Crédito),
/// siempre convertido a PEN. PromedioIngresoPEN es null cuando CantidadFacturas es 0.
public sealed record ResumenFacturacion(int CantidadFacturas, decimal MontoTotalPEN, decimal? PromedioIngresoPEN);
