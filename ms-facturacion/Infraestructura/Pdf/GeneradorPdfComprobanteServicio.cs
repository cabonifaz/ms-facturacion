using System.Globalization;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using QRCoder;

namespace ms_facturacion.Infraestructura.Pdf;

/// Representación impresa del comprobante — QR + leyenda + monto en letras según Anexo C (RS 113-2018/
/// SUNAT, "Aspectos Técnicos") y RS 097-2012/RS 300-2014/SUNAT (contenido mínimo). No es el comprobante
/// legal en sí (eso es el XML firmado) — es únicamente la representación visual para imprimir/entregar.
public sealed class GeneradorPdfComprobanteServicio : IGeneradorPdfComprobanteServicio
{
    private static readonly Dictionary<string, string> NombresTipoDocumento = new()
    {
        ["01"] = "FACTURA ELECTRÓNICA",
        ["03"] = "BOLETA DE VENTA ELECTRÓNICA",
        ["07"] = "NOTA DE CRÉDITO ELECTRÓNICA",
        ["08"] = "NOTA DE DÉBITO ELECTRÓNICA"
    };

    private static bool _fontResolverConfigurado;

    public GeneradorPdfComprobanteServicio()
    {
        // GlobalFontSettings.FontResolver solo se puede asignar una vez por proceso.
        if (!_fontResolverConfigurado)
        {
            GlobalFontSettings.FontResolver = new FuenteEmbebidaResolver();
            _fontResolverConfigurado = true;
        }
    }

