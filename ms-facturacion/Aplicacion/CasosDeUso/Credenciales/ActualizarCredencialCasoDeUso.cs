using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.Credenciales;

/// Rotación: cifra el nuevo valor y sobreescribe — igual que Insertar, coordina cifrado + persistencia.
public sealed class ActualizarCredencialCasoDeUso(ICifradoInquilinoServicio cifradoServicio, ICredencialInquilinoRepositorio repositorio)
{
    public async Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idCredencialInquilino, string usuario, string valorPlano,
        bool activo, CancellationToken cancellationToken)
    {
        var cifrado = await cifradoServicio.CifrarAsync(idInquilino, valorPlano, cancellationToken);
        if (cifrado.IdTipoMensaje != TipoMensaje.Exito || cifrado.Datos is null)
        {
            return new ResultadoOperacion<int>(cifrado.IdTipoMensaje, cifrado.Mensaje, default);
        }

        return await repositorio.ActualizarAsync(
            usuarioEjecutor, idInquilino, idCredencialInquilino, usuario,
            cifrado.Datos.ValorCifrado, cifrado.Datos.Nonce, cifrado.Datos.Tag, cifrado.Datos.VersionLlave,
            activo, cancellationToken);
    }
}
