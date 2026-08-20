using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

/// Arma el SummaryDocuments UBL (namespace propio de SUNAT, "solo anulación": todo ítem va con Status=3)
/// sin firmar, para el Resumen Diario de Baja de Boletas — distinto de IConstructorXmlBajaServicio
/// (VoidedDocuments/"RA-", Factura/NC/ND): SUNAT exige este mecanismo para anular una Boleta, no
/// Comunicación de Baja. No verificado todavía contra la guía SUNAT primaria (Guía XML Resumen de
/// Boletas) — ver el comentario de Control de Cambios en SP_LoteResumenBajaBoleta_Insertar.
public interface IConstructorXmlResumenBajaBoletaServicio
{
    byte[] Construir(LoteDocumentoDetalle lote, Empresa empresa);
}
