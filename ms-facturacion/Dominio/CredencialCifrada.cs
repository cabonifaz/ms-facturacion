namespace ms_facturacion.Dominio;

/// Resultado de cifrar un valor en texto plano — listo para persistir en CREDENCIALES_INQUILINO.
public sealed record CredencialCifrada(byte[] ValorCifrado, byte[] Nonce, byte[] Tag, int VersionLlave);
