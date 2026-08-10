using System.Globalization;
using System.Xml.Linq;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Xml;

/// Construye el UBL 2.1 sin firmar para Factura/Boleta (01/03 → Invoice) y Nota de Crédito/Débito
/// (07 → CreditNote, 08 → DebitNote) — comparten casi toda la estructura (proveedor, cliente, forma de
/// pago, totales, líneas); solo cambian el elemento raíz, el nombre de la línea/cantidad, el nombre del
/// total monetario (DebitNote usa RequestedMonetaryTotal, no LegalMonetaryTotal — quirk real de UBL 2.1),
/// y que 07/08 agregan cac:DiscrepancyResponse + cac:BillingReference en vez de cbc:InvoiceTypeCode.
///
/// Nota: DOCUMENTOS_ELECTRONICOS no persiste FormaPagoCodigo como columna propia — lo resuelve
/// SP_DocumentoElectronico_Obtener contra TABLA_MAESTRA IdMaestro=9 (según haya o no cuotas activas) y
/// llega ya resuelto en Cabecera.FormaPagoCodigo; este constructor no re-deriva nada, solo lee el valor.
///
/// TipoOperacionCodigo (Catálogo N.° 17 SUNAT) solo se emite en Factura/Boleta, como cbc:Note con
/// languageLocaleID="1000" — no aplica a Nota de Crédito/Débito.
public sealed class ConstructorXmlComprobanteServicio : IConstructorXmlComprobanteServicio
{
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";
    private static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

    private sealed record TipoComprobante(
        XNamespace RaizNs, string ElementoRaiz, string ElementoLinea, string ElementoCantidad, string ElementoTotalMonetario);

    private static readonly Dictionary<string, TipoComprobante> TiposComprobante = new()
    {
        ["01"] = new TipoComprobante("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", "Invoice", "InvoiceLine", "InvoicedQuantity", "LegalMonetaryTotal"),
        ["03"] = new TipoComprobante("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", "Invoice", "InvoiceLine", "InvoicedQuantity", "LegalMonetaryTotal"),
        ["07"] = new TipoComprobante("urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2", "CreditNote", "CreditNoteLine", "CreditedQuantity", "LegalMonetaryTotal"),
        ["08"] = new TipoComprobante("urn:oasis:names:specification:ubl:schema:xsd:DebitNote-2", "DebitNote", "DebitNoteLine", "DebitedQuantity", "RequestedMonetaryTotal"),
    };

