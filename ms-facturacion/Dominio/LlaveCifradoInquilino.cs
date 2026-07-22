namespace ms_facturacion.Dominio;

/// Llave de datos cifrada bajo la llave maestra (envelope encryption) — nunca sale del par
/// ILlaveCifradoInquilinoRepositorio/ICifradoInquilinoServicio hacia capas superiores.
public sealed record LlaveCifradoInquilino(
    int IdLlaveCifradoInquilino, int VersionLlave, byte[] LlaveDatosCifrada, byte[] Nonce, byte[] Tag, string Algoritmo);
