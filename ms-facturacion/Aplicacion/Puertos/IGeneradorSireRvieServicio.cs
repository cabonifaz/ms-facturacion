using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

/// Genera el TXT del RVIE (SIRE Formato 14.4) — ver SIRE_RVIE_Estructura_Campos.md. El resultado va
/// codificado en ISO-8859-1 (Latin-1), no UTF-8 — confirmado contra un archivo real de SUNAT
/// (LE2060112979620260600140400021112.txt), y UTF-8 corrompería cualquier carácter con tilde/Ñ.
public interface IGeneradorSireRvieServicio
{
    byte[] Construir(IReadOnlyList<DocumentoSireRvie> documentos);
}
