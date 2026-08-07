using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.CamposExtraDocumentoElectronico;

public sealed class InsertarLoteCamposExtraDocumentoElectronicoCasoDeUso(ICampoExtraDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<IReadOnlyList<int>>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, IReadOnlyList<CampoExtraEntrada> camposExtra,
        CancellationToken cancellationToken) =>
        repositorio.InsertarLoteAsync(usuarioEjecutor, idInquilino, idDocumentoElectronico, camposExtra, cancellationToken);
}
