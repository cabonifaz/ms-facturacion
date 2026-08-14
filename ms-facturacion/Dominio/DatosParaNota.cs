namespace ms_facturacion.Dominio;

/// Cliente + listado de productos de un documento ya emitido, sin resolver ni exponer Id* — para
/// prellenar/listar ambos al armar una Nota de Crédito/Débito contra ese documento (ver
/// SP_DocumentoElectronico_ObtenerParaNota). El listado de productos es solo referencia (código de cada
/// línea) para que el usuario sepa qué había en el documento original — no se copian cantidad/precio/IGV.
/// IdMonedaMaestro sí se resuelve/expone (a diferencia del resto de Id* del documento): la Nota debe
/// compartir la moneda del documento afectado (obligatorio por SUNAT), el llamador la necesita para prellenarla.
public sealed record DatosParaNota(ClienteDatosEntrada Cliente, int IdMonedaMaestro, IReadOnlyList<ProductoDocumentoResumen> Productos);

public sealed record ProductoDocumentoResumen(int NumeroLinea, string ProductoCodigo);
