namespace ms_facturacion.Dominio;

/// Proyección segura para HTTP — nunca incluye ValorCifrado/Nonce/Tag. Usada por Listar y por el
/// caso de uso de Obtener (que descarta esos campos de CredencialInquilinoDetalle antes de responder).
public sealed record CredencialInquilinoResumen(
    int IdCredencialInquilino, string TipoCredencialCodigo, string Usuario, bool Activo, DateTime? FchRotacion);
