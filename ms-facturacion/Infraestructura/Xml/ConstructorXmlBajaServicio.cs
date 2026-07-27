using System.Xml.Linq;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Xml;

/// Construye VoidedDocuments (Comunicación de Baja) — verificado contra la guía oficial de SUNAT
/// (Guia+XML+Comunicacion+de+Baja+revisado.pdf, extraída con PyPDF2, no es una convención asumida).
/// No comparte código con ConstructorXmlComprobanteServicio: raíz, namespace y estructura del emisor
/// son completamente distintos (aquí el RUC va en cbc:CustomerAssignedAccountID, no en PartyIdentification).
public sealed class ConstructorXmlBajaServicio : IConstructorXmlBajaServicio
{
    private static readonly XNamespace Voided = "urn:sunat:names:specification:ubl:peru:schema:xsd:VoidedDocuments-1";
    private static readonly XNamespace Sac = "urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";
    private static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

    public byte[] Construir(LoteDocumentoDetalle lote, Empresa empresa)
    {
        var cabecera = lote.Cabecera;

        var raiz = new XElement(Voided + "VoidedDocuments",
            new XAttribute(XNamespace.Xmlns + "cac", Cac.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "ds", Ds.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "ext", Ext.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "sac", Sac.NamespaceName),

            new XElement(Ext + "UBLExtensions",
                new XElement(Ext + "UBLExtension",
                    new XElement(Ext + "ExtensionContent"))),

            new XElement(Cbc + "UBLVersionID", "2.0"),
            new XElement(Cbc + "CustomizationID", "1.0"),
            new XElement(Cbc + "ID", cabecera.Nombre),
            new XElement(Cbc + "ReferenceDate", cabecera.FechaReferencia.ToString("yyyy-MM-dd")),
            new XElement(Cbc + "IssueDate", DateOnly.FromDateTime(cabecera.FechaGeneracion).ToString("yyyy-MM-dd")),

            ConstruirFirma(empresa.Ruc, empresa.RazonSocial),
            ConstruirProveedor(empresa));

        foreach (var item in lote.Items)
        {
            raiz.Add(ConstruirLinea(item));
        }

        var xDocument = new XDocument(new XDeclaration("1.0", "UTF-8", null), raiz);

        using var memoria = new MemoryStream();
        xDocument.Save(memoria);
        return memoria.ToArray();
    }

    private XElement ConstruirFirma(string ruc, string razonSocial) =>
        new(Cac + "Signature",
            new XElement(Cbc + "ID", ruc),
            new XElement(Cac + "SignatoryParty",
                new XElement(Cac + "PartyIdentification", new XElement(Cbc + "ID", ruc)),
                new XElement(Cac + "PartyName", new XElement(Cbc + "Name", razonSocial))),
            new XElement(Cac + "DigitalSignatureAttachment",
                new XElement(Cac + "ExternalReference",
                    new XElement(Cbc + "URI", $"#SIGN-{ruc}"))));

    /// Orden distinto al de Invoice: aquí el RUC va en cbc:CustomerAssignedAccountID (no en
    /// cac:PartyIdentification), y el tipo de documento del emisor va aparte en cbc:AdditionalAccountID.
    private XElement ConstruirProveedor(Empresa empresa) =>
        new(Cac + "AccountingSupplierParty",
            new XElement(Cbc + "CustomerAssignedAccountID", empresa.Ruc),
            new XElement(Cbc + "AdditionalAccountID", "6"),
            new XElement(Cac + "Party",
                new XElement(Cac + "PartyLegalEntity",
                    new XElement(Cbc + "RegistrationName", empresa.RazonSocial))));

    private XElement ConstruirLinea(ItemLoteDocumentoDetalle item) =>
        new(Sac + "VoidedDocumentsLine",
            new XElement(Cbc + "LineID", item.NumeroLinea),
            new XElement(Cbc + "DocumentTypeCode", item.TipoDocumentoCodigo),
            new XElement(Sac + "DocumentSerialID", item.Serie),
            new XElement(Sac + "DocumentNumberID", item.Correlativo),
            new XElement(Sac + "VoidReasonDescription", item.MotivoDescripcion));
}
