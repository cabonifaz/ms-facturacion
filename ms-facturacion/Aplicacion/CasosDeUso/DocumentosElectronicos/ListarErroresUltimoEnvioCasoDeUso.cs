using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class ListarErroresUltimoEnvioCasoDeUso(IErrorDocumentoRepositorio repositorio)
{
    public Task<ResultadoOperacion<IReadOnlyList<ErrorDocumentoResumen>>> EjecutarAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken) =>
        repositorio.ListarUltimoEnvioAsync(idInquilino, idDocumentoElectronico, cancellationToken);
}
