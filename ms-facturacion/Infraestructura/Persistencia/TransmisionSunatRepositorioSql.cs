using MySqlConnector;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class TransmisionSunatRepositorioSql(IConfiguration configuracion) : ITransmisionSunatRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, NuevaTransmisionSunat transmision, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_TransmisionSunat_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdDocumentoElectronico", (object?)transmision.IdDocumentoElectronico ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intIdLoteDocumento", (object?)transmision.IdLoteDocumento ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchTipoProveedorCodigo", transmision.TipoProveedorCodigo);
            comando.Parameters.AddWithValue("@p_vchEndpoint", transmision.Endpoint);
            comando.Parameters.AddWithValue("@p_vchMetodo", transmision.Metodo);
            comando.Parameters.AddWithValue("@p_intNumeroIntento", transmision.NumeroIntento);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idTransmision = lector.GetInt32(lector.GetOrdinal("IdTransmisionSunat"));

            return ResultadoOperacion<int>.DeExito(mensaje, idTransmision);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idTransmisionSunat, ResultadoTransmisionSunat resultado,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_TransmisionSunat_Actualizar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdTransmisionSunat", idTransmisionSunat);
            comando.Parameters.AddWithValue("@p_intEstadoCodigo", (int)resultado.EstadoCodigo);
            comando.Parameters.AddWithValue("@p_vchSunatCodigoEstado", (object?)resultado.SunatCodigoEstado ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchSunatMensajeEstado", (object?)resultado.SunatMensajeEstado ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchTipoError", (object?)resultado.TipoError ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchMensajeError", (object?)resultado.MensajeError ?? DBNull.Value);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idActualizado = lector.GetInt32(lector.GetOrdinal("IdTransmisionSunat"));

            return ResultadoOperacion<int>.DeExito(mensaje, idActualizado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    private static async Task<(TipoMensaje IdTipoMensaje, string Mensaje)> LeerCabeceraAsync(
        MySqlDataReader lector, CancellationToken cancellationToken)
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
