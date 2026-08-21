namespace ms_facturacion.Dominio;

/// Línea tal como queda persistida (con montos ya calculados por el SP) — usada en la salida de Obtener.
/// TributoSunatCodigo/TributoNombre/TributoTaxTypeCode/TributoCategoria (Catálogo N.° 05 SUNAT) se
/// resuelven en el SP a partir de AfectacionIgvCodigo (Catálogo N.° 07) — ver
/// facturacion/catalogos_sunat_referencia.md.
/// IdPedido: solo en Factura/Boleta (desarmado de DOCUMENTOS_ELECTRONICOS.IdExterno por posición contra
/// NumeroLinea — ver SP_DocumentoElectronico_Obtener) — null en Nota de Crédito/Débito, cuyo IdExterno es
/// el id del documento afectado, no una lista de pedidos.
public sealed record LineaDocumentoElectronico(
    int IdLineaDocumentoElectronico, int NumeroLinea, int? IdPedido, string? ProductoCodigo, string? ProductoSunatCodigo,
    string Descripcion, string UnidadMedidaCodigo, decimal Cantidad, decimal ValorUnitario, decimal PrecioUnitario,
    decimal MontoDescuento, string AfectacionIgvCodigo,
    string TributoSunatCodigo, string TributoNombre, string TributoTaxTypeCode, string TributoCategoria,
    decimal PorcentajeIgv, decimal MontoIgv, decimal MontoIsc,
    decimal MontoOtrosTributos, decimal ValorLinea, decimal TotalLinea);

/// Línea tal como la envía el llamador (Versión B del payload, y también "Guardar cambios" en lote) — el SP
/// calcula los montos, no se reciben. IdLineaDocumentoElectronico solo se usa en Guardar cambios: 0 = línea
/// nueva, >0 = línea existente a actualizar. IdUnidadMedidaMaestro es Num1 de TABLA_MAESTRA IdMaestro=13
/// (subconjunto cerrado del Catálogo N.° 03 SUNAT).
public sealed record LineaDocumentoElectronicoEntrada(
    int NumeroLinea, string? ProductoCodigo, string? ProductoSunatCodigo, string Descripcion, int IdUnidadMedidaMaestro,
    decimal Cantidad, decimal ValorUnitario, decimal MontoDescuento,
    int IdAfectacionIgvMaestro, decimal PorcentajeIgv, int IdLineaDocumentoElectronico = 0);
