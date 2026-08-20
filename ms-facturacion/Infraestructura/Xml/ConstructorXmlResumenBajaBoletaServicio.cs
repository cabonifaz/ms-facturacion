using System.Globalization;
using System.Xml.Linq;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Xml;

/// Construye SummaryDocuments "solo anulación" (todo ítem con Status=3) para el Resumen Diario de Baja de
/// Boletas — raíz/namespace distintos de VoidedDocuments (ConstructorXmlBajaServicio), aunque comparte el
/// mismo patrón ext:UBLExtensions/cac:Signature/cac:AccountingSupplierParty ya resuelto ahí.
///
/// Verificado contra UBLPE-SunatAggregateComponents-1.0.xsd / UBL-CommonAggregateComponents-2.0.xsd
/// (github.com/giansalex/sunat-sfs) tras el primer rechazo real de SUNAT (cvc-particle 2.1 en
/// SummaryDocumentsLine), y contra el XML de ejemplo real de la Guía SUNAT (GUIA_Resumen_de_Boletas,
/// cpe.sunat.gob.pe) tras el segundo rechazo (error 2072, CustomizationID) — ambas fuentes primarias, no
/// fe-primer.greenter.dev (secundaria, usada en la versión original). Los dos puntos pendientes de esa
/// primera versión ya están resueltos: SummaryDocumentsLineType no tiene VoidReasonDescription (es de
/// VoidedDocumentsLineType) y sí exige TotalAmount. Salieron tres errores más que la fuente secundaria no
/// reflejaba: el wrapper de status es cac:Status (no sac:Status) con hijo cbc:ConditionCode (no
/// cbc:StatusCode, que StatusType ni declara); y CustomizationID es "1.1" (no "1.0", el valor de
/// VoidedDocuments/Comunicación de Baja — Resumen usa una versión de estructura distinta).
public sealed class ConstructorXmlResumenBajaBoletaServicio : IConstructorXmlResumenBajaBoletaServicio
{
    private static readonly XNamespace Summary = "urn:sunat:names:specification:ubl:peru:schema:xsd:SummaryDocuments-1";
    private static readonly XNamespace Sac = "urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";
    private static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

    public byte[] Construir(LoteDocumentoDetalle lote, Empresa empresa)
    {
        var cabecera = lote.Cabecera;

        var raiz = new XElement(Summary + "SummaryDocuments",
            new XAttribute(XNamespace.Xmlns + "cac", Cac.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "ds", Ds.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "ext", Ext.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "sac", Sac.NamespaceName),

            new XElement(Ext + "UBLExtensions",
                new XElement(Ext + "UBLExtension",
                    new XElement(Ext + "ExtensionContent"))),

            new XElement(Cbc + "UBLVersionID", "2.0"),
            new XElement(Cbc + "CustomizationID", "1.1"), // confirmado contra el ejemplo XML real de la Guía SUNAT (ver comentario de clase) — "1.0" (error SUNAT 2072) fue un supuesto sin verificar de la primera versión
            new XElement(Cbc + "ID", cabecera.Nombre), // "RC-<fecha>-<correlativo>"
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

    /// Mismo patrón que ConstructorXmlBajaServicio.ConstruirFirma — sin duplicar razonamiento acá.
    private XElement ConstruirFirma(string ruc, string razonSocial) =>
        new(Cac + "Signature",
            new XElement(Cbc + "ID", ruc),
            new XElement(Cac + "SignatoryParty",
                new XElement(Cac + "PartyIdentification", new XElement(Cbc + "ID", ruc)),
                new XElement(Cac + "PartyName", new XElement(Cbc + "Name", razonSocial))),
            new XElement(Cac + "DigitalSignatureAttachment",
                new XElement(Cac + "ExternalReference",
                    new XElement(Cbc + "URI", $"#SIGN-{ruc}"))));

    private XElement ConstruirProveedor(Empresa empresa) =>
        new(Cac + "AccountingSupplierParty",
            new XElement(Cbc + "CustomerAssignedAccountID", empresa.Ruc),
            new XElement(Cbc + "AdditionalAccountID", "6"),
            new XElement(Cac + "Party",
                new XElement(Cac + "PartyLegalEntity",
                    new XElement(Cbc + "RegistrationName", empresa.RazonSocial))));

    /// Status=3 fijo (cac:Status/cbc:ConditionCode, no sac:Status/cbc:StatusCode — StatusType no declara
    /// StatusCode) — este constructor solo se usa para el camino "solo anulación" (nunca declara altas/
    /// modificaciones, status 1/2). ID = Serie-Correlativo combinado, no Serial/Number separados como en
    /// VoidedDocumentsLine. Sin VoidReasonDescription: SummaryDocumentsLineType no lo declara (es de
    /// VoidedDocumentsLineType); MotivoDescripcion sigue quedando en BD, solo no va en este XML.
    /// sac:TotalAmount es obligatorio en la secuencia (a diferencia de VoidedDocumentsLine, que no lo pide).
    private XElement ConstruirLinea(ItemLoteDocumentoDetalle item) =>
        new(Sac + "SummaryDocumentsLine",
            new XElement(Cbc + "LineID", item.NumeroLinea),
            new XElement(Cbc + "DocumentTypeCode", item.TipoDocumentoCodigo),
            new XElement(Cbc + "ID", $"{item.Serie}-{item.Correlativo}"),
            new XElement(Cac + "Status",
                new XElement(Cbc + "ConditionCode", "3")),
            new XElement(Sac + "TotalAmount",
                new XAttribute("currencyID", item.MonedaCodigo),
                item.TotalImporte.ToString("F2", CultureInfo.InvariantCulture)));
}
