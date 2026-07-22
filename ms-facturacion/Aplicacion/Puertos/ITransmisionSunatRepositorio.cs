using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface ITransmisionSunatRepositorio
{
    Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, NuevaTransmisionSunat transmision, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idTransmisionSunat, ResultadoTransmisionSunat resultado,
        CancellationToken cancellationToken);
}
