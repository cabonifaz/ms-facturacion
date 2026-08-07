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

    // Mismos símbolos sembrados en ms-facturación TABLA_MAESTRA IdMaestro=11.String3 (ver
    // 03_LlenarTablaMaestra_MsFacturacion.sql) — este generador no tiene acceso a BD, van fijos acá.
    private static readonly Dictionary<string, string> SimbolosMoneda = new()
    {
        ["PEN"] = "S/",
        ["USD"] = "US$",
        ["EUR"] = "€",
        ["GBP"] = "£"
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

        var fuenteTitulo = new XFont(FuenteEmbebidaResolver.NombreFamiliaBold, 13, XFontStyleEx.Regular);
        var fuenteSubtitulo = new XFont(FuenteEmbebidaResolver.NombreFamiliaBold, 10, XFontStyleEx.Regular);
        var fuenteTexto = new XFont(FuenteEmbebidaResolver.NombreFamilia, 8.5, XFontStyleEx.Regular);
        var fuenteTextoNegrita = new XFont(FuenteEmbebidaResolver.NombreFamiliaBold, 8.5, XFontStyleEx.Regular);
        var fuenteTextoChico = new XFont(FuenteEmbebidaResolver.NombreFamilia, 7.5, XFontStyleEx.Regular);
        var fuenteEncabezadoTabla = new XFont(FuenteEmbebidaResolver.NombreFamiliaBold, 8, XFontStyleEx.Regular);

        var margen = XUnit.FromMillimeter(12).Point;
        var anchoUtil = pagina.Width.Point - 2 * margen;
        var yInicio = margen;
        double y = yInicio + 8;

        var nombreTipoDocumento = NombresTipoDocumento.GetValueOrDefault(cabecera.TipoDocumentoCodigo, "COMPROBANTE ELECTRONICO");
        var nombreMoneda = NombresMoneda.GetValueOrDefault(cabecera.MonedaCodigo, cabecera.MonedaCodigo);
        var simboloMoneda = SimbolosMoneda.GetValueOrDefault(cabecera.MonedaCodigo, cabecera.MonedaCodigo);
        var ciudadEmpresa = $"{empresa.Distrito}-{empresa.Provincia}-{empresa.Departamento}";

        // ===== Cabecera: emisor (izquierda) + recuadro tipo/RUC/serie-correlativo (derecha) =====
        var anchoRecuadro = XUnit.FromMillimeter(65).Point;
        var xRecuadro = margen + anchoUtil - anchoRecuadro;

        var anchoCabeceraIzquierda = anchoUtil - anchoRecuadro - 10;
        var yCabeceraIzquierda = y + 6;
        yCabeceraIzquierda = DibujarLineas(gfx, EnvolverTexto(gfx, fuenteTitulo, empresa.RazonSocial, anchoCabeceraIzquierda),
            fuenteTitulo, margen, anchoCabeceraIzquierda, yCabeceraIzquierda, 14);
        yCabeceraIzquierda = DibujarLineas(gfx, EnvolverTexto(gfx, fuenteTexto, empresa.Direccion, anchoCabeceraIzquierda),
            fuenteTexto, margen, anchoCabeceraIzquierda, yCabeceraIzquierda, 9);
        yCabeceraIzquierda = DibujarLineas(gfx, EnvolverTexto(gfx, fuenteTexto, ciudadEmpresa, anchoCabeceraIzquierda),
            fuenteTexto, margen, anchoCabeceraIzquierda, yCabeceraIzquierda, 9);

        var altoRecuadro = XUnit.FromMillimeter(22).Point;
        gfx.DrawRectangle(XPens.Black, xRecuadro, y, anchoRecuadro, altoRecuadro);
        gfx.DrawString(nombreTipoDocumento, fuenteSubtitulo, XBrushes.Black,
            new XRect(xRecuadro, y + 5, anchoRecuadro, 14), XStringFormats.TopCenter);
        gfx.DrawString($"RUC: {empresa.Ruc}", fuenteTextoNegrita, XBrushes.Black,
            new XRect(xRecuadro, y + 20, anchoRecuadro, 14), XStringFormats.TopCenter);
        gfx.DrawString($"{cabecera.Serie}-{cabecera.Correlativo}", fuenteSubtitulo, XBrushes.Black,
            new XRect(xRecuadro, y + 34, anchoRecuadro, 16), XStringFormats.TopCenter);

        y += Math.Max(Math.Max(altoRecuadro + 20, 55), yCabeceraIzquierda - y + 10);

        // ===== Datos del comprobante: etiqueta : valor, alineado por columna =====
        gfx.DrawLine(XPens.Black, margen, y, margen + anchoUtil, y);
        y += 8;

        // Las 3 partes (etiqueta/":"/valor) de una misma fila van todas por XRect+TopLeft — mezclar eso con
        // DrawString(..., XPoint) (que ancla por baseline, no por el techo del texto) hacía que el valor
        // apareciera ~9pt más abajo que su propia etiqueta, calzando visualmente con la fila siguiente.
        const double anchoEtiqueta = 130;
        double DibujarCampo(string etiqueta, string valor)
        {
            var anchoValor = anchoUtil - anchoEtiqueta - 8;
            gfx.DrawString(etiqueta, fuenteTexto, XBrushes.Black, new XRect(margen, y, anchoEtiqueta - 4, 12), XStringFormats.TopLeft);
            gfx.DrawString(":", fuenteTexto, XBrushes.Black, new XRect(margen + anchoEtiqueta, y, 8, 12), XStringFormats.TopLeft);
            var lineasValor = EnvolverTexto(gfx, fuenteTextoNegrita, valor, anchoValor);
            return DibujarLineas(gfx, lineasValor, fuenteTextoNegrita, margen + anchoEtiqueta + 8, anchoValor, y, 12);
        }

        // Dirección y distrito-provincia-departamento van en líneas separadas (no concatenadas en un solo
        // valor) para que calcen con la representación impresa de referencia de SUNAT.
        double DibujarCampoDosLineas(string etiqueta, string valorLinea1, string valorLinea2)
        {
            var anchoValor = anchoUtil - anchoEtiqueta - 8;
            gfx.DrawString(etiqueta, fuenteTexto, XBrushes.Black, new XRect(margen, y, anchoEtiqueta - 4, 12), XStringFormats.TopLeft);
            gfx.DrawString(":", fuenteTexto, XBrushes.Black, new XRect(margen + anchoEtiqueta, y, 8, 12), XStringFormats.TopLeft);
            var yValor = y;
            yValor = DibujarLineas(gfx, EnvolverTexto(gfx, fuenteTextoNegrita, valorLinea1, anchoValor),
                fuenteTextoNegrita, margen + anchoEtiqueta + 8, anchoValor, yValor, 11);
            yValor = DibujarLineas(gfx, EnvolverTexto(gfx, fuenteTextoNegrita, valorLinea2, anchoValor),
                fuenteTextoNegrita, margen + anchoEtiqueta + 8, anchoValor, yValor, 11);
            return yValor;
        }

        y = DibujarCampo("Fecha de Emisión", cabecera.FechaEmision.ToString("dd/MM/yyyy"));
        y = DibujarCampo("Señor(es)", cabecera.ClienteNombre);
        y = DibujarCampoDosLineas("Establecimiento del Emisor", empresa.Direccion, ciudadEmpresa) + 12;
        y = DibujarCampo("Tipo de Moneda", nombreMoneda);
        y = DibujarCampo("Observación", cabecera.NumeroReferencia ?? "") + 6;

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
        }
        y += 16;
        var yFilasInicio = y;

        foreach (var linea in documento.Lineas)
        {
            // XGraphics.DrawString(texto, fuente, brush, XRect, formato) NO hace word-wrap por sí solo —
            // el XRect solo se usa para alinear una única línea, el texto completo se dibuja de largo sin
            // cortarse aunque se salga del rectángulo. Por eso la descripción se parte a mano en líneas
            // (EnvolverTexto, midiendo ancho real con MeasureString) y se dibuja una DrawString por línea.
            var anchoDescripcion = anchosColumna[2] - 6;
            var lineasDescripcion = EnvolverTexto(gfx, fuenteTextoChico, linea.Descripcion, anchoDescripcion);
            var altoFila = Math.Max(14, lineasDescripcion.Count * 10 + 4);
            xCol = margen;
            var valores = new[]
            {
                linea.Cantidad.ToString("0.###", CultureInfo.InvariantCulture),
                linea.UnidadMedidaCodigo,
                string.Empty,
                linea.ValorUnitario.ToString("F2", CultureInfo.InvariantCulture),
                "0.00"
            };
            for (var i = 0; i < valores.Length; i++)
            {
                if (i == 2)
                {
                    for (var l = 0; l < lineasDescripcion.Count; l++)
                    {
                        gfx.DrawString(lineasDescripcion[l], fuenteTextoChico, XBrushes.Black,
                            new XRect(xCol + 3, y + 3 + l * 10, anchoDescripcion, 10), XStringFormats.TopLeft);
                    }
                }
                else
                {
                    gfx.DrawString(valores[i], fuenteTextoChico, XBrushes.Black,
                        new XRect(xCol, y + 3, anchosColumna[i], altoFila), XStringFormats.TopCenter);
                }
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

        var altoCajaGratuitas = 24;
        gfx.DrawRectangle(XPens.Black, margen, y, anchoGratuitas, altoCajaGratuitas);
        gfx.DrawString("Valor de Venta de Operaciones Gratuitas :", fuenteTexto, XBrushes.Black,
            new XRect(margen + 4, y + 4, anchoGratuitas * 0.65, 16), XStringFormats.TopLeft);
        gfx.DrawString($"{simboloMoneda} {cabecera.TotalGratuito:F2}", fuenteTextoNegrita, XBrushes.Black,
            new XRect(margen + anchoGratuitas * 0.65, y + 4, anchoGratuitas * 0.35 - 4, 16), XStringFormats.TopRight);

        var ySon = y + altoCajaGratuitas + 14;
        var montoLetras = NumeroALetrasConvertidor.Convertir(cabecera.TotalImporte, cabecera.MonedaCodigo);
        var yFinSon = DibujarLineas(gfx, EnvolverTexto(gfx, fuenteTextoNegrita, montoLetras, anchoGratuitas),
            fuenteTextoNegrita, margen, anchoGratuitas, ySon, 11);

        // Totales (derecha): valor de venta ya viene neto de descuento por línea (ValorLinea), Descuentos
        // acá es solo informativo (el monto ya está reflejado en TotalGravado/Exonerado/Inafecto/Gratuito).
        // Todas las filas siempre visibles (aunque sean 0.00) — igual que la referencia, que muestra
        // Anticipos/ISC/ICBPER/Otros Cargos/Otros Tributos/Monto de redondeo aunque valgan cero.
        var subTotalVentas = cabecera.TotalGravado + cabecera.TotalExonerado + cabecera.TotalInafecto + cabecera.TotalGratuito;
        var filasTotales = new (string Etiqueta, decimal Monto)[]
        {
            ("Sub Total Ventas", subTotalVentas),
            ("Anticipos", 0),
            ("Descuentos", cabecera.TotalDescuento),
            ("Valor Venta", subTotalVentas),
            ("ISC", cabecera.TotalIsc),
            ("IGV", cabecera.TotalIgv),
            ("ICBPER", 0),
            ("Otros Cargos", cabecera.TotalCargo),
            ("Otros Tributos", cabecera.TotalOtrosTributos),
            ("Monto de redondeo", 0),
            ("Importe Total", cabecera.TotalImporte)
        };

        var yTotales = y;
        var altoFilaTotal = 13.0;
        var altoCajaTotales = filasTotales.Length * altoFilaTotal;
        gfx.DrawRectangle(XPens.Black, xTotales, yTotales, anchoTotales, altoCajaTotales);

        foreach (var (etiqueta, monto) in filasTotales)
        {
            var esImporteTotal = etiqueta == "Importe Total";
            var fuente = esImporteTotal ? fuenteTextoNegrita : fuenteTexto;
            gfx.DrawString(etiqueta, fuente, XBrushes.Black,
                new XRect(xTotales + 4, yTotales + 2, anchoTotales * 0.6, 12), XStringFormats.TopLeft);
            gfx.DrawString($"{simboloMoneda} {monto.ToString("F2", CultureInfo.InvariantCulture)}", fuente, XBrushes.Black,
                new XRect(xTotales + anchoTotales * 0.6, yTotales + 2, anchoTotales * 0.4 - 4, 12), XStringFormats.TopRight);
            if (etiqueta != filasTotales[^1].Etiqueta)
            {
                gfx.DrawLine(XPens.LightGray, xTotales, yTotales + altoFilaTotal, xTotales + anchoTotales, yTotales + altoFilaTotal);
            }
            yTotales += altoFilaTotal;
        }

        y = Math.Max(yFinSon + 10, yTotales + 15);

        // ===== Campos extra (pares libres etiqueta/valor cargados por el usuario, sin relación con SUNAT) =====
        if (documento.CamposExtra.Count > 0)
        {
            foreach (var campoExtra in documento.CamposExtra)
            {
                var anchoValorCampoExtra = anchoUtil - anchoEtiqueta - 8;
                gfx.DrawString(":", fuenteTexto, XBrushes.Black, new XRect(margen + anchoEtiqueta, y, 8, 12), XStringFormats.TopLeft);
                var yEtiqueta = DibujarLineas(gfx, EnvolverTexto(gfx, fuenteTexto, campoExtra.Etiqueta, anchoEtiqueta - 4),
                    fuenteTexto, margen, anchoEtiqueta - 4, y, 12);
                var yValor = DibujarLineas(gfx, EnvolverTexto(gfx, fuenteTextoNegrita, campoExtra.Valor, anchoValorCampoExtra),
                    fuenteTextoNegrita, margen + anchoEtiqueta + 8, anchoValorCampoExtra, y, 12);
                y = Math.Max(yEtiqueta, yValor);
            }
            y += 6;
        }

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

        // XGraphics.DrawString con un XRect NO hace word-wrap por sí solo (solo alinea una única línea) —
        // por eso cada párrafo se parte a mano con EnvolverTexto y se dibuja línea por línea con
        // DibujarLineas. Al código/hash (que no traen espacios naturales) se les insertan espacios cada 8
        // caracteres con InsertarEspacios solo para darle a EnvolverTexto puntos de corte — el valor real
        // (sin espacios) es el que se usa en cualquier otro lado (QR, comparaciones, etc.), acá es
        // puramente cosmético.
        var yLeyenda = y;
        yLeyenda = DibujarLineas(gfx, EnvolverTexto(gfx, fuenteTextoChico, $"Representación impresa de la {nombreTipoDocumento}.", anchoLeyenda),
            fuenteTextoChico, xLeyenda, anchoLeyenda, yLeyenda, 10);
        yLeyenda = DibujarLineas(gfx, EnvolverTexto(gfx, fuenteTextoChico, "Código de verificación:", anchoLeyenda),
            fuenteTextoChico, xLeyenda, anchoLeyenda, yLeyenda, 10);
        yLeyenda = DibujarLineas(gfx, EnvolverTexto(gfx, fuenteTextoChico, InsertarEspacios(codigoVerificacion, 8), anchoLeyenda),
            fuenteTextoChico, xLeyenda, anchoLeyenda, yLeyenda, 10);

        if (!string.IsNullOrEmpty(sunatHash))
        {
            yLeyenda += 4;
            yLeyenda = DibujarLineas(gfx, EnvolverTexto(gfx, fuenteTextoChico, "Hash:", anchoLeyenda),
                fuenteTextoChico, xLeyenda, anchoLeyenda, yLeyenda, 10);
            yLeyenda = DibujarLineas(gfx, EnvolverTexto(gfx, fuenteTextoChico, InsertarEspacios(sunatHash, 8), anchoLeyenda),
                fuenteTextoChico, xLeyenda, anchoLeyenda, yLeyenda, 10);
        }

        y += Math.Max(ladoQr, yLeyenda - y) + 12;

        // ===== Leyenda final =====
        // Alto calculado a partir de las líneas reales (EnvolverTexto) en vez de un valor fijo: a un largo
        // fijo, si nombreTipoDocumento cambia (o la traducción del texto crece), el texto puede necesitar
        // más líneas de las previstas y se sale por debajo del recuadro.
        // SUNAT no genera esta representación impresa — la genera el propio emisor (este servicio), SUNAT
        // solo valida/almacena el XML y devuelve el CDR. Decir "generada en el Sistema de SUNAT" era falso.
        var textoLeyendaFinal =
            $"Esta es una representación impresa de la {nombreTipoDocumento.ToLowerInvariant()}. " +
            "Puede verificarla utilizando el código de verificación indicado arriba.";
        var anchoLeyendaFinal = anchoUtil - 12;
        var lineasLeyendaFinal = EnvolverTexto(gfx, fuenteTextoChico, textoLeyendaFinal, anchoLeyendaFinal);
        var altoLeyenda = lineasLeyendaFinal.Count * 10 + 12;
        gfx.DrawRectangle(XPens.Black, margen, y, anchoUtil, altoLeyenda);
        DibujarLineas(gfx, lineasLeyendaFinal, fuenteTextoChico, margen + 6, anchoLeyendaFinal, y + 6, 10,
            XStringFormats.TopCenter);

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

        // Un solo XGraphicsPath con todos los módulos (fusionados por corrida horizontal) y un único
        // DrawPath — dibujar cada módulo/corrida como su propio DrawRectangle (intento anterior) dejaba
        // costuras visibles entre rectángulos vecinos al rasterizar el PDF; al ser un solo path con un
        // solo fill no hay bordes internos que puedan mostrar esa costura.
        var path = new XGraphicsPath();
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

                path.AddRectangle(x + inicioCorrida * ladoModulo, y + fila * ladoModulo, anchoCorrida, ladoModulo);
            }
        }

        gfx.DrawPath(XBrushes.Black, path);
    }

    /// XGraphics.DrawString(texto, fuente, brush, XRect, formato) NO hace word-wrap por sí solo — el XRect
    /// solo alinea una única línea, el texto se dibuja de largo aunque se salga del rectángulo. Esto arma
    /// a mano la lista de líneas (wrap "greedy por palabra", igual que un procesador de texto), midiendo
    /// ancho real con MeasureString en vez de asumir un promedio de caracteres por línea.
    private static List<string> EnvolverTexto(XGraphics gfx, XFont fuente, string texto, double anchoDisponible)
    {
        if (string.IsNullOrEmpty(texto)) return [string.Empty];

        var anchoEspacio = gfx.MeasureString(" ", fuente).Width;
        var lineas = new List<string>();
        var lineaActual = string.Empty;
        var anchoLineaActual = 0.0;

        foreach (var palabra in texto.Split(' '))
        {
            var anchoPalabra = gfx.MeasureString(palabra, fuente).Width;

            // Una palabra más ancha que la columna entera nunca va a entrar en una línea aunque esté sola
            // — se corta carácter por carácter como último recurso, para no salirse del rectángulo.
            if (anchoPalabra > anchoDisponible)
            {
                if (lineaActual.Length > 0)
                {
                    lineas.Add(lineaActual);
                    lineaActual = string.Empty;
                    anchoLineaActual = 0;
                }

                var trozo = string.Empty;
                foreach (var caracter in palabra)
                {
                    if (trozo.Length > 0 && gfx.MeasureString(trozo + caracter, fuente).Width > anchoDisponible)
                    {
                        lineas.Add(trozo);
                        trozo = string.Empty;
                    }
                    trozo += caracter;
                }
                lineaActual = trozo;
                anchoLineaActual = gfx.MeasureString(trozo, fuente).Width;
                continue;
            }

            if (lineaActual.Length > 0 && anchoLineaActual + anchoEspacio + anchoPalabra > anchoDisponible)
            {
                lineas.Add(lineaActual);
                lineaActual = palabra;
                anchoLineaActual = anchoPalabra;
            }
            else
            {
                anchoLineaActual = lineaActual.Length == 0 ? anchoPalabra : anchoLineaActual + anchoEspacio + anchoPalabra;
                lineaActual = lineaActual.Length == 0 ? palabra : $"{lineaActual} {palabra}";
            }
        }

        if (lineaActual.Length > 0) lineas.Add(lineaActual);
        return lineas.Count > 0 ? lineas : [string.Empty];
    }

    private static double DibujarLineas(
        XGraphics gfx, IReadOnlyList<string> lineas, XFont fuente, double x, double ancho, double y, double alturaLinea,
        XStringFormat? formato = null)
    {
        foreach (var linea in lineas)
        {
            gfx.DrawString(linea, fuente, XBrushes.Black, new XRect(x, y, ancho, alturaLinea), formato ?? XStringFormats.TopLeft);
            y += alturaLinea;
        }
        return y;
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
}
