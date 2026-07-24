using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class AgregarLineaDocumentoElectronicoCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<LineaDocumentoElectronico>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico,
        LineaDocumentoElectronicoEntrada linea, CancellationToken cancellationToken) =>
        repositorio.AgregarLineaAsync(usuarioEjecutor, idInquilino, idDocumentoElectronico, linea, cancellationToken);
}
