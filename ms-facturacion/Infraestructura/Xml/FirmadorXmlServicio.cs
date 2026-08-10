using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Infraestructura.Xml;

/// Firma XML-DSig enveloped con el certificado del emisor. SignedXml siempre agrega el nodo ds:Signature
/// como último hijo del elemento raíz firmado; SUNAT exige que viva dentro de
/// ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent en su lugar. Se calcula la firma normalmente
/// y luego se reubica el nodo ya firmado — el transform "enveloped-signature" excluye cualquier
/// ds:Signature descendiente del cómputo del digest sin importar dónde termine viviendo, así que mover el
/// nodo después de firmar no invalida el digest ni la firma (técnica estándar para UBL-PE).
public sealed class FirmadorXmlServicio(ILogger<FirmadorXmlServicio> logger) : IFirmadorXmlServicio
{
    private static readonly XmlNamespaceManager NamespaceManagerUbl = ConstruirNamespaceManager();

    public byte[] Firmar(byte[] xmlSinFirmar, X509Certificate2 certificado)
    {
        try
        {
            var documento = new XmlDocument { PreserveWhitespace = true };
            using (var lector = new MemoryStream(xmlSinFirmar))
            {
                documento.Load(lector);
            }

            var signedXml = new SignedXml(documento)
            {
                SigningKey = certificado.GetRSAPrivateKey()
                    ?? throw new InvalidOperationException("El certificado no tiene una clave privada RSA utilizable para firmar.")
            };

            var reference = new Reference { Uri = "" };
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            reference.AddTransform(new XmlDsigC14NTransform());
            signedXml.AddReference(reference);

            var keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificado));
            signedXml.KeyInfo = keyInfo;

            signedXml.ComputeSignature();
            var nodoFirma = signedXml.GetXml();

            // Import + relocate: sacar el nodo (venía como último hijo del raíz) y ponerlo dentro de ExtensionContent.
            var nodoFirmaImportado = documento.ImportNode(nodoFirma, true);
            nodoFirma.ParentNode?.RemoveChild(nodoFirma);

            var extensionContent = documento.SelectSingleNode(
                "//ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent", NamespaceManagerUbl)
                ?? throw new InvalidOperationException("El XML no tiene el nodo ext:ExtensionContent esperado para insertar la firma.");

            extensionContent.AppendChild(nodoFirmaImportado);

            using var salida = new MemoryStream();
            documento.Save(salida);
            return salida.ToArray();
        }
        catch (Exception ex)
        {
            // Igual que AlmacenamientoArchivosS3Servicio: este método devuelve byte[], no ResultadoOperacion,
            // así que se loguea y se re-lanza para que el catch general de
            // EnviarDocumentoElectronicoASunatCasoDeUso lo convierta en respuesta con envelope. GetRSAPrivateKey
            // acceder a la clave privada de un X509Certificate2 con EphemeralKeySet es el punto más probable
            // de comportarse distinto entre Windows (dev) y Linux (p.ej. Azure App Service Linux) — sin este
            // log no había forma de distinguir esta falla de cualquier otra en la cadena.
            logger.LogError(ex, "FirmadorXml — falló al firmar el XML.");
            throw;
        }
    }

    private static XmlNamespaceManager ConstruirNamespaceManager()
    {
        var tabla = new NameTable();
        var administrador = new XmlNamespaceManager(tabla);
        administrador.AddNamespace("ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");
        return administrador;
    }
}
