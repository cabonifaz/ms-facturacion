using Microsoft.Data.SqlClient;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class ErrorDocumentoRepositorioSql(IConfiguration configuracion) : IErrorDocumentoRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, ErrorDocumento error, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_ErrorDocumento_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", error.IdDocumentoElectronico);
            comando.Parameters.AddWithValue("@intIdTransmisionSunat", (object?)error.IdTransmisionSunat ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchOrigenErrorCodigo", error.OrigenErrorCodigo);
            comando.Parameters.AddWithValue("@vchCodigoError", error.CodigoError);
            comando.Parameters.AddWithValue("@vchMensajeError", error.MensajeError);
            comando.Parameters.AddWithValue("@vchCampo", (object?)error.Campo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchSeveridadCodigo", error.SeveridadCodigo);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idError = lector.GetInt32(lector.GetOrdinal("IdErrorDocumento"));

            return ResultadoOperacion<int>.DeExito(mensaje, idError);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<IReadOnlyList<ErrorDocumentoResumen>>> ListarUltimoEnvioAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_ErrorDocumento_ListarUltimoEnvio", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<IReadOnlyList<ErrorDocumentoResumen>>(idTipoMensaje, mensaje, default);
            }

            var errores = new List<ErrorDocumentoResumen>();
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                errores.Add(new ErrorDocumentoResumen(
                    lector.GetInt32(lector.GetOrdinal("IdErrorDocumento")),
                    lector.GetString(lector.GetOrdinal("OrigenErrorCodigo")),
                    lector.GetString(lector.GetOrdinal("CodigoError")),
                    lector.GetString(lector.GetOrdinal("MensajeError")),
                    lector.IsDBNull(lector.GetOrdinal("Campo")) ? null : lector.GetString(lector.GetOrdinal("Campo")),
                    lector.GetString(lector.GetOrdinal("SeveridadCodigo")),
                    lector.GetDateTime(lector.GetOrdinal("FchCre"))));
            }

            return ResultadoOperacion<IReadOnlyList<ErrorDocumentoResumen>>.DeExito(mensaje, errores);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IReadOnlyList<ErrorDocumentoResumen>>.DeErrorSistema(ex.Message);
        }
    }

    private static async Task<(TipoMensaje IdTipoMensaje, string Mensaje)> LeerCabeceraAsync(
        SqlDataReader lector, CancellationToken cancellationToken)
    {
        if (!await lector.ReadAsync(cancellationToken))
        {
            return (TipoMensaje.ErrorSistema, MensajeSinCabecera);
        }

        var idTipoMensaje = (TipoMensaje)lector.GetInt32(lector.GetOrdinal("IdTipoMensaje"));
        var mensaje = lector.GetString(lector.GetOrdinal("Mensaje"));
        return (idTipoMensaje, mensaje);
    }
}
