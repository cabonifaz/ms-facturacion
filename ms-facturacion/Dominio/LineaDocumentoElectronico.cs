namespace ms_facturacion.Dominio;

/// Línea tal como queda persistida (con montos ya calculados por el SP) — usada en la salida de Obtener.
public sealed record LineaDocumentoElectronico(
    int IdLineaDocumentoElectronico, int NumeroLinea, string ProductoCodigo, string? ProductoSunatCodigo,
    string Descripcion, string UnidadMedidaCodigo, decimal Cantidad, decimal ValorUnitario, decimal PrecioUnitario,
    decimal MontoDescuento, string AfectacionIgvCodigo, decimal PorcentajeIgv, decimal MontoIgv, decimal MontoIsc,
    decimal MontoOtrosTributos, decimal ValorLinea, decimal TotalLinea);

/// Línea tal como la envía el llamador (Versión B del payload) — el SP calcula los montos, no se reciben.
public sealed record LineaDocumentoElectronicoEntrada(
    int NumeroLinea, string ProductoCodigo, string? ProductoSunatCodigo, string Descripcion, string UnidadMedidaCodigo,
    decimal Cantidad, decimal ValorUnitario, decimal PrecioUnitario, decimal MontoDescuento,
    string AfectacionIgvCodigo, decimal PorcentajeIgv);