    public byte[] Construir(DocumentoElectronicoDetalle documento, Empresa empresa)
    {
        var cabecera = documento.Cabecera;
        var moneda = cabecera.MonedaCodigo;

        if (!TiposComprobante.TryGetValue(cabecera.TipoDocumentoCodigo, out var tipo))
        {
            throw new NotSupportedException(
                $"El constructor de XML todavía no soporta TipoDocumentoCodigo '{cabecera.TipoDocumentoCodigo}'.");
        }

        var esNota = cabecera.TipoDocumentoCodigo is "07" or "08";

        var raiz = new XElement(tipo.RaizNs + tipo.ElementoRaiz,
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
            new XElement(Cbc + "IssueTime", cabecera.HoraEmision.ToString("HH:mm:ss")));

        if (esNota && documento.Referencia is null)
        {
            throw new InvalidOperationException(
                "El documento es una nota de crédito/débito pero no tiene REFERENCIAS_DOCUMENTO_ELECTRONICO asociada.");
        }

        if (!esNota)
        {
            raiz.Add(
                new XElement(Cbc + "InvoiceTypeCode", new XAttribute("listID", "0101"), cabecera.TipoDocumentoCodigo),
                // Catálogo N.° 17 SUNAT (Tipo de Operación) — languageLocaleID="1000" es la convención SUNAT
                // para este catálogo dentro de cbc:Note (no aplica a notas de crédito/débito).
                new XElement(Cbc + "Note", new XAttribute("languageLocaleID", "1000"), cabecera.TipoOperacionCodigo));
        }

        // cbc:DocumentCurrencyCode debe ir ANTES de cac:DiscrepancyResponse (orden exigido por el XSD
        // CreditNoteType/DebitNoteType de UBL 2.1) — invertido antes, lo que causaba fault
        // "found DocumentCurrencyCode, but next item should be AccountingSupplierParty" en SUNAT: al ser
        // ambos opcionales, el validador saltaba DocumentCurrencyCode en su posición real, calzaba
        // DiscrepancyResponse más adelante, y luego no lograba ubicar el DocumentCurrencyCode sobrante.
        raiz.Add(new XElement(Cbc + "DocumentCurrencyCode", moneda));

        if (esNota)
        {
            raiz.Add(ConstruirDiscrepancyResponse(documento.Referencia!));
        }

        // cac:OrderReference — opcional (0..1 en la guía SUNAT), string plano (an..20) sin validación SUNAT.
        if (!string.IsNullOrEmpty(cabecera.NumeroReferencia))
        {
            raiz.Add(new XElement(Cac + "OrderReference", new XElement(Cbc + "ID", cabecera.NumeroReferencia)));
        }

        if (esNota)
        {
            raiz.Add(ConstruirBillingReference(documento.Referencia!));
        }

        raiz.Add(
            ConstruirFirma(cabecera.EmpresaRuc, cabecera.EmpresaRazonSocial),
            ConstruirProveedor(cabecera, empresa),
            ConstruirCliente(cabecera),
            ConstruirFormaPago(cabecera.FormaPagoCodigo, documento.Cuotas, cabecera.TotalImporte, moneda));

        // cac:PaymentExchangeRate — solo cuando el documento trae TipoCambio (moneda extranjera ligada a
        // detracción/percepción/retención, Anexo N.° 7 SUNAT). Target siempre PEN: es el único caso en que
        // este tipo de cambio aplica.
        if (cabecera.TipoCambio.HasValue)
        {
            raiz.Add(new XElement(Cac + "PaymentExchangeRate",
                new XElement(Cbc + "SourceCurrencyCode", moneda),
                new XElement(Cbc + "TargetCurrencyCode", "PEN"),
                new XElement(Cbc + "CalculationRate", cabecera.TipoCambio.Value.ToString("0.000", CultureInfo.InvariantCulture)),
                new XElement(Cbc + "Date", cabecera.FechaEmision.ToString("yyyy-MM-dd"))));
        }

        raiz.Add(
            ConstruirTaxTotal(documento.Lineas, moneda),
            ConstruirTotalMonetario(tipo.ElementoTotalMonetario, cabecera, moneda));

        foreach (var linea in documento.Lineas)
        {
            raiz.Add(ConstruirLinea(tipo.ElementoLinea, tipo.ElementoCantidad, linea, moneda));
        }

        var xDocument = new XDocument(new XDeclaration("1.0", "UTF-8", null), raiz);

        using var memoria = new MemoryStream();
        xDocument.Save(memoria);
        return memoria.ToArray();
    }

    /// Catálogo N.° 09 (motivo de nota de crédito) va en cbc:ResponseCode; la descripción en cbc:Description
    /// (Anexo VII, num. 6-7). El documento afectado se referencia por separado en cac:BillingReference.
    private XElement ConstruirDiscrepancyResponse(ReferenciaDocumentoElectronico referencia) =>
        new(Cac + "DiscrepancyResponse",
            new XElement(Cbc + "ReferenceID", $"{referencia.SerieRelacionada}-{referencia.CorrelativoRelacionado}"),
            new XElement(Cbc + "ResponseCode", referencia.MotivoCodigo),
            new XElement(Cbc + "Description", referencia.MotivoDescripcion));

    private XElement ConstruirBillingReference(ReferenciaDocumentoElectronico referencia) =>
        new(Cac + "BillingReference",
            new XElement(Cac + "InvoiceDocumentReference",
                new XElement(Cbc + "ID", $"{referencia.SerieRelacionada}-{referencia.CorrelativoRelacionado}"),
                new XElement(Cbc + "DocumentTypeCode", referencia.TipoDocumentoRelacionadoCodigo)));

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
        registrationAddress.Add(new XElement(Cac + "Country", new XElement(Cbc + "IdentificationCode", cabecera.ClientePaisCodigo)));

