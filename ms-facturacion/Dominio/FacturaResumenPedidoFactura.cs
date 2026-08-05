namespace ms_facturacion.Dominio;

/// Proyección exclusiva para el listado que maximlian3_backend expone desde PedidoFactura — no reutiliza
/// DocumentoElectronicoResumen porque ese es de SP_DocumentoElectronico_Listar (uso interno de
/// ms-facturación, distinto shape/filtros). Ver SP_DocumentoElectronico_ListarParaPedidoFactura.
public sealed record FacturaResumenPedidoFactura(
    int IdDocumentoElectronico, string NumeroFactura, string ClienteNombre, DateOnly FechaEmision,
    string FormaPagoCodigo, decimal TotalImporte, string EstadoCodigo, string ColorLetra, string ColorFondo);
