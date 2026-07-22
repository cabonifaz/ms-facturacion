using System.Xml.Linq;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Xml;

/// Construye el Invoice UBL 2.1 sin firmar para Factura/Boleta (01/03) — sigue el template exacto de
/// facturacion/payload_input_output_sunat.md §2.
///
/// Nota: DOCUMENTOS_ELECTRONICOS no persiste FormaPagoCodigo por separado; se deriva de si el documento
/// tiene cuotas (Credito) o no (Contado) — ver Detalle.Cuotas. Es una limitación conocida del esquema
/// actual, no un error de este constructor.
public sealed class ConstructorXmlFacturaServicio : IConstructorXmlFacturaServicio
{
    private static readonly XNamespace Ubl = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";
    private static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

    public byte[] Construir(DocumentoElectronicoDetalle documento, Empresa empresa)
    {
        var cabecera = documento.Cabecera;
        var moneda = cabecera.MonedaCodigo;

        var invoice = new XElement(Ubl + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cac", Cac.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "ds", Ds.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "ext", Ext.NamespaceName),

            new XElement(Ext + "UBLExtensions",
                new XElement(Ext + "UBLExtension",
                    new XElement(Ext + "ExtensionContent"))),

            new XElement(Cbc + "UBLVersionID", "2.1"),
            new XElement(Cbc + "CustomizationID", "2.0"),
            new XElement(Cbc + "ID", $"{cabecera.Serie}-{cabecera.Correlativo}"),
            new XElement(Cbc + "IssueDate", cabecera.FechaEmision.ToString("yyyy-MM-dd")),
            new XElement(Cbc + "IssueTime", cabecera.HoraEmision.ToString("HH:mm:ss")),
            new XElement(Cbc + "InvoiceTypeCode", new XAttribute("listID", "0101"), cabecera.TipoDocumentoCodigo),
            new XElement(Cbc + "DocumentCurrencyCode", moneda),

            ConstruirFirma(cabecera.EmpresaRuc, cabecera.EmpresaRazonSocial),
            ConstruirProveedor(cabecera, empresa),
            ConstruirCliente(cabecera),
            ConstruirFormaPago(documento.Cuotas, cabecera.TotalImporte, moneda),
            ConstruirTaxTotal(cabecera, moneda),
            ConstruirLegalMonetaryTotal(cabecera, moneda));

        foreach (var linea in documento.Lineas)
        {
            invoice.Add(ConstruirLinea(linea, moneda));
        }

        var xDocument = new XDocument(new XDeclaration("1.0", "UTF-8", null), invoice);

        using var memoria = new MemoryStream();
        xDocument.Save(memoria);
        return memoria.ToArray();
    }

    private XElement ConstruirFirma(string ruc, string razonSocial) =>
        new(Cac + "Signature",
            new XElement(Cbc + "ID", ruc),
            new XElement(Cbc + "Note", "ms-facturacion"),
            new XElement(Cac + "SignatoryParty",
                new XElement(Cac + "PartyIdentification", new XElement(Cbc + "ID", ruc)),
                new XElement(Cac + "PartyName", new XElement(Cbc + "Name", razonSocial))),
            new XElement(Cac + "DigitalSignatureAttachment",
                new XElement(Cac + "ExternalReference",
                    new XElement(Cbc + "URI", $"#SIGN-{ruc}"))));

    private XElement ConstruirProveedor(DocumentoElectronico cabecera, Empresa empresa) =>
        new(Cac + "AccountingSupplierParty",
            new XElement(Cac + "Party",
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID", new XAttribute("schemeID", "6"), cabecera.EmpresaRuc)),
                new XElement(Cac + "PartyName",
                    new XElement(Cbc + "Name", cabecera.EmpresaNombreComercial ?? cabecera.EmpresaRazonSocial)),
                new XElement(Cac + "PartyLegalEntity",
                    new XElement(Cbc + "RegistrationName", cabecera.EmpresaRazonSocial),
                    new XElement(Cac + "RegistrationAddress",
                        new XElement(Cbc + "ID", cabecera.EmpresaUbigeo),
                        new XElement(Cbc + "AddressTypeCode", "0000"),
                        new XElement(Cbc + "CityName", empresa.Provincia),
                        new XElement(Cbc + "CountrySubentity", empresa.Departamento),
                        new XElement(Cbc + "District", empresa.Distrito),
                        new XElement(Cac + "AddressLine", new XElement(Cbc + "Line", cabecera.EmpresaDireccion)),
                        new XElement(Cac + "Country", new XElement(Cbc + "IdentificationCode", empresa.PaisCodigo))))));

    private XElement ConstruirCliente(DocumentoElectronico cabecera)
    {
        var registrationAddress = new XElement(Cac + "RegistrationAddress");
        if (!string.IsNullOrWhiteSpace(cabecera.ClienteDireccion))
        {
            registrationAddress.Add(new XElement(Cac + "AddressLine", new XElement(Cbc + "Line", cabecera.ClienteDireccion)));
        }
        registrationAddress.Add(new XElement(Cac + "Country", new XElement(Cbc + "IdentificationCode", "PE")));

        return new XElement(Cac + "AccountingCustomerParty",
            new XElement(Cac + "Party",
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID", new XAttribute("schemeID", cabecera.ClienteTipoDocumentoCodigo), cabecera.ClienteNumeroDocumento)),
                new XElement(Cac + "PartyLegalEntity",
                    new XElement(Cbc + "RegistrationName", cabecera.ClienteNombre),
                    registrationAddress)));
    }

