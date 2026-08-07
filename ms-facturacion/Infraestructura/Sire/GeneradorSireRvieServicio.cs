using System.Globalization;
using System.Text;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Sire;

/// Construye el TXT del RVIE (SIRE Formato 14.4, R.S. 112-2021/SUNAT y modificatorias) — 38 campos
/// separados por pipe, un renglón por documento (ver SIRE_RVIE_Estructura_Campos.md).
///
/// No replica el padding con espacios al final de cada línea que trae el archivo de ejemplo de SUNAT
/// (LE2060112979620260600140400021112.txt) — ese padding no es parte del formato documentado (el Anexo 2/3
/// describe registros de largo variable separados por pipe), y en el archivo real es inconsistente (22 de
/// 24 líneas quedan en 512 bytes, 2 en 509) — todo indica que es un artefacto de quien generó ese archivo,
/// no un requisito real de SUNAT.
public sealed class GeneradorSireRvieServicio : IGeneradorSireRvieServicio
{
    // Campos 15-25: bucket de solo Gravado usa el tributo/IGV real; los demás (Exonerado/Inafecto/ISC/
    // IVAP/ICBPER/Otros) que este proyecto no separa a nivel de campo quedan en "0.00" — mismo criterio
    // que ya usa GeneradorPdfComprobanteServicio para ICBPER/Monto de redondeo (no tracked, se informa 0).
    private const string CeroFijo = "0.00";

    public byte[] Construir(IReadOnlyList<DocumentoSireRvie> documentos)
    {
        var sb = new StringBuilder();

        foreach (var documento in documentos)
        {
            sb.Append(ConstruirLinea(documento));
            sb.Append("\r\n");
        }

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static string ConstruirLinea(DocumentoSireRvie documento)
    {
        // Notas de Crédito (07) van con montos negativos en el RVIE — reducen las ventas del período. Notas
        // de Débito (08) suman, van positivas. Los montos en DOCUMENTOS_ELECTRONICOS siempre se guardan en
        // positivo (así los necesita el XML UBL, cac:LegalMonetaryTotal nunca es negativo) — el signo para
        // SIRE se aplica acá, no en el origen.
        var signo = documento.TipoDocumentoCodigo == "07" ? -1 : 1;

        string Monto(decimal valor) => (valor * signo).ToString("F2", CultureInfo.InvariantCulture);

        var esNota = documento.TipoDocumentoCodigo is "07" or "08";

        var campos = new List<string>
        {
            /*  1 */ documento.EmpresaRuc,
            /*  2 */ documento.EmpresaRazonSocial,
            /*  3 */ documento.FechaEmision.ToString("yyyyMM"),
            /*  4 */ "", // CAR — AUTO, lo completa SUNAT
            /*  5 */ documento.FechaEmision.ToString("dd/MM/yyyy"),
            /*  6 */ "", // Fecha vencimiento/pago — solo tipo 14 (Recibos SP), no soportado en este proyecto
            /*  7 */ documento.TipoDocumentoCodigo,
            /*  8 */ documento.Serie,
            /*  9 */ documento.Correlativo.ToString(CultureInfo.InvariantCulture),
            /* 10 */ "", // Número final — solo boletas consolidadas, no soportado
            /* 11 */ documento.ClienteTipoDocumentoCodigo,
            /* 12 */ documento.ClienteNumeroDocumento,
            /* 13 */ documento.ClienteNombre,
            /* 14 */ Monto(documento.TotalExportacion),
            /* 15 */ Monto(documento.TotalGravado),
            /* 16 */ CeroFijo, // Descuento de la base imponible — ya neteado en ValorLinea antes de llegar acá
            /* 17 */ Monto(documento.TotalIgv),
            /* 18 */ CeroFijo, // Descuento del IGV — mismo criterio que 16
            /* 19 */ Monto(documento.TotalExonerado),
            /* 20 */ Monto(documento.TotalInafecto),
            /* 21 */ Monto(documento.TotalIsc),
            /* 22 */ CeroFijo, // Base imponible IVAP — no se trackea por separado (código 17 del Catálogo 07)
            /* 23 */ CeroFijo, // IVAP
            /* 24 */ CeroFijo, // ICBPER — no se trackea (igual que en GeneradorPdfComprobanteServicio)
            /* 25 */ Monto(documento.TotalOtrosTributos),
            /* 26 */ Monto(documento.TotalImporte),
            /* 27 */ documento.MonedaCodigo,
            /* 28 */ documento.TipoCambio?.ToString("0.000", CultureInfo.InvariantCulture) ?? "",
            /* 29 */ esNota ? documento.FechaEmisionDocModificado?.ToString("dd/MM/yyyy") ?? "" : "",
            /* 30 */ esNota ? documento.TipoDocumentoRelacionadoCodigo ?? "" : "",
            /* 31 */ esNota ? documento.SerieRelacionada ?? "" : "",
            /* 32 */ esNota ? documento.CorrelativoRelacionado?.ToString(CultureInfo.InvariantCulture) ?? "" : "",
            /* 33 */ "", // Contrato o proyecto — OPC
            /* 34 */ "", // Error tipo 1 — OPC
            /* 35 */ "", // Indicador de pago — OPC
            /* 36 */ "", // Anotación/estado — OPC
            /* 37 */ "", // Campo libre 1 — OPC
            /* 38 */ ""  // Campo libre 2 — OPC
        };

        return string.Join('|', campos);
    }
}
