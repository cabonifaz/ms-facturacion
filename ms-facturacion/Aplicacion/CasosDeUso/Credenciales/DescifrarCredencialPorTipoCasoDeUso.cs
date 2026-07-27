using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.Credenciales;

/// Uso exclusivo del Worker (Módulo 4) para resolver p.ej. la ClaveSol vigente de una empresa y
/// descifrarla en memoria antes de armar el SOAP envelope — deliberadamente NO expuesto por ningún
/// Controller: un valor descifrado nunca debe cruzar la frontera HTTP de este microservicio.
public sealed class DescifrarCredencialPorTipoCasoDeUso(
    ICredencialInquilinoRepositorio repositorio, ICifradoInquilinoServicio cifradoServicio)
{
    public async Task<ResultadoOperacion<string>> EjecutarAsync(
        int idInquilino, int idEmpresa, string tipoCredencialCodigo, CancellationToken cancellationToken)
    {
        var credencial = await repositorio.ObtenerPorTipoAsync(idInquilino, idEmpresa, tipoCredencialCodigo, cancellationToken);
        if (credencial.IdTipoMensaje != TipoMensaje.Exito || credencial.Datos is null)
        {
            return new ResultadoOperacion<string>(credencial.IdTipoMensaje, credencial.Mensaje, default);
        }

        return await cifradoServicio.DescifrarAsync(
            idInquilino, credencial.Datos.ValorCifrado, credencial.Datos.Nonce, credencial.Datos.Tag, cancellationToken);
    }
}
