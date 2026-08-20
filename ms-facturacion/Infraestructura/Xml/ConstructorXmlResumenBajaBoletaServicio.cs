using System.Xml.Linq;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Xml;

/// Construye SummaryDocuments "solo anulación" (todo ítem con Status=3) para el Resumen Diario de Baja de
/// Boletas — raíz/namespace distintos de VoidedDocuments (ConstructorXmlBajaServicio), aunque comparte el
/// mismo patrón ext:UBLExtensions/cac:Signature/cac:AccountingSupplierParty ya resuelto ahí.
///
/// NO verificado todavía contra la guía SUNAT primaria (Guía XML Resumen de Boletas — la conexión directa a
/// contenido.app.sunat.gob.pe falló durante la investigación de este pase; se basó en fe-primer.greenter.dev,
/// que documenta el mismo estándar de forma secundaria). Dos puntos pendientes de confirmar antes de usar
/// esto contra SUNAT real:
///   1. sac:VoidReasonDescription para el motivo de la línea — se asume el mismo nombre de elemento que ya
///      usa VoidedDocumentsLine; no confirmado que SummaryDocumentsLine use el mismo nombre.
///   2. Si SUNAT exige totales (TotalAmount/TaxTotal) también en una línea anulada — ItemLoteDocumentoDetalle
///      no trae esos datos hoy (SP_LoteDocumento_Obtener nunca los seleccionó, ni falta para VoidedDocuments);
///      si el punto 2 resulta cierto, este constructor y esa consulta necesitan extenderse antes de poder
///      enviar un resumen real.
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
            new XElement(Cbc + "CustomizationID", "1.0"),
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

    /// Status=3 fijo — este constructor solo se usa para el camino "solo anulación" (nunca declara altas/
    /// modificaciones, status 1/2). ID = Serie-Correlativo combinado, no Serial/Number separados como en
    /// VoidedDocumentsLine.
    private XElement ConstruirLinea(ItemLoteDocumentoDetalle item) =>
        new(Sac + "SummaryDocumentsLine",
            new XElement(Cbc + "LineID", item.NumeroLinea),
            new XElement(Cbc + "DocumentTypeCode", item.TipoDocumentoCodigo),
            new XElement(Cbc + "ID", $"{item.Serie}-{item.Correlativo}"),
            new XElement(Sac + "Status",
                new XElement(Cbc + "StatusCode", "3")),
            new XElement(Sac + "VoidReasonDescription", item.MotivoDescripcion));
}
