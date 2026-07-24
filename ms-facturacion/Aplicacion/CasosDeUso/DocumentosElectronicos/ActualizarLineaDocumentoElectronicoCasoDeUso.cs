using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class ActualizarLineaDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<LineaDocumentoElectronico>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, int idLineaDocumentoElectronico,
        LineaDocumentoElectronicoEntrada linea, CancellationToken cancellationToken) =>
        repositorio.ActualizarLineaAsync(
            usuarioEjecutor, idInquilino, idDocumentoElectronico, idLineaDocumentoElectronico, linea, cancellationToken);
}
