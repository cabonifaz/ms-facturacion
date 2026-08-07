using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.CamposExtraDocumentoElectronico;

public sealed class ActualizarCampoExtraDocumentoElectronicoCasoDeUso(ICampoExtraDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idCampoExtraDocumentoElectronico, CampoExtraEntrada campo,
        CancellationToken cancellationToken) =>
        repositorio.ActualizarAsync(usuarioEjecutor, idInquilino, idCampoExtraDocumentoElectronico, campo, cancellationToken);
}
