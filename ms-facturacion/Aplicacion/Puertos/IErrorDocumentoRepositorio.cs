using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface IErrorDocumentoRepositorio
{
    Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, ErrorDocumento error, CancellationToken cancellationToken);

    /// Solo los errores/observaciones del último intento de envío a SUNAT (MAX(IdTransmisionSunat) para
    /// ese documento) — no el historial completo de reintentos anteriores. Ver SP_ErrorDocumento_ListarUltimoEnvio.
    Task<ResultadoOperacion<IReadOnlyList<ErrorDocumentoResumen>>> ListarUltimoEnvioAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken);
}