    public byte[] Construir(DocumentoElectronicoDetalle documento, Empresa empresa, string codigoVerificacion, string? sunatHash)
    {
        var cabecera = documento.Cabecera;

        using var doc = new PdfDocument();
        var pagina = doc.AddPage();
        pagina.Width = XUnit.FromMillimeter(210);
        pagina.Height = XUnit.FromMillimeter(297);
        using var gfx = XGraphics.FromPdfPage(pagina);

        var fuenteTitulo = new XFont(FuenteEmbebidaResolver.NombreFamilia, 14, XFontStyleEx.Bold);
        var fuenteSubtitulo = new XFont(FuenteEmbebidaResolver.NombreFamilia, 10, XFontStyleEx.Bold);
        var fuenteTexto = new XFont(FuenteEmbebidaResolver.NombreFamilia, 9, XFontStyleEx.Regular);
        var fuenteTextoChico = new XFont(FuenteEmbebidaResolver.NombreFamilia, 7.5, XFontStyleEx.Regular);
        var fuenteEncabezadoTabla = new XFont(FuenteEmbebidaResolver.NombreFamilia, 8, XFontStyleEx.Bold);

        var margen = XUnit.FromMillimeter(15).Point;
        var anchoUtil = pagina.Width.Point - 2 * margen;
        double y = margen;

        var nombreTipoDocumento = NombresTipoDocumento.GetValueOrDefault(cabecera.TipoDocumentoCodigo, "COMPROBANTE ELECTRÓNICO");

        // Cabecera: datos del emisor (izquierda) + recuadro RUC/tipo/serie-correlativo (derecha)
        gfx.DrawString(empresa.NombreComercial ?? empresa.RazonSocial, fuenteTitulo, XBrushes.Black, new XPoint(margen, y + 12));
        gfx.DrawString(empresa.RazonSocial, fuenteTexto, XBrushes.Black, new XPoint(margen, y + 26));
        gfx.DrawString($"RUC {empresa.Ruc}", fuenteTexto, XBrushes.Black, new XPoint(margen, y + 38));
        gfx.DrawString(empresa.Direccion, fuenteTexto, XBrushes.Black, new XPoint(margen, y + 50));
        gfx.DrawString($"{empresa.Distrito}, {empresa.Provincia}, {empresa.Departamento}", fuenteTexto, XBrushes.Black, new XPoint(margen, y + 62));

        var anchoRecuadro = XUnit.FromMillimeter(70).Point;
        var xRecuadro = margen + anchoUtil - anchoRecuadro;
        var altoRecuadro = XUnit.FromMillimeter(28).Point;
        gfx.DrawRectangle(XPens.Black, xRecuadro, y, anchoRecuadro, altoRecuadro);
        gfx.DrawString($"RUC: {empresa.Ruc}", fuenteSubtitulo, XBrushes.Black,
            new XRect(xRecuadro, y + 6, anchoRecuadro, 16), XStringFormats.TopCenter);
        gfx.DrawString(nombreTipoDocumento, fuenteSubtitulo, XBrushes.Black,
            new XRect(xRecuadro, y + 22, anchoRecuadro, 16), XStringFormats.TopCenter);
        gfx.DrawString($"{cabecera.Serie}-{cabecera.Correlativo}", fuenteTitulo, XBrushes.Black,
            new XRect(xRecuadro, y + 40, anchoRecuadro, 20), XStringFormats.TopCenter);

        y += altoRecuadro + 55;

        // Datos del receptor
        gfx.DrawLine(XPens.Black, margen, y, margen + anchoUtil, y);
        y += 12;
        gfx.DrawString($"Señor(es): {cabecera.ClienteNombre}", fuenteTexto, XBrushes.Black, new XPoint(margen, y));
        y += 12;
        gfx.DrawString($"{DescripcionTipoDocumentoCliente(cabecera.ClienteTipoDocumentoCodigo)}: {cabecera.ClienteNumeroDocumento}", fuenteTexto, XBrushes.Black, new XPoint(margen, y));
        y += 12;
        gfx.DrawString($"Fecha de emisión: {cabecera.FechaEmision:dd/MM/yyyy}", fuenteTexto, XBrushes.Black, new XPoint(margen, y));
        gfx.DrawString($"Moneda: {cabecera.MonedaCodigo}", fuenteTexto, XBrushes.Black, new XPoint(margen + anchoUtil / 2, y));
        y += 18;

        // Tabla de líneas
        double[] anchosColumna = [40, 200, 50, 60, 65, 65];
        string[] encabezados = ["CANT.", "DESCRIPCIÓN", "UND.", "P. UNIT.", "DSCTO.", "TOTAL"];

        gfx.DrawRectangle(XBrushes.LightGray, margen, y, anchoUtil, 16);
        double xCol = margen;
        for (var i = 0; i < encabezados.Length; i++)
        {
            gfx.DrawString(encabezados[i], fuenteEncabezadoTabla, XBrushes.Black,
                new XRect(xCol, y + 3, anchosColumna[i], 12), XStringFormats.TopCenter);
            xCol += anchosColumna[i];
        }
        y += 16;

        foreach (var linea in documento.Lineas)
        {
            var altoFila = 14;
            xCol = margen;
            var valores = new[]
            {
                linea.Cantidad.ToString("0.###", CultureInfo.InvariantCulture),
                linea.Descripcion,
                linea.UnidadMedidaCodigo,
                linea.PrecioUnitario.ToString("F2", CultureInfo.InvariantCulture),
                linea.MontoDescuento.ToString("F2", CultureInfo.InvariantCulture),
                linea.TotalLinea.ToString("F2", CultureInfo.InvariantCulture)
            };
            for (var i = 0; i < valores.Length; i++)
            {
                var alineacion = i == 1 ? XStringFormats.TopLeft : XStringFormats.TopCenter;
                gfx.DrawString(valores[i], fuenteTextoChico, XBrushes.Black,
                    new XRect(xCol + (i == 1 ? 2 : 0), y + 3, anchosColumna[i] - (i == 1 ? 4 : 0), altoFila), alineacion);
                xCol += anchosColumna[i];
            }
            gfx.DrawLine(XPens.LightGray, margen, y + altoFila, margen + anchoUtil, y + altoFila);
            y += altoFila;
        }

        y += 10;

        // Monto en letras (izquierda) + totales (derecha)
        var montoLetras = NumeroALetrasConvertidor.Convertir(cabecera.TotalImporte, cabecera.MonedaCodigo);
        gfx.DrawString(montoLetras, fuenteTextoChico, XBrushes.Black, new XRect(margen, y, anchoUtil * 0.55, 40), XStringFormats.TopLeft);

        var anchoTotales = anchoUtil * 0.4;
        var xTotales = margen + anchoUtil - anchoTotales;
        var filasTotales = new (string Etiqueta, decimal Monto)[]
        {
            ("Op. Gravada", cabecera.TotalGravado),
            ("Op. Exonerada", cabecera.TotalExonerado),
            ("Op. Inafecta", cabecera.TotalInafecto),
            ("Op. Gratuita", cabecera.TotalGratuito),
            ("IGV", cabecera.TotalIgv),
            ("Descuentos", cabecera.TotalDescuento),
            ("IMPORTE TOTAL", cabecera.TotalImporte)
        };

        var yTotales = y;
        foreach (var (etiqueta, monto) in filasTotales)
        {
            if (monto == 0 && etiqueta is not ("IGV" or "IMPORTE TOTAL")) continue;

            var fuente = etiqueta == "IMPORTE TOTAL" ? fuenteSubtitulo : fuenteTexto;
            gfx.DrawString(etiqueta, fuente, XBrushes.Black, new XRect(xTotales, yTotales, anchoTotales * 0.55, 12), XStringFormats.TopLeft);
            gfx.DrawString($"{cabecera.MonedaCodigo} {monto:F2}", fuente, XBrushes.Black,
                new XRect(xTotales + anchoTotales * 0.55, yTotales, anchoTotales * 0.45, 12), XStringFormats.TopRight);
            yTotales += 14;
        }

        y = Math.Max(y + 45, yTotales) + 15;

        // QR (Anexo C, RS 113-2018/SUNAT): RUC|TipoDoc|Serie|Correlativo|IGV|Total|FechaEmision|TipoDocAdq|NumDocAdq|Hash
        var contenidoQr = string.Join('|',
            empresa.Ruc, cabecera.TipoDocumentoCodigo, cabecera.Serie, cabecera.Correlativo,
            cabecera.TotalIgv.ToString("F2", CultureInfo.InvariantCulture),
            cabecera.TotalImporte.ToString("F2", CultureInfo.InvariantCulture),
            cabecera.FechaEmision.ToString("yyyy-MM-dd"),
            cabecera.ClienteTipoDocumentoCodigo, cabecera.ClienteNumeroDocumento,
            sunatHash ?? "");

        using var generadorQr = new QRCodeGenerator();
        using var datosQr = generadorQr.CreateQrCode(contenidoQr, QRCodeGenerator.ECCLevel.M);
        using var pngQr = new PngByteQRCode(datosQr);
        var qrBytes = pngQr.GetGraphic(10);

        using var streamQr = new MemoryStream(qrBytes);
        using var imagenQr = XImage.FromStream(streamQr);
        // Máximo 6cm x 6cm por norma — se usa un tamaño bastante menor, alcanza para lectura.
        var ladoQr = XUnit.FromMillimeter(28).Point;
        gfx.DrawImage(imagenQr, margen, y, ladoQr, ladoQr);

        var xLeyenda = margen + ladoQr + 10;
        var anchoLeyenda = anchoUtil - ladoQr - 10;
        gfx.DrawString(
            $"Representación impresa de la {nombreTipoDocumento}. Código de verificación: {codigoVerificacion}",
            fuenteTextoChico, XBrushes.Black, new XRect(xLeyenda, y, anchoLeyenda, 40), XStringFormats.TopLeft);

        if (!string.IsNullOrEmpty(sunatHash))
        {
            gfx.DrawString($"Hash: {sunatHash}", fuenteTextoChico, XBrushes.Black,
                new XRect(xLeyenda, y + 28, anchoLeyenda, 24), XStringFormats.TopLeft);
        }

        using var salida = new MemoryStream();
        doc.Save(salida);
        return salida.ToArray();
    }

    private static string DescripcionTipoDocumentoCliente(string codigo) => codigo switch
    {
        "6" => "RUC",
        "1" => "DNI",
        _ => "Documento"
    };
}
