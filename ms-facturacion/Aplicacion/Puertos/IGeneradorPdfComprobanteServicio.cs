using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

/// Construye la representación impresa (PDF) del comprobante — QR + leyendas según Anexo C (RS 113-2018/
/// SUNAT, Aspectos Técnicos) y RS 097-2012/RS 300-2014/SUNAT (contenido mínimo de la representación impresa).
public interface IGeneradorPdfComprobanteServicio
{
    byte[] Construir(DocumentoElectronicoDetalle documento, Empresa empresa, string codigoVerificacion, string? sunatHash);
}
