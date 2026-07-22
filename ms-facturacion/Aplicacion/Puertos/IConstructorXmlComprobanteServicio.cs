using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

/// Arma el XML UBL 2.1 sin firmar para Factura/Boleta (01/03 → Invoice) y Nota de Crédito/Débito
/// (07 → CreditNote, 08 → DebitNote) — ver facturacion/payload_input_output_sunat.md §2 para Invoice y
/// el Anexo VII (anexoVII-114-2019.pdf, num. 6-7) para el mapeo de cac:DiscrepancyResponse.
public interface IConstructorXmlComprobanteServicio
{
    byte[] Construir(DocumentoElectronicoDetalle documento, Empresa empresa);
}
