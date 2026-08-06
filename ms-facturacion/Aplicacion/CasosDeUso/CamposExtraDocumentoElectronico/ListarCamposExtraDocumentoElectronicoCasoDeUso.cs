using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.CamposExtraDocumentoElectronico;

public sealed class ListarCamposExtraDocumentoElectronicoCasoDeUso(ICampoExtraDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<IReadOnlyList<Dominio.CampoExtraDocumentoElectronico>>> EjecutarAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(idInquilino, idDocumentoElectronico, cancellationToken);
}
