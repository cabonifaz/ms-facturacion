namespace ms_facturacion.Dominio;

/// Proyección mínima para resolver-y-descifrar por tipo (uso del Worker, Módulo 4) — matches SP_CredencialInquilino_ObtenerPorTipo.
public sealed record CredencialInquilinoCifrada(
    int IdCredencialInquilino, string Usuario, byte[] ValorCifrado, byte[] Nonce, byte[] Tag);
