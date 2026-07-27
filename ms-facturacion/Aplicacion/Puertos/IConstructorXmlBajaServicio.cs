using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

/// Arma el VoidedDocuments UBL (namespace propio de SUNAT, no UBL estándar) sin firmar para una
/// Comunicación de Baja — ver Guia+XML+Comunicacion+de+Baja+revisado.pdf (SUNAT) para el mapeo exacto.
public interface IConstructorXmlBajaServicio
{
    byte[] Construir(LoteDocumentoDetalle lote, Empresa empresa);
}
