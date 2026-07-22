using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

/// Arma el XML UBL 2.1 (Invoice) sin firmar para Factura/Boleta (01/03) — ver
/// facturacion/payload_input_output_sunat.md §2 para el template exacto que sigue.
public interface IConstructorXmlFacturaServicio
{
    byte[] Construir(DocumentoElectronicoDetalle documento, Empresa empresa);
}
