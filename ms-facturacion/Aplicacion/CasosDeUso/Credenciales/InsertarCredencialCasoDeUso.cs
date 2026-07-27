using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.Credenciales;

/// Coordina dos puertos: cifra el valor en texto plano y luego persiste el resultado — el repositorio
/// nunca ve texto plano, ni el servicio de cifrado sabe de CREDENCIALES_INQUILINO.
public sealed class InsertarCredencialCasoDeUso(ICifradoInquilinoServicio cifradoServicio, ICredencialInquilinoRepositorio repositorio)
{
    public async Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string tipoCredencialCodigo, string usuario,
        string valorPlano, bool activo, CancellationToken cancellationToken)
    {
        var cifrado = await cifradoServicio.CifrarAsync(idInquilino, valorPlano, cancellationToken);
        if (cifrado.IdTipoMensaje != TipoMensaje.Exito || cifrado.Datos is null)
        {
            return new ResultadoOperacion<int>(cifrado.IdTipoMensaje, cifrado.Mensaje, default);
        }

        return await repositorio.InsertarAsync(
            usuarioEjecutor, idInquilino, idEmpresa, tipoCredencialCodigo, usuario,
            cifrado.Datos.ValorCifrado, cifrado.Datos.Nonce, cifrado.Datos.Tag,
            activo, cancellationToken);
    }
}
