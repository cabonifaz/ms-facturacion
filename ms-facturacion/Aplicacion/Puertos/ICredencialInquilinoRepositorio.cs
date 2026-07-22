using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

/// Puerto de persistencia pura — nunca cifra/descifra, solo guarda/lee los bytes que le entrega la capa
/// de aplicación (que ya coordinó el cifrado vía ICifradoInquilinoServicio).
public interface ICredencialInquilinoRepositorio
{
    Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string tipoCredencialCodigo, string usuario,
        byte[] valorCifrado, byte[] nonce, byte[] tag, int versionLlave, bool activo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<CredencialInquilinoDetalle>> ObtenerAsync(
        int idInquilino, int idCredencialInquilino, CancellationToken cancellationToken);

    Task<ResultadoOperacion<CredencialInquilinoCifrada>> ObtenerPorTipoAsync(
        int idInquilino, int idEmpresa, string tipoCredencialCodigo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<ResultadoPaginado<CredencialInquilinoResumen>>> ListarAsync(
        int idInquilino, int idEmpresa, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idCredencialInquilino, string usuario,
        byte[] valorCifrado, byte[] nonce, byte[] tag, int versionLlave, bool activo, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idCredencialInquilino, CancellationToken cancellationToken);
}
