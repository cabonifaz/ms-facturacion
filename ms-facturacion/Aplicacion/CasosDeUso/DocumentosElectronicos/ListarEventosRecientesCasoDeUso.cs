using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class ListarEventosRecientesCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<IReadOnlyList<EventoDocumentoReciente>>> EjecutarAsync(
        int idInquilino, int ultimoIdEvento, CancellationToken cancellationToken) =>
        repositorio.ListarEventosRecientesAsync(idInquilino, ultimoIdEvento, cancellationToken);
}
