using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.CamposExtraDocumentoElectronico;

public sealed class EliminarCampoExtraDocumentoElectronicoCasoDeUso(ICampoExtraDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idCampoExtraDocumentoElectronico, CancellationToken cancellationToken) =>
        repositorio.EliminarAsync(usuarioEjecutor, idInquilino, idCampoExtraDocumentoElectronico, cancellationToken);
}
