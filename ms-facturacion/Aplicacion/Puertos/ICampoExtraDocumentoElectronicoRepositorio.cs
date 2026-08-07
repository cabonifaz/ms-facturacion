using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface ICampoExtraDocumentoElectronicoRepositorio
{
    Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, CampoExtraEntrada campo,
        CancellationToken cancellationToken);

    Task<ResultadoOperacion<IReadOnlyList<int>>> InsertarLoteAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, IReadOnlyList<CampoExtraEntrada> camposExtra,
        CancellationToken cancellationToken);

    Task<ResultadoOperacion<IReadOnlyList<CampoExtraDocumentoElectronico>>> ListarAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idCampoExtraDocumentoElectronico, CampoExtraEntrada campo,
        CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idCampoExtraDocumentoElectronico, CancellationToken cancellationToken);
}
