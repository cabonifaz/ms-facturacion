using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

/// Puerto de persistencia pura — no cifra ni descifra, solo guarda/lee bytes ya cifrados bajo la llave maestra.
public interface ILlaveCifradoInquilinoRepositorio
{
    Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int versionLlave, byte[] llaveDatosCifrada, byte[] nonce, byte[] tag,
        string algoritmo, bool activo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<LlaveCifradoInquilino>> ObtenerActivaAsync(int idInquilino, CancellationToken cancellationToken);

    Task<ResultadoOperacion<LlaveCifradoInquilino>> ObtenerPorVersionAsync(
        int idInquilino, int versionLlave, CancellationToken cancellationToken);
}
