using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.Credenciales;

/// Frontera de seguridad: obtiene el detalle completo (con el valor cifrado) del repositorio y
/// descarta esos campos antes de devolver algo que un Controller pueda exponer por HTTP.
public sealed class ObtenerCredencialCasoDeUso(ICredencialInquilinoRepositorio repositorio)
{
    public async Task<ResultadoOperacion<CredencialInquilinoResumen>> EjecutarAsync(
        int idInquilino, int idCredencialInquilino, CancellationToken cancellationToken)
    {
        var detalle = await repositorio.ObtenerAsync(idInquilino, idCredencialInquilino, cancellationToken);
        if (detalle.IdTipoMensaje != TipoMensaje.Exito || detalle.Datos is null)
        {
            return new ResultadoOperacion<CredencialInquilinoResumen>(detalle.IdTipoMensaje, detalle.Mensaje, default);
        }

        var resumen = new CredencialInquilinoResumen(
            detalle.Datos.IdCredencialInquilino, detalle.Datos.TipoCredencialCodigo, detalle.Datos.Usuario,
            detalle.Datos.Activo, detalle.Datos.FchRotacion);

        return ResultadoOperacion<CredencialInquilinoResumen>.DeExito(detalle.Mensaje, resumen);
    }
}