        return new XElement(Cac + "AccountingCustomerParty",
            new XElement(Cac + "Party",
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID", new XAttribute("schemeID", cabecera.ClienteTipoDocumentoCodigo), cabecera.ClienteNumeroDocumento)),
                new XElement(Cac + "PartyLegalEntity",
                    new XElement(Cbc + "RegistrationName", cabecera.ClienteNombre),
                    registrationAddress)));
    }

    /// Anexo IV Res. 000193-2020/SUNAT (num. 170-173): Contado = un solo PaymentTerms; Credito = uno con
    /// el monto neto pendiente + uno por cuota con PaymentDueDate/Amount. formaPagoCodigo ya viene resuelto
    /// por SP_DocumentoElectronico_Obtener (TABLA_MAESTRA IdMaestro=9) — este método no vuelve a inferirlo.
    private IEnumerable<XElement> ConstruirFormaPago(
        string formaPagoCodigo, IReadOnlyList<CuotaDocumentoElectronico> cuotas, decimal totalImporte, string moneda)
    {
        if (formaPagoCodigo == "Contado")
        {
            yield return new XElement(Cac + "PaymentTerms",
                new XElement(Cbc + "ID", "FormaPago"),
                new XElement(Cbc + "PaymentMeansID", "Contado"));
            yield break;
        }

        yield return new XElement(Cac + "PaymentTerms",
            new XElement(Cbc + "ID", "FormaPago"),
            new XElement(Cbc + "PaymentMeansID", "Credito"),
            new XElement(Cbc + "Amount", new XAttribute("currencyID", moneda), totalImporte.ToString("F2", CultureInfo.InvariantCulture)));

        foreach (var cuota in cuotas)
        {
            yield return new XElement(Cac + "PaymentTerms",
                new XElement(Cbc + "ID", "FormaPago"),
                new XElement(Cbc + "PaymentMeansID", $"Cuota{cuota.NumeroCuota:000}"),
                new XElement(Cbc + "Amount", new XAttribute("currencyID", moneda), cuota.Monto.ToString("F2", CultureInfo.InvariantCulture)),
                new XElement(Cbc + "PaymentDueDate", cuota.FechaVencimiento.ToString("yyyy-MM-dd")));
        }
    }

    /// SUNAT exige un cac:TaxSubtotal por cada tributo que aparezca en al menos una línea (fault 2638) —
    /// las 4 columnas de bucket en cabecera (TotalGravado/Exonerado/Inafecto/Exportacion) agrupan por
    /// Num2/AfectacionIgvCodigo, NO por tributo real: un mismo bucket Gravado puede mezclar líneas con
    /// tributo 1000 y 9996 a la vez (ver fix de fault 2040), así que no sirven para armar este total —
    /// hay que agrupar las líneas por su propio TributoSunatCodigo.
    private XElement ConstruirTaxTotal(IEnumerable<LineaDocumentoElectronico> lineas, string moneda)
    {
        var gruposPorTributo = lineas
            .GroupBy(l => (l.TributoSunatCodigo, l.TributoNombre, l.TributoTaxTypeCode))
            .Select(g => new
            {
                g.Key.TributoSunatCodigo,
                g.Key.TributoNombre,
                g.Key.TributoTaxTypeCode,
                TaxableAmount = g.Sum(l => l.ValorLinea),
                TaxAmount = g.Sum(l => l.MontoIgv)
            })
            .OrderBy(g => g.TributoSunatCodigo)
            .ToList();

        var totalTaxAmount = gruposPorTributo.Sum(g => g.TaxAmount);

        return new XElement(Cac + "TaxTotal",
            new XElement(Cbc + "TaxAmount", new XAttribute("currencyID", moneda), totalTaxAmount.ToString("F2", CultureInfo.InvariantCulture)),
            gruposPorTributo.Select(g =>
                new XElement(Cac + "TaxSubtotal",
                    new XElement(Cbc + "TaxableAmount", new XAttribute("currencyID", moneda), g.TaxableAmount.ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(Cbc + "TaxAmount", new XAttribute("currencyID", moneda), g.TaxAmount.ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(Cac + "TaxCategory",
                        new XElement(Cac + "TaxScheme",
                            new XElement(Cbc + "ID", g.TributoSunatCodigo),
                            new XElement(Cbc + "Name", g.TributoNombre),
                            new XElement(Cbc + "TaxTypeCode", g.TributoTaxTypeCode))))));
    }

    /// Nombre del elemento varía por tipo: LegalMonetaryTotal (Invoice/CreditNote) vs RequestedMonetaryTotal
    /// (DebitNote) — mismos hijos en los tres casos.
    private XElement ConstruirTotalMonetario(string elementoTotalMonetario, DocumentoElectronico cabecera, string moneda)
    {
        var lineExtensionAmount = cabecera.TotalGravado + cabecera.TotalInafecto + cabecera.TotalExonerado + cabecera.TotalExportacion;

        return new XElement(Cac + elementoTotalMonetario,
            new XElement(Cbc + "LineExtensionAmount", new XAttribute("currencyID", moneda), lineExtensionAmount.ToString("F2", CultureInfo.InvariantCulture)),
            new XElement(Cbc + "TaxInclusiveAmount", new XAttribute("currencyID", moneda), cabecera.TotalImporte.ToString("F2", CultureInfo.InvariantCulture)),
            new XElement(Cbc + "PayableAmount", new XAttribute("currencyID", moneda), cabecera.TotalImporte.ToString("F2", CultureInfo.InvariantCulture)));
    }

    private XElement ConstruirLinea(string elementoLinea, string elementoCantidad, LineaDocumentoElectronico linea, string moneda) =>
        new(Cac + elementoLinea,
            new XElement(Cbc + "ID", linea.NumeroLinea),
            new XElement(Cbc + elementoCantidad, new XAttribute("unitCode", linea.UnidadMedidaCodigo), linea.Cantidad),
            new XElement(Cbc + "LineExtensionAmount", new XAttribute("currencyID", moneda), linea.ValorLinea.ToString("F2", CultureInfo.InvariantCulture)),
            new XElement(Cac + "PricingReference",
                new XElement(Cac + "AlternativeConditionPrice",
                    new XElement(Cbc + "PriceAmount", new XAttribute("currencyID", moneda), linea.PrecioUnitario.ToString("F6", CultureInfo.InvariantCulture)),
                    new XElement(Cbc + "PriceTypeCode", "01"))),
            // Descuento por ítem — solo se emite si hay descuento; SUNAT recalcula LineExtensionAmount =
            // Price*Quantity y lo compara contra el valor declarado (fault 3271), así que sin este elemento
            // no hay forma de informarle que ese monto ya viene con un descuento restado.
            linea.MontoDescuento > 0
                ? new XElement(Cac + "AllowanceCharge",
                    new XElement(Cbc + "ChargeIndicator", "false"),
                    new XElement(Cbc + "AllowanceChargeReasonCode", "00"), // Catálogo 53: OTROS DESCUENTOS
                    new XElement(Cbc + "Amount", new XAttribute("currencyID", moneda), linea.MontoDescuento.ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(Cbc + "BaseAmount", new XAttribute("currencyID", moneda), (linea.Cantidad * linea.ValorUnitario).ToString("F2", CultureInfo.InvariantCulture)))
                : null,
            new XElement(Cac + "TaxTotal",
                new XElement(Cbc + "TaxAmount", new XAttribute("currencyID", moneda), linea.MontoIgv.ToString("F2", CultureInfo.InvariantCulture)),
                new XElement(Cac + "TaxSubtotal",
                    new XElement(Cbc + "TaxableAmount", new XAttribute("currencyID", moneda), linea.ValorLinea.ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(Cbc + "TaxAmount", new XAttribute("currencyID", moneda), linea.MontoIgv.ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(Cac + "TaxCategory",
                        new XElement(Cbc + "ID", linea.TributoCategoria),
                        new XElement(Cbc + "Percent", linea.PorcentajeIgv),
                        new XElement(Cbc + "TaxExemptionReasonCode", linea.AfectacionIgvCodigo),
                        new XElement(Cac + "TaxScheme",
                            new XElement(Cbc + "ID", linea.TributoSunatCodigo),
                            new XElement(Cbc + "Name", linea.TributoNombre),
                            new XElement(Cbc + "TaxTypeCode", linea.TributoTaxTypeCode))))),
            new XElement(Cac + "Item",
                new XElement(Cbc + "Description", linea.Descripcion),
                new XElement(Cac + "SellersItemIdentification", new XElement(Cbc + "ID", linea.ProductoCodigo))),
            new XElement(Cac + "Price",
                new XElement(Cbc + "PriceAmount", new XAttribute("currencyID", moneda), linea.ValorUnitario.ToString("F6", CultureInfo.InvariantCulture))));
}
