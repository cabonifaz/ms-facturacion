using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.CamposExtraDocumentoElectronico;

public sealed class InsertarCampoExtraDocumentoElectronicoCasoDeUso(ICampoExtraDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, CampoExtraEntrada campo,
        CancellationToken cancellationToken) =>
        repositorio.InsertarAsync(usuarioEjecutor, idInquilino, idDocumentoElectronico, campo, cancellationToken);
}
