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
        ["01"] = "FACTURA ELECTRONICA",
        ["03"] = "BOLETA DE VENTA ELECTRONICA",
        ["07"] = "NOTA DE CREDITO ELECTRONICA",
        ["08"] = "NOTA DE DEBITO ELECTRONICA"
    };

    // Nombre legible de moneda — mismo subconjunto sembrado en ms-facturación TABLA_MAESTRA IdMaestro=11
    // (PEN/USD/EUR/GBP). Este generador no tiene acceso a BD, por eso va como diccionario fijo acá.
    private static readonly Dictionary<string, string> NombresMoneda = new()
    {
        ["PEN"] = "SOL",
        ["USD"] = "DOLAR AMERICANO",
        ["EUR"] = "EURO",
        ["GBP"] = "LIBRA ESTERLINA"
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

        var fuenteTitulo = new XFont(FuenteEmbebidaResolver.NombreFamilia, 13, XFontStyleEx.Bold);
        var fuenteSubtitulo = new XFont(FuenteEmbebidaResolver.NombreFamilia, 10, XFontStyleEx.Bold);
        var fuenteTexto = new XFont(FuenteEmbebidaResolver.NombreFamilia, 8.5, XFontStyleEx.Regular);
        var fuenteTextoNegrita = new XFont(FuenteEmbebidaResolver.NombreFamilia, 8.5, XFontStyleEx.Bold);
        var fuenteTextoChico = new XFont(FuenteEmbebidaResolver.NombreFamilia, 7.5, XFontStyleEx.Regular);
        var fuenteEncabezadoTabla = new XFont(FuenteEmbebidaResolver.NombreFamilia, 8, XFontStyleEx.Bold);

        var margen = XUnit.FromMillimeter(12).Point;
        var anchoUtil = pagina.Width.Point - 2 * margen;
        var yInicio = margen;
        double y = yInicio + 8;

        var nombreTipoDocumento = NombresTipoDocumento.GetValueOrDefault(cabecera.TipoDocumentoCodigo, "COMPROBANTE ELECTRONICO");
        var nombreMoneda = NombresMoneda.GetValueOrDefault(cabecera.MonedaCodigo, cabecera.MonedaCodigo);
        var establecimiento = $"{empresa.Direccion} {empresa.Distrito}-{empresa.Provincia}-{empresa.Departamento}";

        // ===== Cabecera: emisor (izquierda) + recuadro tipo/RUC/serie-correlativo (derecha) =====
        var anchoRecuadro = XUnit.FromMillimeter(65).Point;
        var xRecuadro = margen + anchoUtil - anchoRecuadro;

        gfx.DrawString(empresa.RazonSocial, fuenteTitulo, XBrushes.Black, new XPoint(margen, y + 10));
        gfx.DrawString(establecimiento, fuenteTexto, XBrushes.Black,
            new XRect(margen, y + 16, anchoUtil - anchoRecuadro - 10, 26), XStringFormats.TopLeft);

        var altoRecuadro = XUnit.FromMillimeter(22).Point;
        gfx.DrawRectangle(XPens.Black, xRecuadro, y, anchoRecuadro, altoRecuadro);
        gfx.DrawString(nombreTipoDocumento, fuenteSubtitulo, XBrushes.Black,
            new XRect(xRecuadro, y + 5, anchoRecuadro, 14), XStringFormats.TopCenter);
        gfx.DrawString($"RUC: {empresa.Ruc}", fuenteTexto, XBrushes.Black,
            new XRect(xRecuadro, y + 20, anchoRecuadro, 14), XStringFormats.TopCenter);
        gfx.DrawString($"{cabecera.Serie}-{cabecera.Correlativo}", fuenteSubtitulo, XBrushes.Black,
            new XRect(xRecuadro, y + 34, anchoRecuadro, 16), XStringFormats.TopCenter);

        y += Math.Max(altoRecuadro + 20, 55);

        // ===== Datos del comprobante: etiqueta : valor, alineado por columna =====
        gfx.DrawLine(XPens.Black, margen, y, margen + anchoUtil, y);
        y += 8;

        const double anchoEtiqueta = 130;
        void DibujarCampo(string etiqueta, string valor)
        {
            gfx.DrawString(etiqueta, fuenteTexto, XBrushes.Black, new XPoint(margen, y + 9));
            gfx.DrawString(":", fuenteTexto, XBrushes.Black, new XPoint(margen + anchoEtiqueta, y + 9));
            gfx.DrawString(valor, fuenteTextoNegrita, XBrushes.Black,
                new XRect(margen + anchoEtiqueta + 8, y + 9, anchoUtil - anchoEtiqueta - 8, 22), XStringFormats.TopLeft);
        }

        DibujarCampo("Fecha de Emisión", cabecera.FechaEmision.ToString("dd/MM/yyyy"));
        y += 12;
        DibujarCampo("Señor(es)", cabecera.ClienteNombre);
        y += 12;
        DibujarCampo($"{DescripcionTipoDocumentoCliente(cabecera.ClienteTipoDocumentoCodigo)}", cabecera.ClienteNumeroDocumento);
        y += 12;
        DibujarCampo("Establecimiento del Emisor", establecimiento);
        y += 20;
        DibujarCampo("Tipo de Moneda", nombreMoneda);
        y += 12;
        DibujarCampo("Observación", cabecera.NumeroReferencia ?? "");
        y += 18;

        // ===== Tabla de líneas =====
        double[] anchosColumna = [45, 75, anchoUtil - 45 - 75 - 70 - 60, 70, 60];
        string[] encabezados = ["Cantidad", "Unidad Medida", "Descripción", "Valor Unitario", "ICBPER"];

        gfx.DrawRectangle(XPens.Black, margen, y, anchoUtil, 16);
        double xCol = margen;
        for (var i = 0; i < encabezados.Length; i++)
        {
            gfx.DrawString(encabezados[i], fuenteEncabezadoTabla, XBrushes.Black,
                new XRect(xCol, y + 3, anchosColumna[i], 12), XStringFormats.TopCenter);
            xCol += anchosColumna[i];
            if (i < encabezados.Length - 1) gfx.DrawLine(XPens.Black, xCol, y, xCol, y + 16);
        }
        y += 16;
        var yFilasInicio = y;

        foreach (var linea in documento.Lineas)
        {
            var altoFila = Math.Max(14, 10 * (int)Math.Ceiling(linea.Descripcion.Length / 55.0));
            xCol = margen;
            var valores = new[]
            {
                linea.Cantidad.ToString("0.###", CultureInfo.InvariantCulture),
                linea.UnidadMedidaCodigo,
                linea.Descripcion,
                linea.ValorUnitario.ToString("F2", CultureInfo.InvariantCulture),
                "0.00"
            };
            for (var i = 0; i < valores.Length; i++)
            {
                var alineacion = i == 2 ? XStringFormats.TopLeft : XStringFormats.TopCenter;
                gfx.DrawString(valores[i], fuenteTextoChico, XBrushes.Black,
                    new XRect(xCol + (i == 2 ? 3 : 0), y + 3, anchosColumna[i] - (i == 2 ? 6 : 0), altoFila), alineacion);
                xCol += anchosColumna[i];
            }
            gfx.DrawLine(XPens.LightGray, margen, y + altoFila, margen + anchoUtil, y + altoFila);
            y += altoFila;
        }
        gfx.DrawRectangle(XPens.Black, margen, yFilasInicio, anchoUtil, y - yFilasInicio);

        y += 12;

        // ===== Operaciones gratuitas (izquierda) + totales (derecha) =====
        var anchoTotales = XUnit.FromMillimeter(75).Point;
        var xTotales = margen + anchoUtil - anchoTotales;
        var anchoGratuitas = anchoUtil - anchoTotales - 15;

        var yBloqueInicio = y;

        var altoCajaGratuitas = 24;
        gfx.DrawRectangle(XPens.Black, margen, y, anchoGratuitas, altoCajaGratuitas);
        gfx.DrawString("Valor de Venta de Operaciones Gratuitas :", fuenteTexto, XBrushes.Black,
            new XRect(margen + 4, y + 4, anchoGratuitas * 0.65, 16), XStringFormats.TopLeft);
        gfx.DrawString($"{(nombreMoneda == "SOL" ? "S/" : "$")} {cabecera.TotalGratuito:F2}", fuenteTextoNegrita, XBrushes.Black,
            new XRect(margen + anchoGratuitas * 0.65, y + 4, anchoGratuitas * 0.35 - 4, 16), XStringFormats.TopRight);

        var ySon = y + altoCajaGratuitas + 14;
        var montoLetras = NumeroALetrasConvertidor.Convertir(cabecera.TotalImporte, cabecera.MonedaCodigo);
        gfx.DrawString(montoLetras, fuenteTextoNegrita, XBrushes.Black, new XRect(margen, ySon, anchoGratuitas, 30), XStringFormats.TopLeft);

        // Totales (derecha): valor de venta ya viene neto de descuento por línea (ValorLinea), Descuentos
        // acá es solo informativo (el monto ya está reflejado en TotalGravado/Exonerado/Inafecto/Gratuito).
        var subTotalVentas = cabecera.TotalGravado + cabecera.TotalExonerado + cabecera.TotalInafecto + cabecera.TotalGratuito;
        var filasTotales = new (string Etiqueta, decimal Monto, bool Siempre)[]
        {
            ("Sub Total Ventas", subTotalVentas, true),
            ("Anticipos", 0, false),
            ("Descuentos", cabecera.TotalDescuento, false),
            ("Valor Venta", subTotalVentas, true),
            ("ISC", cabecera.TotalIsc, false),
            ("IGV", cabecera.TotalIgv, true),
            ("ICBPER", 0, false),
            ("Otros Cargos", cabecera.TotalCargo, false),
            ("Otros Tributos", cabecera.TotalOtrosTributos, false),
            ("Monto de redondeo", 0, false),
            ("Importe Total", cabecera.TotalImporte, true)
        };

        var yTotales = y;
        var altoFilaTotal = 13.0;
        var totalesVisibles = filasTotales.Where(f => f.Siempre || f.Monto != 0).ToList();
        var altoCajaTotales = totalesVisibles.Count * altoFilaTotal;
        gfx.DrawRectangle(XPens.Black, xTotales, yTotales, anchoTotales, altoCajaTotales);

        foreach (var (etiqueta, monto, _) in totalesVisibles)
        {
            var esImporteTotal = etiqueta == "Importe Total";
            var fuente = esImporteTotal ? fuenteTextoNegrita : fuenteTexto;
            gfx.DrawString(etiqueta, fuente, XBrushes.Black,
                new XRect(xTotales + 4, yTotales + 2, anchoTotales * 0.6, 12), XStringFormats.TopLeft);
            gfx.DrawString(monto.ToString("F2", CultureInfo.InvariantCulture), fuente, XBrushes.Black,
                new XRect(xTotales + anchoTotales * 0.6, yTotales + 2, anchoTotales * 0.4 - 4, 12), XStringFormats.TopRight);
            if (etiqueta != totalesVisibles[^1].Etiqueta)
            {
                gfx.DrawLine(XPens.LightGray, xTotales, yTotales + altoFilaTotal, xTotales + anchoTotales, yTotales + altoFilaTotal);
            }
            yTotales += altoFilaTotal;
        }

        y = Math.Max(ySon + 40, yTotales + 15);

        // ===== QR (Anexo C, RS 113-2018/SUNAT) =====
        // RUC|TipoDoc|Serie|Correlativo|IGV|Total|FechaEmision|TipoDocAdq|NumDocAdq|Hash
        var contenidoQr = string.Join('|',
            empresa.Ruc, cabecera.TipoDocumentoCodigo, cabecera.Serie, cabecera.Correlativo,
            cabecera.TotalIgv.ToString("F2", CultureInfo.InvariantCulture),
            cabecera.TotalImporte.ToString("F2", CultureInfo.InvariantCulture),
            cabecera.FechaEmision.ToString("yyyy-MM-dd"),
            cabecera.ClienteTipoDocumentoCodigo, cabecera.ClienteNumeroDocumento,
            sunatHash ?? "");

        var ladoQr = XUnit.FromMillimeter(28).Point;
        DibujarQr(gfx, contenidoQr, margen, y, ladoQr);

        var xLeyenda = margen + ladoQr + 10;
        var anchoLeyenda = anchoUtil - ladoQr - 10;

        // PdfSharp solo hace word-wrap en espacios — un hash/código largo sin espacios se dibuja como una
        // sola "palabra" y se sale del rectángulo. Se le insertan espacios cada 8 caracteres solo para que
        // pueda cortarse en varias líneas; el valor real (sin espacios) es el que se usa en cualquier otro
        // lado (QR, comparaciones, etc.), acá es puramente cosmético.
        gfx.DrawString(
            $"Representación impresa de la {nombreTipoDocumento}. Código de verificación: {InsertarEspacios(codigoVerificacion, 8)}",
            fuenteTextoChico, XBrushes.Black, new XRect(xLeyenda, y, anchoLeyenda, 55), XStringFormats.TopLeft);

        if (!string.IsNullOrEmpty(sunatHash))
        {
            gfx.DrawString($"Hash: {InsertarEspacios(sunatHash, 8)}", fuenteTextoChico, XBrushes.Black,
                new XRect(xLeyenda, y + 40, anchoLeyenda, 30), XStringFormats.TopLeft);
        }

        y += ladoQr + 12;

        // ===== Leyenda final =====
        var altoLeyenda = 26.0;
        gfx.DrawRectangle(XPens.Black, margen, y, anchoUtil, altoLeyenda);
        gfx.DrawString(
            $"Esta es una representación impresa de la {nombreTipoDocumento.ToLowerInvariant()}, generada en el Sistema de SUNAT. " +
            "Puede verificarla utilizando el código de verificación indicado arriba.",
            fuenteTextoChico, XBrushes.Black, new XRect(margen + 6, y + 6, anchoUtil - 12, altoLeyenda - 8), XStringFormats.TopLeft);

        y += altoLeyenda + 6;

        // ===== Borde exterior de toda la tarjeta =====
        gfx.DrawRectangle(XPens.Black, margen - 6, yInicio - 6, anchoUtil + 12, y - yInicio + 12);

        using var salida = new MemoryStream();
        doc.Save(salida);
        return salida.ToArray();
    }

    /// Se dibuja como rectángulos vectoriales (no como imagen PNG rasterizada): PdfSharp.Drawing.
    /// XImage.FromStream no logra decodificar el PNG que produce QRCoder en Linux sin GDI+/libgdiplus
    /// (System.InvalidOperationException: "Unsupported image format"). Cada fila se dibuja fusionando
    /// corridas de módulos contiguos en un solo rectángulo (en vez de uno por módulo) — dibujar un
    /// rectángulo por módulo dejaba líneas blancas finas entre módulos vecinos por redondeo de subpíxel
    /// al renderizar el PDF; con una corrida por fila esas costuras internas desaparecen.
    private static void DibujarQr(XGraphics gfx, string contenido, double x, double y, double lado)
    {
        using var generadorQr = new QRCodeGenerator();
        using var datosQr = generadorQr.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.M);
        var matriz = datosQr.ModuleMatrix;
        var numModulos = matriz.Count;
        var ladoModulo = lado / numModulos;

        for (var fila = 0; fila < numModulos; fila++)
        {
            var columna = 0;
            while (columna < numModulos)
            {
                if (!matriz[fila][columna])
                {
                    columna++;
                    continue;
                }

                var inicioCorrida = columna;
                while (columna < numModulos && matriz[fila][columna]) columna++;
                var anchoCorrida = (columna - inicioCorrida) * ladoModulo;

                // +0.5pt de margen para que corridas de filas vecinas se solapen levemente y no dejen
                // costura horizontal entre filas (mismo motivo que la fusión por corrida).
                gfx.DrawRectangle(XBrushes.Black,
                    x + inicioCorrida * ladoModulo, y + fila * ladoModulo, anchoCorrida, ladoModulo + 0.5);
            }
        }
    }

    private static string InsertarEspacios(string valor, int cada)
    {
        var partes = new List<string>();
        for (var i = 0; i < valor.Length; i += cada)
        {
            partes.Add(valor.Substring(i, Math.Min(cada, valor.Length - i)));
        }
        return string.Join(' ', partes);
    }

    private static string DescripcionTipoDocumentoCliente(string codigo) => codigo switch
    {
        "6" => "RUC",
        "1" => "DNI",
        _ => "Documento"
    };
}
