namespace ms_facturacion.Dominio;

/// Proyección completa (incluye el valor cifrado) — uso interno del repositorio/servicio de cifrado,
/// nunca debe cruzar hacia un Controller.
public sealed record CredencialInquilinoDetalle(
    int IdCredencialInquilino, int IdEmpresa, string TipoCredencialCodigo, string Usuario,
    byte[] ValorCifrado, byte[] Nonce, byte[] Tag, bool Activo, DateTime? FchRotacion);