    /// Anexo IV Res. 000193-2020/SUNAT (num. 170-173): Contado = un solo PaymentTerms; Credito = uno con
    /// el monto neto pendiente + uno por cuota con PaymentDueDate/Amount.
    private IEnumerable<XElement> ConstruirFormaPago(
        IReadOnlyList<CuotaDocumentoElectronico> cuotas, decimal totalImporte, string moneda)
    {
        if (cuotas.Count == 0)
        {
            yield return new XElement(Cac + "PaymentTerms",
                new XElement(Cbc + "ID", "FormaPago"),
                new XElement(Cbc + "PaymentMeansID", "Contado"));
            yield break;
        }

        yield return new XElement(Cac + "PaymentTerms",
            new XElement(Cbc + "ID", "FormaPago"),
            new XElement(Cbc + "PaymentMeansID", "Credito"),
            new XElement(Cbc + "Amount", new XAttribute("currencyID", moneda), totalImporte.ToString("F2")));

        foreach (var cuota in cuotas)
        {
            yield return new XElement(Cac + "PaymentTerms",
                new XElement(Cbc + "ID", "FormaPago"),
                new XElement(Cbc + "PaymentMeansID", $"Cuota{cuota.NumeroCuota:000}"),
                new XElement(Cbc + "Amount", new XAttribute("currencyID", moneda), cuota.Monto.ToString("F2")),
                new XElement(Cbc + "PaymentDueDate", cuota.FechaVencimiento.ToString("yyyy-MM-dd")));
        }
    }

    private XElement ConstruirTaxTotal(DocumentoElectronico cabecera, string moneda) =>
        new(Cac + "TaxTotal",
            new XElement(Cbc + "TaxAmount", new XAttribute("currencyID", moneda), cabecera.TotalIgv.ToString("F2")),
            new XElement(Cac + "TaxSubtotal",
                new XElement(Cbc + "TaxableAmount", new XAttribute("currencyID", moneda), cabecera.TotalGravado.ToString("F2")),
                new XElement(Cbc + "TaxAmount", new XAttribute("currencyID", moneda), cabecera.TotalIgv.ToString("F2")),
                new XElement(Cac + "TaxCategory",
                    new XElement(Cac + "TaxScheme",
                        new XElement(Cbc + "ID", "1000"),
                        new XElement(Cbc + "Name", "IGV"),
                        new XElement(Cbc + "TaxTypeCode", "VAT")))));

    private XElement ConstruirLegalMonetaryTotal(DocumentoElectronico cabecera, string moneda)
    {
        var lineExtensionAmount = cabecera.TotalGravado + cabecera.TotalInafecto + cabecera.TotalExonerado + cabecera.TotalGratuito;

        return new XElement(Cac + "LegalMonetaryTotal",
            new XElement(Cbc + "LineExtensionAmount", new XAttribute("currencyID", moneda), lineExtensionAmount.ToString("F2")),
            new XElement(Cbc + "TaxInclusiveAmount", new XAttribute("currencyID", moneda), cabecera.TotalImporte.ToString("F2")),
            new XElement(Cbc + "PayableAmount", new XAttribute("currencyID", moneda), cabecera.TotalImporte.ToString("F2")));
    }

    private XElement ConstruirLinea(LineaDocumentoElectronico linea, string moneda) =>
        new(Cac + "InvoiceLine",
            new XElement(Cbc + "ID", linea.NumeroLinea),
            new XElement(Cbc + "InvoicedQuantity", new XAttribute("unitCode", linea.UnidadMedidaCodigo), linea.Cantidad),
            new XElement(Cbc + "LineExtensionAmount", new XAttribute("currencyID", moneda), linea.ValorLinea.ToString("F2")),
            new XElement(Cac + "PricingReference",
                new XElement(Cac + "AlternativeConditionPrice",
                    new XElement(Cbc + "PriceAmount", new XAttribute("currencyID", moneda), linea.PrecioUnitario.ToString("F6")),
                    new XElement(Cbc + "PriceTypeCode", "01"))),
            new XElement(Cac + "TaxTotal",
                new XElement(Cbc + "TaxAmount", new XAttribute("currencyID", moneda), linea.MontoIgv.ToString("F2")),
                new XElement(Cac + "TaxSubtotal",
                    new XElement(Cbc + "TaxableAmount", new XAttribute("currencyID", moneda), linea.ValorLinea.ToString("F2")),
                    new XElement(Cbc + "TaxAmount", new XAttribute("currencyID", moneda), linea.MontoIgv.ToString("F2")),
                    new XElement(Cac + "TaxCategory",
                        new XElement(Cbc + "Percent", linea.PorcentajeIgv),
                        new XElement(Cbc + "TaxExemptionReasonCode", linea.AfectacionIgvCodigo),
                        new XElement(Cac + "TaxScheme",
                            new XElement(Cbc + "ID", "1000"),
                            new XElement(Cbc + "Name", "IGV"),
                            new XElement(Cbc + "TaxTypeCode", "VAT"))))),
            new XElement(Cac + "Item",
                new XElement(Cbc + "Description", linea.Descripcion),
                new XElement(Cac + "SellersItemIdentification", new XElement(Cbc + "ID", linea.ProductoCodigo))),
            new XElement(Cac + "Price",
                new XElement(Cbc + "PriceAmount", new XAttribute("currencyID", moneda), linea.ValorUnitario.ToString("F6"))));
}
