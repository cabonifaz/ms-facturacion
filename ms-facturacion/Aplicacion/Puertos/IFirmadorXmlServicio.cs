using System.Security.Cryptography.X509Certificates;

namespace ms_facturacion.Aplicacion.Puertos;

/// Firma XML-DSig enveloped, reubicando ds:Signature dentro de ext:UBLExtensions/.../ext:ExtensionContent
/// tras calcularla — ver la nota técnica en el plan de este módulo.
public interface IFirmadorXmlServicio
{
    byte[] Firmar(byte[] xmlSinFirmar, X509Certificate2 certificado);
}
